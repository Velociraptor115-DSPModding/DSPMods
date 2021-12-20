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
    public const string VERSION = "0.0.6";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(PlayerController__Init));
      _harmony.PatchAll(typeof(CreativeModeFunctions));
      _harmony.PatchAll(typeof(InfiniteInventoryPatch));
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

    [HarmonyReversePatch]
    public static void UnlockAllPublishedTech(PlayerAction instance)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        return TryExtractWithinIfBlock(code, generator
          , new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)KeyCode.T)
          , new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) }))
          , new CodeMatch(OpCodes.Brfalse));
      }

      _ = Transpiler(null, null);
      throw new NotImplementedException("Stub");
    }

    [HarmonyReversePatch]
    public static void CoverPlanetInFoundation(PlayerAction instance)
    {
      IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator generator)
      {
        return TryExtractWithinIfBlock(code, generator
          , new CodeMatch(OpCodes.Ldc_I4, (int)KeyCode.Keypad3)
          , new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) }))
          , new CodeMatch(OpCodes.Brfalse));
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
  }
}