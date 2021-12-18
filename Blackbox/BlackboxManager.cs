using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace DysonSphereProgram.Modding.Blackbox
{
  public enum BlackboxStatus
  {
    Uninitialized,
    /* TO-BE-DEPRECATED */ Invalid,
    /* TO-BE-DEPRECATED */ Profiling,
    Analysing,
    AnalysisFailed,
    Blackboxed
  }

  public class BlackboxRecipe
  {
    public long idleEnergyPerTick;
    public long workingEnergyPerTick;
    public int timeSpend;
    // Dictionary<StationId, Dictionary<SlotId, CountPerCycle>>
    public Dictionary<int, Dictionary<int, int>> inputs;
    public Dictionary<int, Dictionary<int, int>> outputs;
    // Dictionary<ItemId, CountPerCycle>
    public Dictionary<int, int> produces;
    public Dictionary<int, int> consumes;
  }

  public class Blackbox
  {
    public readonly BlackboxRecipe Recipe;
    public readonly PlanetFactory Factory;
    public readonly int Id;

    public Blackbox(BlackboxRecipe recipe, PlanetFactory factory, int id)
    {
      Recipe = recipe;
      Factory = factory;
      Id = id;
    }

    public ICollection<int> entityIds;
    public int[] pcIds;
    public int[] assemblerIds;
    public int[] labIds;
    public int[] inserterIds;
    public int[] stationIds;
    public int[] cargoPathIds;
    public int[] splitterIds;

    Dictionary<int, Dictionary<int, int>> parsedInputs;
    Dictionary<int, Dictionary<int, int>> parsedOutputs;

    long[] idleEnergyRestore;
    long[] workEnergyRestore;

    int timeIdx = 0;
    bool isWorking = false;

    const bool continuousStats = true;

    public float CycleProgress { get { return timeIdx / (float)(Recipe.timeSpend - 1); } }
    
    public void BeginBlackboxing()
    {
      idleEnergyRestore = new long[pcIds.Length];
      workEnergyRestore = new long[pcIds.Length];

      for (int i = 0; i < pcIds.Length; i++)
      {
        idleEnergyRestore[i] = Factory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick;
        workEnergyRestore[i] = Factory.powerSystem.consumerPool[pcIds[i]].workEnergyPerTick;
        Factory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick = 0;
        Factory.powerSystem.consumerPool[pcIds[i]].workEnergyPerTick = 0;
        Factory.powerSystem.consumerPool[pcIds[i]].SetRequiredEnergy(false);
      }

      for (int i = 0; i < assemblerIds.Length; i++)
      {
        Factory.factorySystem.assemblerPool[assemblerIds[i]].id = -Id;
      }
      for (int i = 0; i < labIds.Length; i++)
      {
        Factory.factorySystem.labPool[labIds[i]].id = -Id;
      }
      for (int i = 0; i < inserterIds.Length; i++)
      {
        Factory.factorySystem.inserterPool[inserterIds[i]].id = -Id;
      }
      for (int i = 0; i < cargoPathIds.Length; i++)
      {
        var cargoPath = Factory.cargoTraffic.pathPool[cargoPathIds[i]];
        cargoPath.id = -Id;
        for (int j = 0; j < cargoPath.belts.Count; j++)
        {
          var beltId = cargoPath.belts[j];
          Factory.cargoTraffic.beltPool[beltId].id = -Id;
        }
      }
      for (int i = 0; i < splitterIds.Length; i++)
      {
        Factory.cargoTraffic.splitterPool[splitterIds[i]].id = -Id;
      }

      Factory.powerSystem.consumerPool[pcIds[0]].idleEnergyPerTick = Recipe.idleEnergyPerTick;
      Factory.powerSystem.consumerPool[pcIds[0]].workEnergyPerTick = Recipe.workingEnergyPerTick;

      parsedInputs = new Dictionary<int, Dictionary<int, int>>();
      parsedOutputs = new Dictionary<int, Dictionary<int, int>>();

      foreach (var station in Recipe.inputs)
      {
        var stationId = stationIds[station.Key];
        if (!parsedInputs.ContainsKey(stationId))
          parsedInputs[stationId] = new Dictionary<int, int>();
        var stationStorage = Factory.transport.stationPool[stationId].storage;
        foreach (var stationItemProduction in station.Value)
        {
          for (int j = 0; j < stationStorage.Length; j++)
            if (stationStorage[j].itemId == stationItemProduction.Key)
            {
              parsedInputs[stationId][j] = stationItemProduction.Value;
              break; 
            }
        }
      }

      foreach (var station in Recipe.outputs)
      {
        var stationId = stationIds[station.Key];
        if (!parsedOutputs.ContainsKey(stationId))
          parsedOutputs[stationId] = new Dictionary<int, int>();
        var stationStorage = Factory.transport.stationPool[stationId].storage;
        foreach (var stationItemRequirement in station.Value)
        {
          for (int j = 0; j < stationStorage.Length; j++)
            if (stationStorage[j].itemId == stationItemRequirement.Key)
            {
              parsedOutputs[stationId][j] = stationItemRequirement.Value;
              break;
            }
        }
      }

      isWorking = false;
      timeIdx = 0;
    }

    public void EndBlackboxing()
    {
      for (int i = 0; i < pcIds.Length; i++)
      {
        Factory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick = idleEnergyRestore[i];
        Factory.powerSystem.consumerPool[pcIds[i]].workEnergyPerTick = workEnergyRestore[i];
      }

      idleEnergyRestore = null;
      workEnergyRestore = null;

      for (int i = 0; i < assemblerIds.Length; i++)
      {
        Factory.factorySystem.assemblerPool[assemblerIds[i]].id = assemblerIds[i];
      }
      for (int i = 0; i < labIds.Length; i++)
      {
        Factory.factorySystem.labPool[labIds[i]].id = labIds[i];
      }
      for (int i = 0; i < inserterIds.Length; i++)
      {
        Factory.factorySystem.inserterPool[inserterIds[i]].id = inserterIds[i];
      }
      for (int i = 0; i < cargoPathIds.Length; i++)
      {
        var cargoPath = Factory.cargoTraffic.pathPool[cargoPathIds[i]];
        cargoPath.id = cargoPathIds[i];
        for (int j = 0; j < cargoPath.belts.Count; j++)
        {
          var beltId = cargoPath.belts[j];
          Factory.cargoTraffic.beltPool[beltId].id = beltId;
        }
      }
      for (int i = 0; i < splitterIds.Length; i++)
      {
        Factory.cargoTraffic.splitterPool[splitterIds[i]].id = splitterIds[i];
      }

      parsedInputs = null;
      parsedOutputs = null;

      isWorking = false;
      timeIdx = 0;
    }

    public void Simulate()
    {
      // Set the power consumption for the previous tick
      Factory.powerSystem.consumerPool[pcIds[0]].SetRequiredEnergy(isWorking);
      
      if (timeIdx == 0)
      {
        // Check if we can simulate a cycle. Else return and wait till we can.
        foreach (var station in parsedInputs)
        {
          foreach (var stationItemRequirement in station.Value)
          {
            if (Factory.transport.stationPool[station.Key].storage[stationItemRequirement.Key].count < stationItemRequirement.Value)
            {
              isWorking = false;
              return;
            }
          }
        }

        // Remove items and begin the cycle
        foreach (var station in parsedInputs)
        {
          foreach (var stationItemRequirement in station.Value)
          {
            Factory.transport.stationPool[station.Key].storage[stationItemRequirement.Key].count -= stationItemRequirement.Value;
          }
        }
      }

      isWorking = true;
      var totalTimeSpend = (float)Recipe.timeSpend;

      if (timeIdx == totalTimeSpend - 1)
      {
        // Check if stations can handle the outputs.
        // Else don't make any progress
        foreach (var station in parsedOutputs)
        {
          foreach (var stationItemProduction in station.Value)
          {
            var stationStorage = Factory.transport.stationPool[station.Key].storage[stationItemProduction.Key];
            if (stationStorage.max < stationStorage.count + stationItemProduction.Value)
            {
              isWorking = false;
              return;
            }
          }
        }

        foreach (var station in parsedOutputs)
        {
          foreach (var stationItemProduction in station.Value)
          {
            Factory.transport.stationPool[station.Key].storage[stationItemProduction.Key].count += stationItemProduction.Value;
          }
        }

        if (!continuousStats)
        {
          var factoryStatPool = GameMain.data.statistics.production.factoryStatPool[Factory.index];
          foreach (var production in Recipe.produces)
            factoryStatPool.productRegister[production.Key] += production.Value;
          foreach (var consumption in Recipe.consumes)
            factoryStatPool.consumeRegister[consumption.Key] += consumption.Value;
        }  
      }

      if (continuousStats)
      {
        var curPercent = timeIdx / totalTimeSpend;
        var nextPercent = (timeIdx + 1) / totalTimeSpend;
        var factoryStatPool = GameMain.data.statistics.production.factoryStatPool[Factory.index];

        foreach (var production in Recipe.produces)
        {
          var countToAdd = (int)(nextPercent * production.Value) - (int)(curPercent * production.Value);
          factoryStatPool.productRegister[production.Key] += countToAdd;
        }
        foreach (var consumption in Recipe.consumes)
        {
          var countToAdd = (int)(nextPercent * consumption.Value) - (int)(curPercent * consumption.Value);
          factoryStatPool.consumeRegister[consumption.Key] += countToAdd;
        }
      }

      timeIdx = (timeIdx + 1) % Recipe.timeSpend;
    }
  }

  public class BlackboxManager
  {
    public static BlackboxManager Instance { get; } = new BlackboxManager();
    private static int blackboxIdCounter = 1;

    public List<BlackboxAnalysis> analyses = new List<BlackboxAnalysis>();
    public List<Blackbox> blackboxes = new List<Blackbox>();

    public List<BlackboxAnalysis> toRemove = new List<BlackboxAnalysis>();

    public BlackboxAnalysis AddAnalysis(BlackboxAnalysis analysis) => AddAnalysis(analysis, out _);
    public BlackboxAnalysis AddAnalysis(BlackboxAnalysis analysis, out int id)
    {
      analyses.Add(analysis);
      lock (Instance)
      {
        id = blackboxIdCounter;
        blackboxIdCounter++;
      }
      analysis.id = id;
      analysis.Begin();
      return analysis;
    }

    public void MarkAnalysisForRemoval(BlackboxAnalysis analysis)
    {
      toRemove.Add(analysis);
    }

    public void RemoveFinishedAnalyses()
    {
      foreach (var analysis in toRemove)
        analyses.Remove(analysis);
      toRemove.Clear();
    }

    public void AddBlackbox(Blackbox blackbox)
    {
      blackboxes.Add(blackbox);
    }

    public void SimulateBlackboxes()
    {
      foreach (var blackbox in blackboxes)
        blackbox.Simulate();
    }

    public void ClearAll()
    {
      analyses.Clear();
      blackboxes.Clear();
      toRemove.Clear();
      blackboxIdCounter = 1;
    }
  }

  class BlackboxPatch
  {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.GameTick))]
    public static void GameStatData__GameTick()
    {
      BlackboxManager.Instance.RemoveFinishedAnalyses();
      BlackboxManager.Instance.SimulateBlackboxes();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.Destroy))]
    public static void GameData__Destroy()
    {
      BlackboxManager.Instance.ClearAll();
    }
  }
}