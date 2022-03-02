using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;

namespace DysonSphereProgram.Modding.AutoQueueTech
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.AutoQueueTech";
    public const string NAME = "AutoQueueTech";
    public const string VERSION = "0.0.2";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      AutoQueueTech.InitConfig(Config);
      _harmony.PatchAll(typeof(AutoQueueTech));
      Logger.LogInfo("AutoQueueTech Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("AutoQueueTech OnDestroy() called");
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }
  
  public enum AutoQueueMode
  {
    LeastHashesRequired,
    LastResearchedTech
  }
  
  class AutoQueueTech
  {
    private static ConfigEntry<AutoQueueMode> QueueMode;
    private static int lastResearchedTechId = 0;

    public static void InitConfig(ConfigFile configFile)
    {
      QueueMode = configFile.Bind("AutoQueueMode", "Auto-Queue Mode", AutoQueueMode.LastResearchedTech);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.NotifyTechUnlock))]
    static void CaptureLastResearchedTech(ref GameHistoryData __instance, int _techId)
    {
      if (__instance.techQueue == null || __instance.techQueueLength > 1)
        return;
      lastResearchedTechId = _techId;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    static void AutoQueueNext()
    {
      if (DSPGame.IsMenuDemo)
        return;
      if (lastResearchedTechId == 0)
        return;
      if (QueueMode == null)
        return;

      var history = GameMain.history;
      if (history.techQueue == null || history.techQueueLength > 0)
        return;
      var techStates = history.techStates;

      if (QueueMode.Value == AutoQueueMode.LastResearchedTech)
      {
        if (techStates.ContainsKey(lastResearchedTechId) && !techStates[lastResearchedTechId].unlocked)
        {
          history.EnqueueTech(lastResearchedTechId);
          lastResearchedTechId = 0;
        }
      }
      else if (QueueMode.Value == AutoQueueMode.LeastHashesRequired)
      {
        var minTechId = 0;
        var minTechHash = long.MaxValue;
        foreach (var kvp in techStates)
        {
          if (kvp.Key == 0)
            continue;
          var techState = kvp.Value;
          if (techState.unlocked)
            continue;
          if (!history.CanEnqueueTechIgnoreFull(kvp.Key))
            continue;

          if (techState.hashNeeded < minTechHash)
          {
            minTechHash = techState.hashNeeded;
            minTechId = kvp.Key;
          }
        }
        if (minTechId != 0)
        {
          history.EnqueueTech(minTechId);
          lastResearchedTechId = 0;
        }
      }
    }
  }
}
