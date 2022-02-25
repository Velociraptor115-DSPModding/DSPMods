using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine.UI;

namespace DysonSphereProgram.Modding.UIEnhancements;

public class HideGameTimeDisplay: EnhancementBase
{
  private static GameObject gameTimeDisplay;
  
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
    gameTimeDisplay = fpsStat.timeTextComp.gameObject;
    gameTimeDisplay.SetActive(false);
  }
  protected override void DestroyUI()
  {
    if (gameTimeDisplay)
      gameTimeDisplay.SetActive(true);
  }
  protected override string Name => "Hide Game Time Display";
}