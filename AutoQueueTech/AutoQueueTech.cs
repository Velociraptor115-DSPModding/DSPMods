using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;

namespace DysonSphereProgram.Modding.AutoQueueTech
{
    [BepInPlugin(GUID, NAME, VERSION)]
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
        LastResearchedTech,
        LeastHashesRequiredTechAware
    }

    class AutoQueueTech
    {
        private static ConfigEntry<AutoQueueMode> QueueMode;
        private static int lastResearchedTechId = 0;

        public static void InitConfig(ConfigFile configFile)
        {
            QueueMode = configFile.Bind("AutoQueueMode", "Auto-Queue Mode", AutoQueueMode.LeastHashesRequiredTechAware);
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
        public static void AutoQueueNext()
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
                }
                lastResearchedTechId = 0;
            }
            else if (QueueMode.Value == AutoQueueMode.LeastHashesRequiredTechAware)
            {
                var minTechId = 0;
                var minTech = new TechProto();

                foreach (var kvp in techStates)
                {
                    if (kvp.Key == 0)
                        continue;

                    var techState = kvp.Value;
                    var tech = LDB.techs.Select(kvp.Key);
                    if (tech == null)
                        continue;
                    if (techState.unlocked)
                        continue;
                    if (tech.IsHiddenTech)
                        continue;
                    if (!history.CanEnqueueTechIgnoreFull(kvp.Key))
                        continue;
                    if (minTechId == kvp.Key)
                        continue;

                    if (minTechId == 0)
                    {
                        minTech = LDB.techs.Select(kvp.Key);
                        if (minTech != null)
                        {
                            minTechId = kvp.Key;
                        }
                        continue;
                    }
                    int cmp = CompareTechs(minTech, tech);
                    if (cmp == 0 || cmp == 1)
                    {
                        minTech = tech;
                        minTechId = kvp.Key;

                        continue;
                    }
                    
                }
                if (minTechId != 0)
                {
                    history.EnqueueTech(minTechId);
                }
                lastResearchedTechId = 0;
            }
        }

        // Return highest tech (cube) id
        public static int GetHighestTechID(TechProto tech)
        {
            for (int i = TechProto.matrixIds.Length - 1; i >= 0; i --)
            {
                foreach (var j in tech.Items)
                {
                    if (j == TechProto.matrixIds[i])
                        return TechProto.matrixIds[i];
                }
            }
            return 0;
        }

        // return -1 if t1 < t2, 0 if t1 == t2, 1 if t1 > t2
        public static int CompareTechs(TechProto t1, TechProto t2)
        {
            int id1 = GetHighestTechID(t1);
            int id2 = GetHighestTechID(t2);

            if (id1 == 0 && id2 == 0)
                return CompareHashNeeded(t1, t2);
            if (id1 != 0 && id2 != 0)
            {
                if (id1 == id2)
                    return CompareHashNeeded(t1, t2);
                if (id1 > id2)
                    return 1;
                return -1;
            }

            if (id1 == 0)
            {
                return t1.HashNeeded > 18000L ? 1 : -1;
            }
            // id2 == 0
            return t2.HashNeeded > 18000L ? -1 : 1;
        }

        public static int CompareHashNeeded(TechProto t1, TechProto t2)
        {
            if (t1.HashNeeded == t2.HashNeeded)
                return 0;
            else if (t1.HashNeeded < t2.HashNeeded)
                return -1;
            else
                return 1; 
        }
    }
}
