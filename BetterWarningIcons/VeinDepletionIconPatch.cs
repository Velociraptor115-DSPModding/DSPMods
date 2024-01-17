using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection.Emit;
using UnityEngine;
using BepInEx.Configuration;

namespace DysonSphereProgram.Modding.BetterWarningIcons
{
  public static class VeinDepletionIconPatch
  {
    public const string ConfigSection = "Vein Depletion Warning";
    public static ConfigEntry<bool> enablePatch;
    public static ConfigEntry<bool> useTotalVeinDepletionAmountPatch;
    public static ConfigEntry<long> veinAmountToWarnFor;

    public static void InitConfig(ConfigFile confFile)
    {
      enablePatch = confFile.Bind(ConfigSection, "Enable Patch", true, ConfigDescription.Empty);
      useTotalVeinDepletionAmountPatch = confFile.Bind(ConfigSection, "Use Total Vein Amount", true, "Use total vein amount for vein depletion warning instead of minimum vein amount");
      veinAmountToWarnFor = confFile.Bind(ConfigSection, "Vein Amount Threshold", 1000L, "The amount at or below which the warning will trigger");
    }

    static void Patch_Miners_VeinDepletion(FactorySystem __instance, int start, int end)
    {
      var signPool = __instance.factory.entitySignPool;
      var veinPool = __instance.factory.veinPool;

      var compareWithTotal = useTotalVeinDepletionAmountPatch.Value;
      var warnValue = veinAmountToWarnFor.Value;

      for (var i = start; i < end; i++)
      {
        ref readonly var miner = ref __instance.minerPool[i];
        if (miner.id != i)
          continue;
        
        if (miner.type != EMinerType.Vein)
          continue;
        
        var entityId = miner.entityId;

        if (signPool[entityId].signType >= SignData.NO_POWER_CONN && signPool[entityId].signType <= SignData.LOW_POWER)
          continue;
        
        if (compareWithTotal)
          miner.GetTotalVeinAmount(veinPool);

        long compareAmount = compareWithTotal ? miner.totalVeinAmount : miner.minimumVeinAmount;
        signPool[entityId].signType = (compareAmount < warnValue) ? SignData.CUT_PRODUCTION_SOON : SignData.NONE;
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTick), typeof(long), typeof(bool))]
    static void Patch_Miners_VeinDepletion_SingleThread(FactorySystem __instance) => Patch_Miners_VeinDepletion(__instance, 1, __instance.minerCapacity);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTick), typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int))]
    static void Patch_Miners_VeinDepletion_MultiThread(FactorySystem __instance, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
      if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.minerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var start, out var end))
        Patch_Miners_VeinDepletion(__instance, start, end);
    }
  }
}
