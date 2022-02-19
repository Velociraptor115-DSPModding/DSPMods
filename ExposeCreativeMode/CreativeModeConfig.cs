using System;
using BepInEx;
using BepInEx.Configuration;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public static class CreativeModeConfig
  {
    public const string ConfigSection = "Auto-Enable";
    public static ConfigEntry<bool> autoEnableInfiniteInventory;
    public static ConfigEntry<bool> autoEnableInfiniteReach;
    public static ConfigEntry<bool> autoEnableInfinitePower;
    public static ConfigEntry<bool> autoEnableInstantResearch;
    public static ConfigEntry<bool> autoEnableInstantBuild;
    public static ConfigEntry<bool> autoEnableInstantReplicate;

    public static void Init(ConfigFile confFile)
    {
      autoEnableInfiniteInventory = confFile.Bind(ConfigSection, "Auto-enable Infinite Inventory", true, ConfigDescription.Empty);
      autoEnableInfiniteReach = confFile.Bind(ConfigSection, "Auto-enable Infinite Reach", true, ConfigDescription.Empty);
      autoEnableInfinitePower = confFile.Bind(ConfigSection, "Auto-enable Infinite Power", true, ConfigDescription.Empty);
      autoEnableInstantResearch = confFile.Bind(ConfigSection, "Auto-enable Instant Research", true, ConfigDescription.Empty);
      autoEnableInstantBuild = confFile.Bind(ConfigSection, "Auto-enable Instant Build", true, ConfigDescription.Empty);
      autoEnableInstantReplicate = confFile.Bind(ConfigSection, "Auto-enable Instant Replicate", true, ConfigDescription.Empty);
    }
  }
}