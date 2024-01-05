using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace DysonSphereProgram.Modding.DankFrog;

[BepInPlugin(GUID, NAME, VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string GUID = "dev.raptor.dsp.DankFrog";
    public const string NAME = "DankFrog";
    public const string VERSION = "1.0.0";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
        Plugin.Log = Logger;
        _harmony = new Harmony(GUID);
        _harmony.PatchAll(typeof(Patch));
        Logger.LogInfo("DankFrog Awake() called");
    }

    private void OnDestroy()
    {
        Logger.LogInfo("DankFrog OnDestroy() called");
        _harmony?.UnpatchSelf();
        Plugin.Log = null;
    }
}