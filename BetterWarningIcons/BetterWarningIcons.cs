using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DysonSphereProgram.Modding.BetterWarningIcons
{
  [BepInPlugin(GUID, NAME, VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.BetterWarningIcons";
    public const string NAME = "BetterWarningIcons";
    public const string VERSION = "0.0.6";

    private Harmony _harmony;
    public static ManualLogSource Log;
    internal static string Path;

    private void Awake()
    {
      Log = Logger;
      Path = Info.Location;
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
      Log = null;
    }
  }
}
