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
    
    protected abstract string EnhancementKey { get; }

    public const string enhancementEnableDisableSection = "Enable / Disable Enhancements";

    public ConfigEntry<bool> IsEnabled;

    public void LifecycleUseConfig(ConfigFile configFile)
    {
      IsEnabled = configFile.Bind(enhancementEnableDisableSection, EnhancementKey, true);
      UseConfig(configFile);
    }

    public void LifecyclePatch(Harmony _harmony)
    {
      if (!IsEnabled.Value)
        return;
      Patch(_harmony);
    }

    public void LifecycleUnpatch()
    {
      if (!IsEnabled.Value)
        return;
      Unpatch();
    }
    
    public void LifecycleCreateUI()
    {
      if (!IsEnabled.Value)
        return;
      CreateUI();
    }
    
    public void LifecycleDestroyUI()
    {
      if (!IsEnabled.Value)
        return;
      DestroyUI();
    }
  }
}
