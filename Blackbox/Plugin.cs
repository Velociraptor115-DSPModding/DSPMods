using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DysonSphereProgram.Modding.Blackbox
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.Blackbox";
    public const string NAME = "Blackbox";
    public const string VERSION = "0.0.1";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(BlackboxBenchmarkPatch));
      _harmony.PatchAll(typeof(BlackboxPatch));
      _harmony.PatchAll(typeof(Player__GameTick));
      Logger.LogInfo("Blackbox Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("Blackbox OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }

  [HarmonyPatch(typeof(Player), nameof(Player.GameTick))]
  class Player__GameTick
  {
    static bool debounceControl = false;

    static void Postfix(Player __instance)
    {
      if (Input.GetKey(KeyCode.LeftControl))
      {
        if (!debounceControl && Input.GetKeyDown(KeyCode.B))
        {
          BlackboxManager.Instance.AddAnalysis(new BlackboxBenchmarkV2(__instance.nearestFactory, __instance.controller.actionBuild.blueprintCopyTool.selectedObjIds));
          debounceControl = true;
        }

        if (!debounceControl && Input.GetKeyDown(KeyCode.N))
        {
          BlackboxManager.Instance.AddAnalysis(new BlackboxBenchmarkV3(__instance.nearestFactory, __instance.controller.actionBuild.blueprintCopyTool.selectedObjIds));
          debounceControl = true;
        }
      }
      else
      {
        debounceControl = false;
      }
    }
  }
}