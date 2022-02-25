using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace DysonSphereProgram.Modding.UIEnhancements
{

  public abstract class EnhancementBase
  {
    protected abstract void UseConfig(ConfigFile configFile);
    protected abstract void Patch(Harmony _harmony);
    protected abstract void Unpatch();
    protected abstract void CreateUI();
    protected abstract void DestroyUI();
    
    protected abstract string Name { get; }

    protected string ConfigSection => "## " + Name;

    public const string enhancementEnableDisableSection = "# Enable / Disable Enhancements";

    public ConfigEntry<bool> IsEnabled;

    public void LifecycleUseConfig(ConfigFile configFile)
    {
      IsEnabled = configFile.Bind(enhancementEnableDisableSection, Name, false);
      UseConfig(configFile);
    }

    public void LifecyclePatch(Harmony _harmony)
    {
      if (!IsEnabled.Value)
        return;
      try
      {
        Patch(_harmony);
      }
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
      }
    }

    public void LifecycleUnpatch()
    {
      if (!IsEnabled.Value)
        return;
      try
      {
        Unpatch();
      }
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
      }
    }
    
    public void LifecycleCreateUI()
    {
      if (!IsEnabled.Value)
        return;
      try
      {
        CreateUI();
      }
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
      }
    }
    
    public void LifecycleDestroyUI()
    {
      if (!IsEnabled.Value)
        return;
      try
      {
        DestroyUI();
      }
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
      }
    }
  }
}
