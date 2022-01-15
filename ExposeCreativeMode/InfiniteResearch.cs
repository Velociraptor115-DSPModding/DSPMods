using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using CommonAPI;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public interface IInfiniteResearchProvider
  {
    bool IsEnabled { get; }
  }

  [HarmonyPatch]
  public static class InfiniteResearchPatch
  {
    private static IInfiniteResearchProvider provider;

    public static void Register(IInfiniteResearchProvider p)
    {
      provider = p;
    }

    public static void Unregister(IInfiniteResearchProvider p)
    {
      if (provider == p)
        provider = null;
    }

    private static int _ModifierAmount()
    {
      // Ctrl - 10
      // Shift - 100
      // Ctrl + Shift - 1000
      var ctrlHeld = (CombineKey.currModifier & 2) == 2;
      var shiftHeld = (CombineKey.currModifier & 1) == 1;
      var modifierAmount = ctrlHeld && shiftHeld ? 1000 : shiftHeld ? 100 : ctrlHeld ? 10 : 1;
      return modifierAmount;
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.UpdateInfoDynamic))]
    static void UpdateInfoPatch(UITechNode __instance)
    {
      var isEnabled = provider?.IsEnabled ?? false;
      if (!isEnabled)
        return;

      if (!__instance.selected)
        return;

      var history = GameMain.history;
      if (history.currentTech == __instance.techProto.ID)
        return;

      var isUnlocked = history.TechUnlocked(__instance.techProto.ID);
      var lockingMode = CustomKeyBindSystem.GetKeyBind(KeyBinds.HoldLockResearch).keyValue;
      var ts = history.TechState(__instance.techProto.ID);
      var isResearched = ts.curLevel != __instance.techProto.Level;

      if ((lockingMode && (isUnlocked || isResearched)) || (!lockingMode && !isUnlocked))
      {
        __instance.startButton.gameObject.SetActive(true);
        if (__instance.startButton.button.interactable != true)
        {
          __instance.startButton.button.interactable = true;
          __instance.startButton.LateUpdate();
        }

        var isMultipleLevelEntry = __instance.techProto.Level < __instance.techProto.MaxLevel;

        if (isMultipleLevelEntry)
        {
          var modifierAmount = _ModifierAmount();

          var effectiveLevel = lockingMode ? ts.curLevel - modifierAmount : ts.curLevel + modifierAmount;
          effectiveLevel = effectiveLevel >= __instance.techProto.MaxLevel ? __instance.techProto.MaxLevel : effectiveLevel;
          var willBeLocked = effectiveLevel < __instance.techProto.Level;

          var buttonText = willBeLocked ? "Lock" : "Set to Lv " + effectiveLevel;
          __instance.startButtonText.text = buttonText;
        }
        else
        {
          __instance.startButtonText.text = lockingMode ? "Lock" : "Unlock";
        }
      }

      if (lockingMode && !isUnlocked && !isResearched)
      {
        __instance.startButton.gameObject.SetActive(true);
        if (__instance.startButton.button.interactable != false)
        {
          __instance.startButton.button.interactable = false;
          __instance.startButton.LateUpdate();
        }

        __instance.startButtonText.text = "Already Locked";
      }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.OnStartButtonClick))]
    static void StartButtonClickPatch(UITechNode __instance, ref bool __runOriginal)
    {
      var isEnabled = provider?.IsEnabled ?? false;
      if (!isEnabled)
        return;

      if (!__instance.selected)
        return;

      var history = GameMain.history;
      if (history.currentTech == __instance.techProto.ID)
        return;

      var isUnlocked = history.TechUnlocked(__instance.techProto.ID);
      var lockingMode = CustomKeyBindSystem.GetKeyBind(KeyBinds.HoldLockResearch).keyValue;
      var ts = history.TechState(__instance.techProto.ID);
      var isResearched = ts.curLevel != __instance.techProto.Level;

      if ((lockingMode && (isUnlocked || isResearched)) || (!lockingMode && !isUnlocked))
      {
        var isMultipleLevelEntry = __instance.techProto.Level < __instance.techProto.MaxLevel;
        if (isMultipleLevelEntry)
        {
          var modifierAmount = _ModifierAmount();

          var effectiveLevel = lockingMode ? ts.curLevel - modifierAmount : ts.curLevel + modifierAmount;
          effectiveLevel = effectiveLevel >= __instance.techProto.MaxLevel ? __instance.techProto.MaxLevel : effectiveLevel;
          var willBeLocked = effectiveLevel < __instance.techProto.Level;

          if (willBeLocked)
          {
            InfiniteResearchHelper.LockTech(__instance.techProto.ID);
          }
          else
          {
            InfiniteResearchHelper.UnlockTech(__instance.techProto.ID, effectiveLevel);
          }
        }
        else
        {
          if (lockingMode)
          {
            InfiniteResearchHelper.LockTech(__instance.techProto.ID);
          }
          else
          {
            InfiniteResearchHelper.UnlockTech(__instance.techProto.ID);
          }
        }
      }

      __runOriginal = false;
    }
  }

  public struct TechFunctionLevelRangeValue
  {
    public int minLevel;
    public int maxLevel;
    public int techId;
    public double value;
  }

  public static class InfiniteResearchHelper
  {
    public static bool initialized { get; private set; } = false;

    public static Dictionary<int, List<int>> preTechs { get; private set; } = new Dictionary<int, List<int>>();
    public static Dictionary<int, List<int>> postTechs { get; private set; } = new Dictionary<int, List<int>>();
    public static Dictionary<int, List<TechFunctionLevelRangeValue>> techFunctionLevelDetails { get; private set; } = new Dictionary<int, List<TechFunctionLevelRangeValue>>();

    static void Initialize()
    {
      for (int i = 0; i < LDB.techs.dataArray.Length; i++)
      {
        var techProto = LDB.techs.dataArray[i];
        if (techProto.Published)
        {
          preTechs.Add(techProto.ID, new List<int>());
          postTechs.Add(techProto.ID, new List<int>());
          foreach (var function in techProto.UnlockFunctions)
          {
            if (!techFunctionLevelDetails.ContainsKey(function))
              techFunctionLevelDetails.Add(function, new List<TechFunctionLevelRangeValue>());
          }
        }
      }

      for (int i = 0; i < LDB.techs.dataArray.Length; i++)
      {
        var techProto = LDB.techs.dataArray[i];
        if (techProto.Published)
        {
          for (int j = 0; j < techProto.PreTechs.Length; j++)
          {
            preTechs[techProto.ID].Add(techProto.PreTechs[j]);
            postTechs[techProto.PreTechs[j]].Add(techProto.ID);
          }

          for (int j = 0; j < techProto.PreTechsImplicit.Length; j++)
          {
            preTechs[techProto.ID].Add(techProto.PreTechsImplicit[j]);
            postTechs[techProto.PreTechsImplicit[j]].Add(techProto.ID);
          }

          for (int j = 0; j < techProto.UnlockFunctions.Length; j++)
          {
            techFunctionLevelDetails[techProto.UnlockFunctions[j]].Add(
              new TechFunctionLevelRangeValue
              {
                minLevel = techProto.Level,
                maxLevel = techProto.MaxLevel,
                techId = techProto.ID,
                value = techProto.UnlockValues[j]
              }
            );
          }
        }
      }

      foreach (var function in techFunctionLevelDetails.Keys)
      {
        var detailsList = techFunctionLevelDetails[function];
        detailsList.Sort((x, y) => x.minLevel - y.minLevel);
      }

      initialized = true;
    }

    public static void LockTech(int techId)
    {
      var history = GameMain.history;
      foreach (var postTechId in postTechs[techId])
      {
        var postTechProto = LDB.techs.Select(postTechId);
        if (history.techStates[postTechId].unlocked || history.techStates[postTechId].curLevel > postTechProto.Level)
          LockTech(postTechId);
      }
      if (history.techStates.ContainsKey(techId))
      {
        var techState = history.techStates[techId];
        var techProto = LDB.techs.Select(techId);
        techState.curLevel = techProto.Level;
        techState.hashUploaded = 0L;
        techState.hashNeeded = techProto.GetHashNeeded(techState.curLevel);
        techState.unlocked = false;
        history.techStates[techId] = techState;
        if (techProto != null)
        {
          for (int i = 0; i < techProto.UnlockRecipes.Length; i++)
          {
            LockRecipe(techProto.UnlockRecipes[i]);
          }
          for (int j = 0; j < techProto.UnlockFunctions.Length; j++)
          {
            SetTechFunctionLevel(techProto.UnlockFunctions[j], techProto.Level - 1);
          }
        }
        UIRoot.instance.uiGame.replicator.OnTechUnlocked(techId, techState.curLevel);
        UIRoot.instance.uiGame.techTree.OnTechUnlocked(techId, techState.curLevel);
      }
    }

    public static void UnlockTech(int techId)
    {
      var history = GameMain.history;
      foreach (var preTechId in preTechs[techId])
      {
        if (!history.techStates[preTechId].unlocked)
          UnlockTech(preTechId);
      }
      if (history.techStates.ContainsKey(techId))
      {
        var techState = history.techStates[techId];
        SetTechLevelForUnlock(techId, techState.maxLevel);
      }
    }

    public static void UnlockTech(int techId, int level)
    {
      var history = GameMain.history;
      foreach (var pretechId in preTechs[techId])
      {
        if (!history.techStates[pretechId].unlocked)
          UnlockTech(pretechId);
      }
      if (history.techStates.ContainsKey(techId))
      {
        var techState = history.techStates[techId];
        SetTechLevelForUnlock(techId, level);
      }
    }

    static void SetTechLevelForUnlock(int techId, int level)
    {
      var history = GameMain.history;
      if (history.techStates.ContainsKey(techId))
      {
        var techState = history.techStates[techId];
        var techProto = LDB.techs.Select(techId);
        level = level < techProto.Level ? techProto.Level : level;
        if (level >= techState.maxLevel)
        {
          techState.curLevel = techState.maxLevel;
          techState.hashUploaded = techState.hashNeeded;
          techState.unlocked = true;
        }
        else
        {
          techState.curLevel = level;
          techState.hashUploaded = 0L;
          techState.hashNeeded = techProto.GetHashNeeded(techState.curLevel);
          techState.unlocked = false;
        }
        history.techStates[techId] = techState;
        if (techProto != null)
        {
          for (int i = 0; i < techProto.UnlockRecipes.Length; i++)
          {
            UnlockRecipe(techProto.UnlockRecipes[i]);
          }
          for (int j = 0; j < techProto.UnlockFunctions.Length; j++)
          {
            SetTechFunctionLevel(techProto.UnlockFunctions[j], techState.curLevel);
          }
        }
        UIRoot.instance.uiGame.replicator.OnTechUnlocked(techId, techState.curLevel);
        UIRoot.instance.uiGame.techTree.OnTechUnlocked(techId, techState.curLevel);
      }
    }

    static void LockRecipe(int recipeId)
    {
      GameMain.history.recipeUnlocked.Remove(recipeId);
    }

    static void UnlockRecipe(int recipeId)
    {
      GameMain.history.recipeUnlocked.Add(recipeId);
    }

    private static double SumLevelValues(double init, List<TechFunctionLevelRangeValue> levelDetails, int level)
    {
      var result = init;
      for (int i = 0; i < levelDetails.Count; i++)
      {
        var maxLevel = levelDetails[i].maxLevel;
        var minLevel = levelDetails[i].minLevel;
        if (level < minLevel)
          break;
        var times = (level > maxLevel ? maxLevel : level) - minLevel + 1;
        result += times * levelDetails[i].value;
      }
      return result;
    }

    private static int SumLevelValuesInt(int init, List<TechFunctionLevelRangeValue> levelDetails, int level)
    {
      var result = init;
      for (int i = 0; i < levelDetails.Count; i++)
      {
        var maxLevel = levelDetails[i].maxLevel;
        var minLevel = levelDetails[i].minLevel;
        if (level < minLevel)
          break;
        var times = (level > maxLevel ? maxLevel : level) - minLevel + 1;
        var value = levelDetails[i].value;
        var valueRounded = (int)((value > 0.0) ? (value + 0.5) : (value - 0.5));
        result += times * valueRounded;
      }
      return result;
    }

    private static double MulLevelValues(double init, List<TechFunctionLevelRangeValue> levelDetails, int level)
    {
      var result = init;
      for (int i = 0; i < levelDetails.Count; i++)
      {
        var maxLevel = levelDetails[i].maxLevel;
        var minLevel = levelDetails[i].minLevel;
        if (level < minLevel)
          break;
        var times = (level > maxLevel ? maxLevel : level) - minLevel + 1;
        for (int j = 0; j < times; j++)
          result *= levelDetails[i].value;
      }
      return result;
    }

    private static int MaxLevelValueInt(int init, List<TechFunctionLevelRangeValue> levelDetails, int level)
    {
      int maxLevelIdx = int.MinValue;
      for (int i = 0; i < levelDetails.Count; i++)
      {
        var maxLevel = levelDetails[i].maxLevel;
        var minLevel = levelDetails[i].minLevel;
        if (level < minLevel)
          break;
        maxLevelIdx = i;
      }
      if (maxLevelIdx < 0)
        return init;
      var resultRaw = levelDetails[maxLevelIdx].value;
      return (int)((resultRaw > 0.0) ? (resultRaw + 0.5) : (resultRaw - 0.5));
    }

    static void SetTechFunctionLevel(int func, int level)
    {
      var freeMode = Configs.freeMode;
      var levelDetails = techFunctionLevelDetails[func];

      var player = GameMain.mainPlayer;
      var mecha = player.mecha;
      var history = GameMain.history;

      switch (func)
      {
        case 1:
          mecha.droneCount = SumLevelValuesInt(freeMode.mechaDroneCount, levelDetails, level);
          break;
        case 2:
          mecha.reactorPowerGen = SumLevelValues(freeMode.mechaReactorPowerGen, levelDetails, level);
          break;
        case 3:
          mecha.walkSpeed = (float)SumLevelValues(freeMode.mechaWalkSpeed, levelDetails, level);
          break;
        case 4:
          mecha.thrusterLevel = MaxLevelValueInt(freeMode.mechaThrusterLevel, levelDetails, level);
          break;
        case 5:
          var funcValue = (int)SumLevelValues(0, levelDetails, level);
          player.package.SetSize(freeMode.playerPackageSize + funcValue * 10);
          break;
        case 6:
          mecha.coreEnergyCap = SumLevelValues(freeMode.mechaCoreEnergyCap, levelDetails, level);
          mecha.coreLevel = level;
          break;
        case 7:
          mecha.replicateSpeed = (float)SumLevelValues(freeMode.mechaReplicateSpeed, levelDetails, level);
          break;
        case 8:
          history.useIonLayer = level >= 0;
          break;
        case 9:
          mecha.droneMovement = SumLevelValuesInt(freeMode.mechaDroneMovement, levelDetails, level);
          break;
        case 10:
          mecha.droneSpeed = (float)SumLevelValues(freeMode.mechaDroneSpeed, levelDetails, level);
          break;
        case 11:
          mecha.maxSailSpeed = (float)SumLevelValues(freeMode.mechaSailSpeedMax, levelDetails, level);
          break;
        case 12:
          history.solarSailLife = (float)SumLevelValues(freeMode.solarSailLife, levelDetails, level);
          break;
        case 13:
          history.solarEnergyLossRate = (float)MulLevelValues(freeMode.solarEnergyLossRate, levelDetails, level);
          break;
        case 14:
          history.inserterStackCount = MaxLevelValueInt(freeMode.inserterStackCount, levelDetails, level);
          break;
        case 15:
          history.logisticDroneSpeedScale = (float)SumLevelValues(1f, levelDetails, level);
          break;
        case 16:
          history.logisticShipSpeedScale = (float)SumLevelValues(1f, levelDetails, level);
          break;
        case 17:
          history.logisticShipWarpDrive = level >= 4;
          break;
        case 18:
          history.logisticDroneCarries = SumLevelValuesInt(freeMode.logisticDroneCarries, levelDetails, level);
          break;
        case 19:
          history.logisticShipCarries = SumLevelValuesInt(freeMode.logisticShipCarries, levelDetails, level);
          break;
        case 20:
          history.miningCostRate = (float)MulLevelValues(freeMode.miningCostRate, levelDetails, level);
          break;
        case 21:
          history.miningSpeedScale = (float)SumLevelValues(freeMode.miningSpeedScale, levelDetails, level);
          history.miningSpeedScale = Mathf.Round(history.miningSpeedScale * 1000f) / 1000f;
          break;
        case 22:
          history.techSpeed = SumLevelValuesInt(freeMode.techSpeed, levelDetails, level);
          break;
        case 23:
          history.universeObserveLevel = MaxLevelValueInt(freeMode.universeObserveLevel, levelDetails, level);
          break;
        case 24:
          history.storageLevel = SumLevelValuesInt(2, levelDetails, level);
          break;
        case 25:
          history.labLevel = SumLevelValuesInt(3, levelDetails, level);
          break;
        case 26:
          history.dysonNodeLatitude = (float)SumLevelValues(0, levelDetails, level);
          break;
        case 27:
          var baseValue = freeMode.mechaWarpSpeedMax;
          var sumValue = SumLevelValues(0, levelDetails, level);
          mecha.maxWarpSpeed = baseValue + (float)(sumValue * 40000.0);
          break;
        case 28:
          history.blueprintLimit = MaxLevelValueInt(freeMode.blueprintLimit, levelDetails, level);
          break;
        case 99:
          history.missionAccomplished = level >= 0;
          break;
      }
    }

    public static void EnsureInitialized()
    {
      if (initialized)
        return;
      
      Initialize();
    }
  }
}