using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace DysonSphereProgram.Modding.Blackbox
{ 
  public class BlackboxSimulation
  {
    private Blackbox blackbox;
    private WeakReference<PlanetFactory> factoryRef;

    public BlackboxSimulation(Blackbox blackbox)
    {
      this.blackbox = blackbox;
      this.factoryRef = blackbox.FactoryRef;
    }

    Dictionary<int, Dictionary<int, int>> parsedInputs;
    Dictionary<int, Dictionary<int, int>> parsedOutputs;

    long[] idleEnergyRestore;
    long[] workEnergyRestore;
    long[] requiredEnergyRestore;

    int timeIdx = 0;
    public bool isBlackboxSimulating = false;
    bool isWorking = false;

    const bool continuousStats = true;

    public float CycleProgress => timeIdx / (float)(blackbox.Recipe.timeSpend - 1);
    public string CycleProgressText => $"{timeIdx} / {blackbox.Recipe.timeSpend}";

    public void CreateBlackboxingResources()
    {
      if (!factoryRef.TryGetTarget(out var factory))
      {
        Plugin.Log.LogError("PlanetFactory instance pulled out from under a blackbox simulation in " + nameof(CreateBlackboxingResources));
        return;
      }

      var pcCount = blackbox.Selection.pcIds.Count;
      idleEnergyRestore = new long[pcCount];
      workEnergyRestore = new long[pcCount];
      requiredEnergyRestore = new long[pcCount];

      parsedInputs = new Dictionary<int, Dictionary<int, int>>();
      parsedOutputs = new Dictionary<int, Dictionary<int, int>>();

      // WARNING: This will break once Fingerprint (and thereby Recipe) is selection-independent
      // TODO: Figure out a proper way to relate Selection, Fingerprint and Recipe
      var stationIds = blackbox.Selection.stationIds;

      foreach (var station in blackbox.Recipe.inputs)
      {
        
        var stationId = stationIds[station.Key];
        if (!parsedInputs.ContainsKey(stationId))
          parsedInputs[stationId] = new Dictionary<int, int>();
        var stationStorage = factory.transport.stationPool[stationId].storage;
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

      foreach (var station in blackbox.Recipe.outputs)
      {
        var stationId = stationIds[station.Key];
        if (!parsedOutputs.ContainsKey(stationId))
          parsedOutputs[stationId] = new Dictionary<int, int>();
        var stationStorage = factory.transport.stationPool[stationId].storage;
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

    public void ReleaseBlackboxingResources()
    {
      parsedInputs = null;
      parsedOutputs = null;

      idleEnergyRestore = null;
      workEnergyRestore = null;
      requiredEnergyRestore = null;
    }

    public void ResumeBlackboxing()
    {
      if (!factoryRef.TryGetTarget(out var factory))
      {
        Plugin.Log.LogError("PlanetFactory instance pulled out from under a blackbox simulation in " + nameof(ResumeBlackboxing));
        return;
      }

      var pcIds = blackbox.Selection.pcIds;
      var assemblerIds = blackbox.Selection.assemblerIds;
      var labIds = blackbox.Selection.labIds;
      var inserterIds = blackbox.Selection.inserterIds;
      var cargoPathIds = blackbox.Selection.cargoPathIds;
      var splitterIds = blackbox.Selection.splitterIds;

      for (int i = 0; i < pcIds.Count; i++)
      {
        idleEnergyRestore[i] = factory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick;
        workEnergyRestore[i] = factory.powerSystem.consumerPool[pcIds[i]].workEnergyPerTick;
        requiredEnergyRestore[i] = factory.powerSystem.consumerPool[pcIds[i]].requiredEnergy;
        factory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick = 0;
        factory.powerSystem.consumerPool[pcIds[i]].workEnergyPerTick = 0;
        factory.powerSystem.consumerPool[pcIds[i]].SetRequiredEnergy(false);
      }
      factory.powerSystem.consumerPool[pcIds[0]].idleEnergyPerTick = blackbox.Recipe.idleEnergyPerTick;
      factory.powerSystem.consumerPool[pcIds[0]].workEnergyPerTick = blackbox.Recipe.workingEnergyPerTick;

      for (int i = 0; i < assemblerIds.Count; i++)
      {
        factory.factorySystem.assemblerPool[assemblerIds[i]].id = -blackbox.Id;
      }
      for (int i = 0; i < labIds.Count; i++)
      {
        factory.factorySystem.labPool[labIds[i]].id = -blackbox.Id;
      }
      for (int i = 0; i < inserterIds.Count; i++)
      {
        factory.factorySystem.inserterPool[inserterIds[i]].id = -blackbox.Id;
      }
      for (int i = 0; i < cargoPathIds.Count; i++)
      {
        var cargoPath = factory.cargoTraffic.pathPool[cargoPathIds[i]];
        cargoPath.id = -blackbox.Id;
        for (int j = 0; j < cargoPath.belts.Count; j++)
        {
          var beltId = cargoPath.belts[j];
          factory.cargoTraffic.beltPool[beltId].id = -blackbox.Id;
        }
      }
      for (int i = 0; i < splitterIds.Count; i++)
      {
        factory.cargoTraffic.splitterPool[splitterIds[i]].id = -blackbox.Id;
      }

      isBlackboxSimulating = true;
    }

    public void PauseBlackboxing()
    {
      if (!factoryRef.TryGetTarget(out var factory))
      {
        Plugin.Log.LogError("PlanetFactory instance pulled out from under a blackbox simulation in " + nameof(PauseBlackboxing));
        return;
      }

      var pcIds = blackbox.Selection.pcIds;
      var assemblerIds = blackbox.Selection.assemblerIds;
      var labIds = blackbox.Selection.labIds;
      var inserterIds = blackbox.Selection.inserterIds;
      var cargoPathIds = blackbox.Selection.cargoPathIds;
      var splitterIds = blackbox.Selection.splitterIds;

      for (int i = 0; i < pcIds.Count; i++)
      {
        factory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick = idleEnergyRestore[i];
        factory.powerSystem.consumerPool[pcIds[i]].workEnergyPerTick = workEnergyRestore[i];
        factory.powerSystem.consumerPool[pcIds[i]].requiredEnergy = requiredEnergyRestore[i];
      }

      for (int i = 0; i < assemblerIds.Count; i++)
      {
        factory.factorySystem.assemblerPool[assemblerIds[i]].id = assemblerIds[i];
      }
      for (int i = 0; i < labIds.Count; i++)
      {
        factory.factorySystem.labPool[labIds[i]].id = labIds[i];
      }
      for (int i = 0; i < inserterIds.Count; i++)
      {
        factory.factorySystem.inserterPool[inserterIds[i]].id = inserterIds[i];
      }
      for (int i = 0; i < cargoPathIds.Count; i++)
      {
        var cargoPath = factory.cargoTraffic.pathPool[cargoPathIds[i]];
        cargoPath.id = cargoPathIds[i];
        for (int j = 0; j < cargoPath.belts.Count; j++)
        {
          var beltId = cargoPath.belts[j];
          factory.cargoTraffic.beltPool[beltId].id = beltId;
        }
      }
      for (int i = 0; i < splitterIds.Count; i++)
      {
        factory.cargoTraffic.splitterPool[splitterIds[i]].id = splitterIds[i];
      }

      isBlackboxSimulating = false;
    }

    void TakeBackUnusedItems()
    {
      if (!factoryRef.TryGetTarget(out var factory))
      {
        Plugin.Log.LogError("PlanetFactory instance pulled out from under a blackbox simulation in " + nameof(TakeBackUnusedItems));
        return;
      }

      var totalTimeSpend = (float)blackbox.Recipe.timeSpend;
      var curPercent = timeIdx / totalTimeSpend;

      // WARNING: This will break once Fingerprint (and thereby Recipe) is selection-independent
      // TODO: Figure out a proper way to relate Selection, Fingerprint and Recipe
      var stationIds = blackbox.Selection.stationIds;

      foreach (var station in blackbox.Recipe.inputs)
      {
        var stationId = stationIds[station.Key];
        var stationEntityId = factory.transport.stationPool[stationId].entityId;
        foreach (var stationItemRequirement in station.Value)
        {
          var itemId = stationItemRequirement.Key;
          var count = stationItemRequirement.Value;

          var consumedCount = (int)curPercent * count;
          var countToReturn = count - consumedCount;

          GameMain.mainPlayer.TryAddItemToPackage(itemId, countToReturn, true /*, stationEntityId */);
        }
      }

      foreach (var station in blackbox.Recipe.outputs)
      {
        var stationId = stationIds[station.Key];
        var stationEntityId = factory.transport.stationPool[stationId].entityId;
        foreach (var stationItemProduction in station.Value)
        {
          var itemId = stationItemProduction.Key;
          var count = stationItemProduction.Value;

          var producedCount = (int)curPercent * count;

          GameMain.mainPlayer.TryAddItemToPackage(itemId, producedCount, true /*, stationEntityId */);
        }
      }
    }

    public void BeginBlackboxing()
    {
      CreateBlackboxingResources();
      ResumeBlackboxing();
    }

    public void EndBlackboxing()
    {
      PauseBlackboxing();
      TakeBackUnusedItems();
      ReleaseBlackboxingResources();
    }

    public void Simulate()
    {
      if (!isBlackboxSimulating)
        return;

      if (!factoryRef.TryGetTarget(out var factory))
      {
        Plugin.Log.LogError("PlanetFactory instance pulled out from under a blackbox simulation in " + nameof(Simulate));
        return;
      }

      // Set the power consumption for the previous tick
      factory.powerSystem.consumerPool[blackbox.Selection.pcIds[0]].SetRequiredEnergy(isWorking);

      if (timeIdx == 0)
      {
        // Check if we can simulate a cycle. Else return and wait till we can.
        foreach (var station in parsedInputs)
        {
          foreach (var stationItemRequirement in station.Value)
          {
            if (factory.transport.stationPool[station.Key].storage[stationItemRequirement.Key].count < stationItemRequirement.Value)
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
            factory.transport.stationPool[station.Key].storage[stationItemRequirement.Key].count -= stationItemRequirement.Value;
          }
        }
      }

      isWorking = true;
      var Recipe = blackbox.Recipe;

      if (timeIdx == Recipe.timeSpend - 1)
      {
        // Check if stations can handle the outputs.
        // Else don't make any progress
        foreach (var station in parsedOutputs)
        {
          foreach (var stationItemProduction in station.Value)
          {
            var stationStorage = factory.transport.stationPool[station.Key].storage[stationItemProduction.Key];
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
            factory.transport.stationPool[station.Key].storage[stationItemProduction.Key].count += stationItemProduction.Value;
          }
        }

        if (!continuousStats)
        {
          var factoryStatPool = GameMain.data.statistics.production.factoryStatPool[factory.index];
          foreach (var production in Recipe.produces)
            factoryStatPool.productRegister[production.Key] += production.Value;
          foreach (var consumption in Recipe.consumes)
            factoryStatPool.consumeRegister[consumption.Key] += consumption.Value;
        }
      }

      if (continuousStats)
      {
        var totalTimeSpend = (float)Recipe.timeSpend;
        var curPercent = timeIdx / totalTimeSpend;
        var nextPercent = (timeIdx + 1) / totalTimeSpend;
        var factoryStatPool = GameMain.data.statistics.production.factoryStatPool[factory.index];

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

    const int saveLogicVersion = 1;

    public void PreserveVanillaSaveBefore()
    {
      var wasBlackboxSimulating = isBlackboxSimulating;
      PauseBlackboxing();
      isBlackboxSimulating = wasBlackboxSimulating;
    }

    public void PreserveVanillaSaveAfter()
    {
      if (isBlackboxSimulating)
      {
        ResumeBlackboxing();
      }
    }

    public void Export(BinaryWriter w)
    {
      w.Write(saveLogicVersion);
      w.Write(isBlackboxSimulating);
      w.Write(timeIdx);
      w.Write(isWorking);
    }

    public void Import(BinaryReader r)
    {
      var saveLogicVersion = r.ReadInt32();
      isBlackboxSimulating = r.ReadBoolean();
      timeIdx = r.ReadInt32();
      isWorking = r.ReadBoolean();
      PreserveVanillaSaveAfter();
    }
  }
}