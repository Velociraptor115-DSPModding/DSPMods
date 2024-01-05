using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection.Emit;

namespace DysonSphereProgram.Modding.BetterWarningIcons
{
  [BepInPlugin(GUID, NAME, VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.BetterWarningIcons";
    public const string NAME = "BetterWarningIcons";
    public const string VERSION = "0.0.4";

    private Harmony _harmony;
    public static ManualLogSource Log;
    internal static string Path;

    private void Awake()
    {
      Plugin.Log = Logger;
      Plugin.Path = Info.Location;
      InsufficientInputIconPatch.InitConfig(Config);
      VeinDepletionIconPatch.InitConfig(Config);
      _harmony = new Harmony(GUID);
      if (InsufficientInputIconPatch.enablePatch.Value)
        _harmony.PatchAll(typeof(InsufficientInputIconPatch));
      if (VeinDepletionIconPatch.enablePatch.Value)
        _harmony.PatchAll(typeof(VeinDepletionIconPatch));
      Logger.LogInfo("BetterWarningIcons Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("BetterWarningIcons OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }
}
