using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DysonSphereProgram.Modding.Blackbox.UI
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  [BepInDependency("dev.raptor.dsp.Blackbox")]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.Blackbox-UI";
    public const string NAME = "Blackbox-UI";
    public const string VERSION = "0.0.1";

    private Harmony _harmony;
    internal static ManualLogSource Log;
    internal static string Path;

    private void Awake()
    {
      Plugin.Log = Logger;
      Plugin.Path = Info.Location;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(BlackboxUIPatch));
      if (UIRoot.instance?.uiGame?.created ?? false)
        BlackboxUIGateway.Create();
      Logger.LogInfo("Blackbox-UI Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("Blackbox-UI OnDestroy() called");
      BlackboxUIGateway.Destroy();
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
      Plugin.Path = null;
    }
  }
}