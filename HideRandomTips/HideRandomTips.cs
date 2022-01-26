using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DysonSphereProgram.Modding.HideRandomTips
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.HideRandomTips";
    public const string NAME = "HideRandomTips";
    public const string VERSION = "1.0.0";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(HideRandomTips));
      Plugin.Log.LogInfo(nameof(Modding.HideRandomTips) + " Awake() called");
    }

    private void OnDestroy()
    {
      Plugin.Log.LogInfo(nameof(Modding.HideRandomTips) + " OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }


  [HarmonyPatch]
  class HideRandomTips
  {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIRandomTip), nameof(UIRandomTip.popOver), MethodType.Getter)]
    static void AlwaysReturnTrue(ref bool __result)
    {
      __result = true;
    }
  }
}
