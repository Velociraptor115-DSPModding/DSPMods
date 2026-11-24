using System;
using System.Collections.Generic;
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
        public const string VERSION = "0.0.6";

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
        LeastHashesRequiredTechAware,
        UsefulUpgradesOnly
    }

    class AutoQueueTech
    {
        // Late-game list: the upgrades that raise white matrix output (mining, research, logistics); internal tech names, so it matches in every language
        public const string DefaultUsefulUpgrades =
            "矿物利用, 研究速度, 无人机引擎, 运输船引擎, 运输机舱扩容, 运输站集装物流, 分拣器货物叠加, 分拣器货物集装, 集装分拣器叠加, 垂直建造, 配送范围, 戴森球吸附速度";

        private static ConfigEntry<AutoQueueMode> QueueMode;
        private static ConfigEntry<string> UsefulUpgrades;
        private static int lastResearchedTechId = 0;

        public static void InitConfig(ConfigFile configFile)
        {
            QueueMode = configFile.Bind("AutoQueueMode", "Auto-Queue Mode", AutoQueueMode.LeastHashesRequiredTechAware,
                "UsefulUpgradesOnly = like LeastHashesRequiredTechAware, but upgrade techs (id 2000 and up) are only queued when they are on the Useful Upgrades list "
                + "(UsefulUpgradesOnly：和 LeastHashesRequiredTechAware 一样，但升级科技只排 Useful Upgrades 名单里的)");
            UsefulUpgrades = configFile.Bind("AutoQueueMode", "Useful Upgrades", DefaultUsefulUpgrades,
                "Used by UsefulUpgradesOnly. Comma separated tech names as the game shows them (any language) or tech ids; add :N to stop after level N, e.g. 研究速度:140. "
                + "Main-tree techs are always allowed, and so are the techs a listed upgrade still needs first. "
                + "(UsefulUpgradesOnly 用的名单：逗号分隔，名字照游戏里写，或写科技编号；加 :N 表示研究到第 N 级为止。主线科技不受限，名单里科技需要的前置也会排)");
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
                    var tech = LDB.techs.Select(kvp.Key);
                    if (techState.unlocked)
                        continue;
                    if (tech.IsObsolete)
                      continue;
                    if (tech.IsHiddenTech)
                      continue;
                    if (!history.CanEnqueueTechIgnoreFull(kvp.Key))
                        continue;

                    var hashesLeft = techState.hashNeeded - techState.hashUploaded;
                    if (hashesLeft < minTechHash)
                    {
                        minTechHash = hashesLeft;
                        minTechId = kvp.Key;
                    }
                }
                if (minTechId != 0)
                {
                    history.EnqueueTech(minTechId);
                }
                lastResearchedTechId = 0;
            }
            else if (QueueMode.Value == AutoQueueMode.LeastHashesRequiredTechAware || QueueMode.Value == AutoQueueMode.UsefulUpgradesOnly)
            {
                var minTechId = 0;
                var minTech = new TechProto();
                var allowedUpgrades = QueueMode.Value == AutoQueueMode.UsefulUpgradesOnly ? AllowedUpgrades(history) : null;

                foreach (var kvp in techStates)
                {
                    if (kvp.Key == 0)
                        continue;
                    if (allowedUpgrades != null && kvp.Key >= FirstUpgradeTechId && !allowedUpgrades.Contains(kvp.Key))
                        continue;

                    var techState = kvp.Value;
                    var tech = LDB.techs.Select(kvp.Key);
                    if (tech == null)
                        continue;
                    if (techState.unlocked)
                        continue;
                    if (tech.IsObsolete)
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
                    int cmp = CompareTechs(minTech, tech, techStates);
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
                    if (allowedUpgrades != null)
                        Plugin.Log?.LogInfo($"UsefulUpgradesOnly queued {minTechId} {minTech.name} level {history.TechState(minTechId).curLevel}");
                }
                else if (allowedUpgrades != null)
                {
                    Plugin.Log?.LogInfo("UsefulUpgradesOnly: nothing on the list can be researched now, queue left empty");
                }
                lastResearchedTechId = 0;
            }
        }

        // Tech ids from here on are the upgrades (mecha, logistics, mining, research, combat upgrades ...); below are the main tree
        public const int FirstUpgradeTechId = 2000;

        private static string parsedList;
        private static readonly Dictionary<string, int> usefulCaps = new Dictionary<string, int>(); // chain name -> last level, 0 = no cap

        // All levels of one upgrade share the proto name ("T矿物利用"); that is the chain key
        private static string ChainKey(TechProto tech) => tech.Name.StartsWith("T") ? tech.Name.Substring(1) : tech.Name;

        private static void ParseUsefulUpgrades()
        {
            var text = UsefulUpgrades.Value ?? "";
            if (text == parsedList)
                return;
            parsedList = text;
            usefulCaps.Clear();
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tech in LDB.techs.dataArray)
            {
                if (tech == null)
                    continue;
                var key = ChainKey(tech);
                byName[key] = key;
                if (!string.IsNullOrEmpty(tech.name))
                    byName[tech.name.Trim()] = key;
                byName[tech.ID.ToString()] = key;
            }
            foreach (var raw in text.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var item = raw.Trim();
                if (item.Length == 0)
                    continue;
                var cap = 0;
                var colon = item.LastIndexOfAny(new[] { ':', '：' });
                if (colon > 0 && int.TryParse(item.Substring(colon + 1).Trim(), out var n))
                {
                    cap = n;
                    item = item.Substring(0, colon).Trim();
                }
                if (byName.TryGetValue(item, out var chain))
                    usefulCaps[chain] = cap;
                else
                    Plugin.Log?.LogWarning($"Useful Upgrades: '{item}' is not a tech name or id, ignored");
            }
            var shown = new List<string>();
            foreach (var kv in usefulCaps)
                shown.Add(kv.Value > 0 ? $"{kv.Key} up to level {kv.Value}" : kv.Key);
            Plugin.Log?.LogInfo("Useful Upgrades: " + string.Join(", ", shown.ToArray()));
        }

        // Upgrades that may be queued now: every not-yet-researched level of a listed upgrade (up to its cap),
        // plus every not-yet-researched tech those still need first (e.g. 机甲核心 before 无人机引擎)
        private static HashSet<int> AllowedUpgrades(GameHistoryData history)
        {
            ParseUsefulUpgrades();
            var allowed = new HashSet<int>();
            var stack = new Stack<int>();
            foreach (var tech in LDB.techs.dataArray)
            {
                if (tech == null || !usefulCaps.TryGetValue(ChainKey(tech), out var cap))
                    continue;
                var state = history.TechState(tech.ID);
                if (state.unlocked)
                    continue;
                if (cap > 0 && Math.Max(state.curLevel, tech.Level) > cap)
                    continue;
                stack.Push(tech.ID);
            }
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!allowed.Add(id))
                    continue;
                var tech = LDB.techs.Select(id);
                if (tech == null)
                    continue;
                foreach (var pre in tech.PreTechs)
                    if (!history.TechUnlocked(pre, tech.PreTechsMax))
                        stack.Push(pre);
                foreach (var pre in tech.PreTechsImplicit)
                    if (!history.TechUnlocked(pre))
                        stack.Push(pre);
            }
            return allowed;
        }

        // Return highest tech (cube) id
        public static int GetHighestTechID(TechProto tech)
        {
            for (int i = TechProto.matrixIds.Length - 1; i >= 0; i--)
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
        public static int CompareTechs(TechProto t1, TechProto t2, Dictionary<int, TechState> techStates)
        {
            int id1 = GetHighestTechID(t1);
            int id2 = GetHighestTechID(t2);

            if (id1 == 0 && id2 == 0)
                return CompareHashNeeded(techStates[t1.ID], techStates[t2.ID]);
            if (id1 != 0 && id2 != 0)
            {
                if (id1 == id2)
                    return CompareHashNeeded(techStates[t1.ID], techStates[t2.ID]);
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

        public static int CompareHashNeeded(in TechState ts1, in TechState ts2)
        {
            var t1HashesLeft = ts1.hashNeeded - ts1.hashUploaded;
            var t2HashesLeft = ts2.hashNeeded - ts1.hashUploaded;
            return Math.Sign(t1HashesLeft - t2HashesLeft);
        }
    }
}
