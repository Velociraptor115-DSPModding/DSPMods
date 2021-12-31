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
    internal static string Path;

    private void Awake()
    {
      Plugin.Log = Logger;
      Plugin.Path = Info.Location;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(BlackboxBenchmarkPatch));
      _harmony.PatchAll(typeof(BlackboxPatch));
      _harmony.PatchAll(typeof(InputUpdatePatch));
      Logger.LogInfo("Blackbox Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("Blackbox OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
      Plugin.Path = null;
    }
  }

  [HarmonyPatch(typeof(VFInput), nameof(VFInput.OnUpdate))]
  class InputUpdatePatch
  {
    static void Postfix()
    {
      if (Input.GetKey(KeyCode.LeftControl))
      {
        var player = GameMain.mainPlayer;

        if (Input.GetKeyDown(KeyCode.N) && player.factory != null)
        {
          var selection = BlackboxSelection.CreateFrom(player.factory, player.controller.actionBuild.blueprintCopyTool.selectedObjIds);
          BlackboxManager.Instance.CreateForSelection(selection);
        }
      }
    }
  }
}