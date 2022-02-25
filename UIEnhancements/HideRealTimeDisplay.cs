using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine.UI;

namespace DysonSphereProgram.Modding.UIEnhancements;

public class HideRealTimeDisplay: EnhancementBase
{
  private static GameObject realTimeDisplay;
  
  protected override void UseConfig(ConfigFile configFile)
  {
    
  }
  protected override void Patch(Harmony _harmony)
  {
    
  }
  protected override void Unpatch()
  {
    
  }
  protected override void CreateUI()
  {
    var fpsStat = UIRoot.instance.GetComponentInChildren<UIFpsStat>(true);
    realTimeDisplay = fpsStat.realTimeTextComp.gameObject;
    realTimeDisplay.SetActive(false);
  }
  protected override void DestroyUI()
  {
    if (realTimeDisplay)
      realTimeDisplay.SetActive(true);
  }
  protected override string Name => "Hide Real Time Display";
}