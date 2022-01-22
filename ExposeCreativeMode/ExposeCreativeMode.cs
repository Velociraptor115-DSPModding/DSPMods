using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using CommonAPI;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  [BepInDependency(CommonAPIPlugin.GUID)]
  [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(CustomKeyBindSystem))]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.ExposeCreativeMode";
    public const string NAME = "ExposeCreativeMode";
    public const string VERSION = "0.0.11";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(PlayerController__Init));
      _harmony.PatchAll(typeof(CreativeModeFunctions));
      _harmony.PatchAll(typeof(InfiniteInventoryPatch));
      _harmony.PatchAll(typeof(InfinitePowerPatch));
      _harmony.PatchAll(typeof(InfiniteReachPatch));
      _harmony.PatchAll(typeof(InfiniteResearchPatch));
      _harmony.PatchAll(typeof(InputHandlerPatch));
      KeyBinds.RegisterKeyBinds();
      Logger.LogInfo("ExposeCreativeMode Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("ExposeCreativeMode OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }

  [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Init))]
  class PlayerController__Init
  {
    [HarmonyPostfix]
    static void Postfix(PlayerController __instance)
    {
      if (DSPGame.IsMenuDemo)
        return;
      // We do a bit of extra stuff because when using it with ScriptEngine from
      // BepInEx.Debug, we might end up patching the method multiple times

      // If the last entry in the actions array isn't an instance of creative mode's base class, allocate space for it
      if (!(__instance.actions[__instance.actions.Length - 1] is PlayerAction_CreativeMode))
      {
        var newActions = new PlayerAction[__instance.actions.Length + 1];
        __instance.actions.CopyTo(newActions, 0);
        __instance.actions = newActions;
      }

      // Overwrite the last action with the latest creative mode code
      var creativeMode = new PlayerAction_CreativeMode();
      creativeMode.Init(__instance.player);
      __instance.actions[__instance.actions.Length - 1] = creativeMode;

      Debug.Log("Creative Mode Postfix patch applied");
    }
  }

  public delegate void InputUpdateHandler();

  [HarmonyPatch(typeof(VFInput), nameof(VFInput.OnUpdate))]
  class InputHandlerPatch
  {
    static void Postfix()
    {
      Update?.Invoke();
    }

    public static event InputUpdateHandler Update;
  }

  [HarmonyPatch(typeof(PlayerAction_Test), nameof(PlayerAction_Test.Update))]
  public class CreativeModeFunctions
  {
    static bool successfulPatch = true;

    static IEnumerable<CodeInstruction> TryExtractWithinIfBlock(IEnumerable<CodeInstruction> code, ILGenerator generator, params CodeMatch[] condition)
    {
      try
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
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
        successfulPatch = false;
        return code;
      }
    }

    static IEnumerable<CodeInstruction> ReplaceFoundationBrushColor(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
      try
      {
        var matcher = new CodeMatcher(code, generator);
        matcher.MatchForward(
          false
          , new CodeMatch(OpCodes.Ldc_I4_1)
          , new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(PlatformSystem), nameof(PlatformSystem.SetReformType)))
        );

        matcher.Set(OpCodes.Ldc_I4_7, null);
        return matcher.InstructionEnumeration();
      }
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
        successfulPatch = false;
        return code;
      }
    }

    static IEnumerable<CodeInstruction> ReplaceModValue(IEnumerable<CodeInstruction> code, ILGenerator generator, sbyte modValue)
    {
      try
      {
        var matcher = new CodeMatcher(code, generator);
        matcher.MatchForward(
          false
          , new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)51)
          , new CodeMatch(OpCodes.Stelem_I1)
        );

        matcher.Set(OpCodes.Ldc_I4_S, modValue);
        return matcher.InstructionEnumeration();
      }
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
        successfulPatch = false;
        return code;
      }
    }

    static IEnumerable<CodeInstruction> ExtractFlattenPlanetCode(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
      var extractedCode = TryExtractWithinIfBlock(code, generator
          , new CodeMatch(OpCodes.Ldc_I4, (int)KeyCode.Keypad3)
          , new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) }))
          , new CodeMatch(OpCodes.Brfalse));

      return ReplaceFoundationBrushColor(extractedCode, generator);
    }

    [HarmonyReversePatch]
    public static void FlattenPlanet(PlayerAction instance)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        var extractedCode = ExtractFlattenPlanetCode(code, generator);
        return extractedCode;
      }

      _ = Transpiler(null, null);
      throw new NotImplementedException("Stub");
    }

    [HarmonyReversePatch]
    public static void FlattenPlanetM1(PlayerAction instance)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        var extractedCode = ExtractFlattenPlanetCode(code, generator);
        return ReplaceModValue(extractedCode, generator, 34);
      }

      _ = Transpiler(null, null);
      throw new NotImplementedException("Stub");
    }

    [HarmonyReversePatch]
    public static void FlattenPlanetM2(PlayerAction instance)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        var extractedCode = ExtractFlattenPlanetCode(code, generator);
        return ReplaceModValue(extractedCode, generator, 17);
      }

      _ = Transpiler(null, null);
      throw new NotImplementedException("Stub");
    }

    [HarmonyReversePatch]
    public static void FlattenPlanetM3(PlayerAction instance)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        var extractedCode = ExtractFlattenPlanetCode(code, generator);
        return ReplaceModValue(extractedCode, generator, 0);
      }

      _ = Transpiler(null, null);
      throw new NotImplementedException("Stub");
    }

    [HarmonyReversePatch]
    public static void ResearchCurrentTechInstantly(PlayerAction instance)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        return TryExtractWithinIfBlock(code, generator
          , new CodeMatch(OpCodes.Ldc_I4, (int)KeyCode.Keypad6)
          , new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) }))
          , new CodeMatch(OpCodes.Brfalse));
      }

      _ = Transpiler(null, null);
      throw new NotImplementedException("Stub");
    }
    
    public static void ModifyAllVeinsHeight(PlayerAction instance, bool bury)
    {
      var planetData = instance.player.planetData;
      var factory = instance.player.factory;
      var physics = planetData.physics;
      var veinPool = factory.veinPool;
      for (int i = 1; i < factory.veinCursor; i++)
      {
        var veinPoolPos = veinPool[i].pos;
        var veinColliderId = veinPool[i].colliderId;
        var heightToSet = bury ? planetData.realRadius - 50f : planetData.data.QueryModifiedHeight(veinPool[i].pos) - 0.13f;
        physics.colChunks[veinColliderId >> 20].colliderPool[veinColliderId & 1048575].pos = physics.GetColliderData(veinColliderId).pos.normalized * (heightToSet + 0.4f);
        veinPool[i].pos = veinPoolPos.normalized * heightToSet;
        physics.SetPlanetPhysicsColliderDirty();
        GameMain.gpuiManager.AlterModel(veinPool[i].modelIndex, veinPool[i].modelId, i, veinPool[i].pos, false);
      }
      GameMain.gpuiManager.SyncAllGPUBuffer();
    }
  }
}