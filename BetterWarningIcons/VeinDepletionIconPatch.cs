using BepInEx.Configuration;
using HarmonyLib;

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
      useTotalVeinDepletionAmountPatch = confFile.Bind(
        ConfigSection,
        "Use Total Vein Amount",
        true,
        "Use total vein amount for vein depletion warning instead of minimum vein amount");
      veinAmountToWarnFor = confFile.Bind(
        ConfigSection,
        "Vein Amount Threshold",
        1000L,
        "The amount at or below which the warning will trigger");
    }

    static void PatchMinersVeinDepletion(FactorySystem factorySystem, int start, int end)
    {
      var signPool = factorySystem.factory.entitySignPool;
      var veinPool = factorySystem.factory.veinPool;
      var compareWithTotal = useTotalVeinDepletionAmountPatch.Value;
      var warnValue = veinAmountToWarnFor.Value;

      for (var i = start; i < end; i++)
      {
        ref var miner = ref factorySystem.minerPool[i];
        if (miner.id != i || miner.type != EMinerType.Vein)
          continue;

        var entityId = miner.entityId;
        var signType = signPool[entityId].signType;
        if (signType >= SignData.NO_POWER_CONN && signType <= SignData.LOW_POWER)
          continue;

        if (compareWithTotal)
          miner.GetTotalVeinAmount(veinPool);

        long compareAmount = compareWithTotal ? miner.totalVeinAmount : miner.minimumVeinAmount;
        signPool[entityId].signType = compareAmount < warnValue
          ? SignData.CUT_PRODUCTION_SOON
          : SignData.NONE;
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic.OnFactoryFrameEnd))]
    static void PatchMinersAfterProcessing()
    {
      var data = GameMain.data;
      if (data?.factories == null)
        return;

      for (var i = 0; i < data.factoryCount; i++)
      {
        var factory = data.factories[i];
        if (factory?.factorySystem == null)
          continue;
        PatchMinersVeinDepletion(factory.factorySystem, 1, factory.factorySystem.minerCapacity);
      }
    }
  }
}
