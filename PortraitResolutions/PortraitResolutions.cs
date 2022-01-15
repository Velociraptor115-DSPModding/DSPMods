using System.Linq;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DysonSphereProgram.Modding.PortraitResolutions
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.PortraitResolutions";
    public const string NAME = "PortraitResolutions";
    public const string VERSION = "1.0.0";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(PortraitResolutions));
      Logger.LogInfo("PortraitResolutions Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("PortraitResolutions OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }

  [HarmonyPatch]
  class PortraitResolutions
  {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameOption), nameof(GameOption.Apply))]
    public static void ResetToPortraitResolutionIfDetected(ref GameOption __instance)
    {
      if (Screen.currentResolution.width != __instance.resolution.width || Screen.currentResolution.height != __instance.resolution.height)
      {
        // Check if __instance.resolution is rotatable
        var rotatedResolutions = GetRotatedResolutions();
        foreach (var rotatedResolution in rotatedResolutions)
        {
          if (rotatedResolution.width == __instance.resolution.width && rotatedResolution.height == __instance.resolution.height)
          {
            Screen.SetResolution(__instance.resolution.width, __instance.resolution.height, __instance.fullscreen, __instance.resolution.refreshRate);
            break;
          }
        }
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.CollectResolutions))]
    public static void AddRotatedResolutions(UIOptionWindow __instance)
    {
      //var rotatedResolutions = GetRotatedResolutions();
      var existingResolutions = __instance.availableResolutions.ToArray();
      foreach (var resolution in existingResolutions)
      {
        var res = resolution;
        var tmp = res.width;
        res.width = res.height;
        res.height = tmp;
        __instance.availableResolutions.Add(res);
        __instance.resolutionComp.Items.Add(string.Concat(new string[]
        {
          res.width.ToString(),
          " x ",
          res.height.ToString(),
          "   ",
          res.refreshRate.ToString(),
          "Hz"
        }));
      }
    }

    public static Resolution[] GetRotatedResolutions()
    {
      return Screen.resolutions.Select(res =>
      {
        var tmp = res.width;
        res.width = res.height;
        res.height = tmp;
        return res;
      }).ToArray();
    }
  }
}
