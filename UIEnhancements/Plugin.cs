using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using DysonSphereProgram.Modding.UIEnhancements.UI.Builder;

namespace DysonSphereProgram.Modding.UIEnhancements
{
  [BepInPlugin(GUID, NAME, VERSION)]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.UIEnhancements";
    public const string NAME = "UIEnhancements";
    public const string VERSION = "0.0.4";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    internal List<EnhancementBase> enhancements = new List<EnhancementBase>();

    private void Awake()
    {
      Plugin.Log = Logger;
      enhancements.Add(new EditableStationStorageMax());
      enhancements.Add(new UnrestrictedUIScaler());
      enhancements.Add(new PartialOffscreenWindows());
      enhancements.Add(new HideRealTimeDisplay());
      enhancements.Add(new HideGameTimeDisplay());
      enhancements.Add(new SwapNewGameAndContinue());
      
      _harmony = new Harmony(GUID);
      enhancements.ForEach(x => x.LifecycleUseConfig(Config));
      enhancements.ForEach(x => x.LifecyclePatch(_harmony));
      UIBuilderPlugin.Create(GUID, OnCreateUI);
      Logger.LogInfo("UIEnhancements Awake() called");
    }

    private void OnCreateUI()
    {
      enhancements.ForEach(x => x.LifecycleCreateUI());
    }

    private void OnDestroy()
    {
      Logger.LogInfo("UIEnhancements OnDestroy() called");
      enhancements.ForEach(x => x.LifecycleDestroyUI());
      enhancements.ForEach(x => x.LifecycleUnpatch());
      _harmony?.UnpatchSelf();
      UIBuilderPlugin.Destroy();
      Plugin.Log = null;
    }
  }
}
