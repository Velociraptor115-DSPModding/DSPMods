using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("DSPGAME.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "dev.raptor.dsp.ExposeCreativeMode";
        public const string NAME = "ExposeCreativeMode";
        public const string VERSION = "0.0.3";

        private Harmony _harmony;
        internal static ManualLogSource Log;

        private void Awake()
        {
            Plugin.Log = Logger;
            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(PlayerController__Init));
            _harmony.PatchAll(typeof(PlayerAction_Test__Update));
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
            if (!(__instance.actions[__instance.actions.Length - 1] is PlayerAction_Test))
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
    public class PlayerAction_Test__Update
    {
        static CodeMatch[] KeypadNum_PatchSite(KeyCode k) => new[]
        {
              new CodeMatch(OpCodes.Ldc_I4, (int)k)
            , new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new [] { typeof(KeyCode) }))
            , new CodeMatch(ci => ci.opcode == OpCodes.Brfalse)
        };

        static CodeMatcher PatchLeftControlSkipOverrideForKeypadNum(CodeMatcher matcher, KeyCode k)
        {
            matcher.MatchForward(true, KeypadNum_PatchSite(k));
            var ifBlockExitLabel = matcher.Operand;

            matcher
                .Advance(1)
                .Insert(
                      new CodeInstruction(OpCodes.Ldc_I4, (int)KeyCode.LeftControl)
                    , new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) }))
                    , new CodeInstruction(OpCodes.Brtrue, ifBlockExitLabel)
                );

            return matcher;
        }

        static CodeMatcher PlayWithIL(List<CodeInstruction> il_copy, ILGenerator generator)
        {
            var matcher = new CodeMatcher(il_copy, generator);

            PatchLeftControlSkipOverrideForKeypadNum(matcher, KeyCode.Keypad1);
            PatchLeftControlSkipOverrideForKeypadNum(matcher, KeyCode.Keypad0);

            return matcher;
        }

        const bool DryRun = false;

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var il_copy = new List<CodeInstruction>(instructions);
            try
            {
                PlayWithIL(il_copy, generator);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }

            if (DryRun)
            {
                foreach (var ins in il_copy)
                    yield return ins;
            }
            else
            {
                var modified_ins = PlayWithIL(il_copy, generator).InstructionEnumeration();
                foreach (var ins in modified_ins)
                    yield return ins;
            }
        }
    }
}