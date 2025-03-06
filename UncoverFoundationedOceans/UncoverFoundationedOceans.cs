using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection.Emit;

namespace DysonSphereProgram.Modding.UncoverFoundationedOceans
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.UncoverFoundationedOceans";
    public const string NAME = "UncoverFoundationedOceans";
    public const string VERSION = "0.0.3";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(ExposeOceansPatch));
      Plugin.Log.LogInfo("UncoverFoundationedOceans Awake() called");
    }

    private void OnDestroy()
    {
      Plugin.Log.LogInfo("UncoverFoundationedOceans OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }

  [HarmonyPatch]
  class ExposeOceansPatch
  {
    static Dictionary<int, int> previousModLevel = new Dictionary<int, int>();
    static Dictionary<int, int> newModLevel = new Dictionary<int, int>();
    static bool toExpose;
    static int[] cursorIndicesRestore = new int[100];
    static int cursorPointCountRestore;


    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.FlattenTerrainReform))]
    static void Patch(ref PlanetFactory __instance)
    {
      var indicesToReform = newModLevel.Keys;
      var data = __instance.planet.data;

      foreach (var index in indicesToReform)
      {
        var newLevel = newModLevel[index];
        var modDiff = newLevel - previousModLevel[index];

        if (!toExpose)
        {
          if (modDiff >= 0)
          { 
            data.SetModLevel(index, newLevel);
            SetDirtyFlags(__instance.planet, index, newLevel);
          }
        }
        else
        {
          if (modDiff <= 0)
          {
            data.SetModLevel(index, newLevel);
            SetDirtyFlags(__instance.planet, index, newLevel);
          }
        }
      }
    }

    static void AddModLevelForIndex(int index, int prevMod, int modChange)
    {
      previousModLevel[index] = prevMod;
      newModLevel[index] = toExpose ? (3 - modChange) : modChange;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.ComputeFlattenTerrainReform))]
    static IEnumerable<CodeInstruction> InterceptIndices(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
      var matcher = new CodeMatcher(code, generator);

      const string indexLocMatch = nameof(indexLocMatch);
      const string modChangeLocMatch = nameof(modChangeLocMatch);
      const string previousModLevelLocMatch = nameof(previousModLevelLocMatch);
      const string modDiffLocMatch = nameof(modDiffLocMatch);

      matcher.MatchForward(
        true
        , new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetFactory), nameof(PlanetFactory.tmp_levelChanges)))
        , new CodeMatch(OpCodes.Ldloc_S, name: indexLocMatch)
        , new CodeMatch(OpCodes.Ldloc_S, name: modChangeLocMatch)
        , new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Dictionary<int, int>), "Item"))
      );

      var indexLocal = matcher.NamedMatch(indexLocMatch).operand;
      var modChangeLocal = matcher.NamedMatch(modChangeLocMatch).operand;

      Plugin.Log.LogDebug(matcher.Pos);

      matcher.MatchBack(
        true
        , new CodeMatch(OpCodes.Ldloc_S, modChangeLocal)
        , new CodeMatch(OpCodes.Ldloc_S, name: previousModLevelLocMatch)
        , new CodeMatch(OpCodes.Blt)
      );

      var previousModLevelLocal = matcher.NamedMatch(previousModLevelLocMatch).operand;

      Plugin.Log.LogDebug(matcher.Pos);

      matcher.MatchBack(
        true
        , new CodeMatch(OpCodes.Ldloc_S, modChangeLocal)
        , new CodeMatch(OpCodes.Ldloc_S, previousModLevelLocal)
        , new CodeMatch(OpCodes.Sub)
        , new CodeMatch(OpCodes.Stloc_S, name: modDiffLocMatch)
      );

      var modDiffLocal = matcher.NamedMatch(modDiffLocMatch).operand;

      Plugin.Log.LogDebug(matcher.Pos);

      matcher.Advance(1);
      
      matcher.Insert(
          new CodeInstruction(OpCodes.Ldloc_S, indexLocal)
        , new CodeInstruction(OpCodes.Ldloc_S, previousModLevelLocal)
        , new CodeInstruction(OpCodes.Ldloc_S, modChangeLocal)
        , new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ExposeOceansPatch), nameof(ExposeOceansPatch.AddModLevelForIndex)))
      );

      return matcher.InstructionEnumeration();
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.ReformAction))]
    static void PreReformAction(ref BuildTool_Reform __instance)
    {
      toExpose = (CombineKey.currModifier & CombineKey.CTRL_COMB) == CombineKey.CTRL_COMB;
      
      // Adjust Foundation Usage
      cursorPointCountRestore = __instance.cursorPointCount;
      if (toExpose)
        Array.Copy(__instance.cursorIndices, cursorIndicesRestore, cursorIndicesRestore.Length);
      var countToCover = __instance.brushSize * __instance.brushSize;
      var platformSystem = __instance.factory.platformSystem;
      platformSystem.EnsureReformData();
      for (var i = 0; i < countToCover; i++)
      {
        var reformIndex = __instance.cursorIndices[i];
        if (reformIndex >= 0)
        {
          var reformType = platformSystem.GetReformType(reformIndex);
          var isReformed = platformSystem.IsTerrainReformed(reformType);
          if (toExpose)
          {
            if (isReformed && __instance.drawing)
            {
              platformSystem.SetReformType(reformIndex, 7);
              platformSystem.SetReformColor(reformIndex, 0); 
            }
            __instance.cursorIndices[i] = -1;
            __instance.cursorPointCount--;
          }
          else if (isReformed)
          {
            // __instance.cursorIndices[i] = -1;
            __instance.cursorPointCount--;
          }
        }
      }
      
      Plugin.Log.LogDebug($"{cursorPointCountRestore} - {__instance.cursorPointCount}");
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.ReformAction))]
    static void PostReformAction(ref BuildTool_Reform __instance)
    {
      if (toExpose)
        Array.Copy(cursorIndicesRestore, __instance.cursorIndices, cursorIndicesRestore.Length);
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.ComputeFlattenTerrainReform))]
    static void PreCompute(ref int pointsCount)
    {
      pointsCount = cursorPointCountRestore;
      previousModLevel.Clear();
      newModLevel.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.ComputeFlattenTerrainReform))]
    static void PostCompute(ref PlanetFactory __instance, ref int costSandCount, ref int getSandCount)
    {
      __instance.tmp_levelChanges.Clear();

      var indicesToReform = newModLevel.Keys;
      var heightData = __instance.planet.data.heightData;
      var realRadius = __instance.planet.realRadius;
      int soilRequired = 0;

      var totalCount = indicesToReform.Count;
      var effectiveCount = 0;

      foreach (var index in indicesToReform)
      {
        var modDiff = newModLevel[index] - previousModLevel[index];

        if (!toExpose)
        {
          if (modDiff > 0)
          {
            var heightDiff = realRadius + 0.2f - (heightData[index] * 0.01f);
            if (heightDiff < 0f)
              heightDiff *= 2f;
            soilRequired += UnityEngine.Mathf.FloorToInt(100f * modDiff * heightDiff * 0.3333333f);
            effectiveCount++;
          }
        }
        else
        {
          if (modDiff < 0)
          {
            var heightDiff = realRadius + 0.2f - (heightData[index] * 0.01f);
            if (heightDiff < 0f)
              heightDiff *= 2f;
            soilRequired -= UnityEngine.Mathf.FloorToInt(100f * -modDiff * heightDiff * 0.3333333f);
            effectiveCount++;
          }
        }
      }

      if (soilRequired < 0)
      {
        costSandCount = 0;
        getSandCount = -soilRequired;
      }
      else
      {
        costSandCount = soilRequired;
        getSandCount = 0;
      }

      //Plugin.Log.LogDebug($"Total: {totalCount}; Effective: {effectiveCount}");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlanetGrid), nameof(PlanetGrid.ReformSnapTo))]
    static IEnumerable<CodeInstruction> PatchToSelectAll(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
      var matcher = new CodeMatcher(code, generator);

      const string ifConditionEntryLabelMatch = nameof(ifConditionEntryLabelMatch);

      matcher.MatchForward(
        false
        , new CodeMatch(OpCodes.Ldarg_S)
        , new CodeMatch(OpCodes.Ldloc_S)
        , new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(PlatformSystem), nameof(PlatformSystem.IsTerrainReformed)))
        , new CodeMatch(OpCodes.Brtrue)
        , new CodeMatch(OpCodes.Ldloc_S)
        , new CodeMatch(OpCodes.Ldarg_3)
        , new CodeMatch(OpCodes.Bne_Un, name: ifConditionEntryLabelMatch)
      );
      
      Plugin.Log.LogDebug(matcher.Pos);

      var ifConditionEntryLabel = matcher.NamedMatch(ifConditionEntryLabelMatch).operand;

      matcher.Insert(
        new CodeInstruction(OpCodes.Br, ifConditionEntryLabel)
      );

      return matcher.InstructionEnumeration();
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(PlanetData), nameof(PlanetData.AddHeightMapModLevel))]
    static void SetDirtyFlags(PlanetData instance, int index, int level)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        var extractedCode = PatchHelpers.ExtractWithinIfBlock(code, generator
          , new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(PlanetRawData), nameof(PlanetRawData.AddModLevel)))
          , new CodeMatch(OpCodes.Brfalse));
        return extractedCode;
      }

      _ = Transpiler(null, null);
      throw new NotImplementedException("Stub");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetRawData), nameof(PlanetRawData.AddModLevel))]
    static void AlwaysReturnTrue(ref bool __result)
    {
      __result = true;
    }
  }

  public static class PatchHelpers
  {
    public static IEnumerable<CodeInstruction> ExtractWithinIfBlock(IEnumerable<CodeInstruction> code, ILGenerator generator, params CodeMatch[] condition)
    {
      var matcher = new CodeMatcher(code, generator);
      matcher.MatchForward(true, condition);

      var ifBlockExitLabel = (Label)matcher.Operand;

      matcher.Advance(1);
      var startPos = matcher.Pos;

      while (!matcher.Labels.Contains(ifBlockExitLabel))
        matcher.Advance(1);
      var endPos = matcher.Pos;

      matcher.Set(OpCodes.Ret, null);
      return matcher.InstructionsInRange(startPos, endPos);
    }
  }
}
