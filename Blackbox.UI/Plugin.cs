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
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.Blackbox-UI";
    public const string NAME = "Blackbox-UI";
    public const string VERSION = "0.0.1";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(BlackboxUIPatch));
      Logger.LogInfo("Blackbox-UI Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("Blackbox-UI OnDestroy() called");
      BlackboxUI.DestroyAll();
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }
}