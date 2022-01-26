using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DysonSphereProgram.Modding.DontPauseInMechaEditor
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.DontPauseInMechaEditor";
    public const string NAME = "DontPauseInMechaEditor";
    public const string VERSION = "1.0.0";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(DontPauseInMechaEditor));
      Plugin.Log.LogInfo(nameof(Modding.DontPauseInMechaEditor) + " Awake() called");
    }

    private void OnDestroy()
    {
      Plugin.Log.LogInfo(nameof(Modding.DontPauseInMechaEditor) + " OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }


  [HarmonyPatch]
  class DontPauseInMechaEditor
  {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DSPGame), nameof(DSPGame.PauseGameToMechaEditor))]
    static void KeepRunningTheGame()
    {
      GameMain.Resume();
    }
  }
}
