using BepInEx;
using BepInEx.Logging;
using DysonSphereProgram.Modding.CustomResolutions.UI.Builder;
using HarmonyLib;

namespace DysonSphereProgram.Modding.CustomResolutions;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("DSPGAME.exe")]
public class Plugin : BaseUnityPlugin
{
  public const string GUID = "dev.raptor.dsp.CustomResolutions";
  public const string NAME = "CustomResolutions";
  public const string VERSION = "1.0.0";

  private Harmony _harmony;
  internal static ManualLogSource Log;
  internal static CustomResolutionsController Controller;
  internal static UIManager uiManager;

  private void Awake()
  {
    Plugin.Log = Logger;
    Controller = new CustomResolutionsController(Config);
    uiManager = new UIManager(Controller);
    _harmony = new Harmony(GUID);
    _harmony.PatchAll(typeof(Patch));
    UIBuilderPlugin.Create(GUID, uiManager.CreateUI);
    Logger.LogInfo("CustomResolutions Awake() called");
  }

  private void OnDestroy()
  {
    Logger.LogInfo("CustomResolutions OnDestroy() called");
    uiManager.DestroyUI();
    uiManager = null;
    UIBuilderPlugin.Destroy();
    _harmony?.UnpatchSelf();
    Controller = null;
    Plugin.Log = null;
  }
}