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
  public class BlackboxBenchmarkV1: BlackboxBenchmark
  {
    static ERecipeType[] supportedRecipeTypes = new[]
    {
        ERecipeType.Smelt
      , ERecipeType.Assemble
      , ERecipeType.Refine
      , ERecipeType.Chemical
      , ERecipeType.Particle
    };

    int[] stationIds;
    int[] assemblerIds;
    int[] inserterIds;
    int[] pcIds;

    // const int MaxTimeWindowInTicks = 10 * 60 * 60;
    // const int MaxTimeWindowInTicks = 1 * 30 * 60;

    const int TicksPerSecond = 60;
    const int TicksPerMinute = TicksPerSecond * 60;

    const bool profileInserters = false;
    const bool logProfiledData = true;

    TimeSeriesData<int> profilingTsData;

    const int pcOffset = 0;
    int pcSize;
    int assemblerOffset;
    int[] assemblerOffsets;
    int assemblerSize;
    int stationOffset;
    int[] stationOffsets;
    int stationSize;
    int inserterOffset;
    int inserterSize;

    int perTickProfilingSize;
    int profilingTick = 0;

    const int analysisVerificationCount = 4;
    int timeSpendGCD;
    int timeSpendLCM;
    int profilingTickCount;
    int profilingEntryCount;
    int stabilizingTickCount;
    bool hasStabilized = false;
    int observedCycleLength;

    const int initialStabilizationWaitTicks = 30 * TicksPerSecond;

    BlackboxRecipe analysedRecipe;
    public override BlackboxRecipe EffectiveRecipe => analysedRecipe;

    public BlackboxBenchmarkV1(PlanetFactory factory, ICollection<int> entityIds) : base(factory, entityIds)
    {
    }

    class BlackboxBenchmarkV1Summarizer : ISummarizer<int>
    {
      public BlackboxBenchmarkV1 analysis;

      public void Initialize(Span<int> data)
      {
        data.Clear();
      }

      public void Summarize(Span<int> detailed, Span<int> summary)
      {
        var pcIds = analysis.pcIds.Length;
        var pcDetailed = MemoryMarshal.Cast<int, long>(detailed.Slice(pcOffset, pcIds * 2));
        var pcSummary = MemoryMarshal.Cast<int, long>(summary.Slice(pcOffset, pcIds * 2));
        for (int i = 0; i < pcSummary.Length; i++)
          pcSummary[i] += pcDetailed[i];

        var restDetailed = detailed.Slice(analysis.assemblerOffset);
        var restSummary = summary.Slice(analysis.assemblerOffset);
        for (int i = 0; i < restSummary.Length; i++)
          restSummary[i] += restDetailed[i];
      }
    }

    public override void Begin()
    {
      // TODO: Initialize analysis

      var tmp_stationIds = new List<int>();
      var tmp_assemblerIds = new List<int>();
      //var tmp_assemblerRecipeIds = new List<int>();
      var tmp_assemblerTimeSpends = new List<int>();
      var tmp_inserterIds = new List<int>();
      var tmp_pcIds = new List<int>();

      foreach (var entityId in entityIds)
      {
        var entity = factory.entityPool[entityId];
        if (entity.stationId > 0)
        {
          tmp_stationIds.Add(entity.stationId);
        }
        else if (entity.assemblerId > 0)
        {
          tmp_assemblerIds.Add(entity.assemblerId);
          var assembler = factory.factorySystem.assemblerPool[entity.assemblerId];
          tmp_assemblerTimeSpends.Add(assembler.timeSpend / assembler.speed);
          //tmp_assemblerRecipeIds.Add(factory.factorySystem.assemblerPool[entity.assemblerId].recipeId);
          tmp_pcIds.Add(entity.powerConId);
        }
        if (entity.inserterId > 0)
        {
          if (profileInserters)
          {
            tmp_inserterIds.Add(entity.inserterId);
          }
          tmp_pcIds.Add(entity.powerConId);
        }
      }

      this.pcIds = tmp_pcIds.ToArray();
      this.assemblerIds = tmp_assemblerIds.ToArray();
      this.assemblerOffsets = new int[assemblerIds.Length];
      this.stationIds = tmp_stationIds.ToArray();
      this.stationOffsets = new int[stationIds.Length];
      this.inserterIds = tmp_inserterIds.ToArray();

      this.pcSize = AnalysisData.size_powerConsumer * pcIds.Length;
      this.assemblerSize = 0;
      for (int i = 0; i < assemblerIds.Length; i++)
      {
        assemblerOffsets[i] = assemblerSize;
        var assembler = factory.factorySystem.assemblerPool[assemblerIds[i]];
        this.assemblerSize += assembler.served.Length + assembler.produced.Length;
      }
      this.stationSize = 0;
      for (int i = 0; i < stationIds.Length; i++)
      {
        stationOffsets[i] = stationSize;
        var station = factory.transport.stationPool[stationIds[i]];
        this.stationSize += station.storage.Length;
      }
      this.inserterSize = AnalysisData.size_inserter * inserterIds.Length;

      this.assemblerOffset = pcOffset + pcSize;
      this.stationOffset = assemblerOffset + assemblerSize;
      this.inserterOffset = stationOffset + stationSize;
      this.perTickProfilingSize = pcSize + assemblerSize + stationSize + inserterSize;

      //var distinctRecipes = tmp_assemblerRecipeIds.Distinct().Select(recipeId => LDB.recipes.Select(recipeId));
      //var distinctRecipeTimeSpends = distinctRecipes.Select(recipe => recipe.TimeSpend).Distinct();
      var distinctTimeSpends = tmp_assemblerTimeSpends.Distinct();
      this.timeSpendGCD = Utils.GCD(distinctTimeSpends);
      this.timeSpendLCM = Utils.LCM(distinctTimeSpends);
      this.profilingTickCount = timeSpendLCM * ((analysisVerificationCount * 2) + 2);
      this.profilingEntryCount = profilingTickCount / timeSpendGCD;
      this.stabilizingTickCount = (4 * profilingTickCount) + initialStabilizationWaitTicks;

      var mlg = new MultiLevelGranularity();
      mlg.levels = 2;
      //mlg.entryCounts = new[] { 600, 600 };
      //mlg.ratios = new[] { 60 };
      mlg.entryCounts = new[] { timeSpendGCD, this.profilingEntryCount };
      mlg.ratios = new[] { timeSpendGCD };

      var summarizer = new BlackboxBenchmarkV1Summarizer() { analysis = this };

      profilingTsData = new TimeSeriesData<int>(this.perTickProfilingSize, mlg, summarizer);
      profilingTick = 0;

      status = BlackboxStatus.Profiling;
      this.hasStabilized = true;
    }

    private void DumpAnalysisToFile()
    {
      using (var f = new FileStream($@"D:\Raptor\Workspace\Personal\Projects\DSPMods\Blackbox\DataAnalysis\BenchmarkV1_{this.id}.txt", FileMode.Create))
      {
        using (var s = new StreamWriter(f))
        {
          var dataSize = profilingTsData.DataSize;
          s.WriteLine(dataSize);
          var data = profilingTsData.Data;
          var entries = data.Length / dataSize;
          var cursor = 0;
          for (int i = 0; i < entries; i++)
          {
            var pcs = MemoryMarshal.Cast<int, long>(new Span<int>(data, i * dataSize, pcSize));
            foreach (var pc in pcs)
            {
              s.Write(pc);
              s.Write(" ");
            }
            cursor += pcSize;

            for (int j = pcSize; j < dataSize; j++)
            {
              s.Write(data[cursor++]);
              s.Write(" ");
            }
            s.WriteLine();
          }
        }
      }
    }

    private void EndGameTick_Profiling()
    {
      profilingTsData.SummarizeAtHigherGranularity(profilingTick);
      profilingTick += 1;

      if (profilingTick % timeSpendGCD == 0)
      {
        Debug.Log("Profiling Tick: " + profilingTick);
      }
      if (!hasStabilized && profilingTick > initialStabilizationWaitTicks)
      {
        hasStabilized = true;
        Debug.Log("Assuming the blackbox has stabilized. Will begin to try detect the shortest cycle.");
      }
      if (profilingTick >= (2 * timeSpendGCD) && profilingTick % timeSpendGCD == 0)
      {
        var endIndex = (profilingTick / timeSpendGCD) - 1;
        var circularOffset = 0;
        if (endIndex > profilingEntryCount)
        {
          circularOffset = (endIndex - profilingEntryCount) % profilingEntryCount;
          endIndex = profilingEntryCount - 1;
        }

        var indexEquals = new Func<int, int, bool>((int i1, int i2) =>
        {
          var span1 = profilingTsData.Level(1).Entry((i1 + circularOffset) % profilingEntryCount);
          var span2 = profilingTsData.Level(1).Entry((i2 + circularOffset) % profilingEntryCount);

          for (int i = 0; i < span2.Length; i++)
            if (span1[i] != span2[i])
              return false;
          return true;
        });
        
        if (hasStabilized)
        {
          if (CycleDetection.TryDetectCycles(endIndex, 0, analysisVerificationCount, indexEquals, out int cycleLength))
          {
            this.observedCycleLength = cycleLength * timeSpendGCD;
            Debug.Log($"Cycle Length of {this.observedCycleLength} detected");
            if (logProfiledData)
            {
              DumpAnalysisToFile();
            }
            this.GenerateRecipe(endIndex, circularOffset, cycleLength);
            this.status = BlackboxStatus.Analysing;
          }
        }
      }
      if (profilingTick >= this.stabilizingTickCount + this.profilingTickCount)
      {
        this.status = BlackboxStatus.AnalysisFailed;
        profilingTick = 0;

        Debug.Log($"Analysis Failed");
        if (logProfiledData)
        {
          DumpAnalysisToFile();
        }
      }
    }

    public override void EndGameTick()
    {
      if (this.status == BlackboxStatus.Profiling)
      {
        EndGameTick_Profiling();
      }
    }

    void GenerateRecipe(int endIndex, int circularOffset, int cycleLength)
    {
      long idleEnergyPerTick = 0;
      for (int i = 0; i < pcIds.Length; i++)
        idleEnergyPerTick += factory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick;

      long idleEnergyPerCycle = idleEnergyPerTick * this.observedCycleLength;

      long workingEnergyPerCycle = 0;

      var summarizer = new BlackboxBenchmarkV1Summarizer() { analysis = this };

      int[] dataPerCycle = new int[perTickProfilingSize];
      var dataPerCycleSpan = new Span<int>(dataPerCycle);
      summarizer.Initialize(dataPerCycleSpan);

      for (int entryIdx = endIndex; entryIdx > endIndex - cycleLength; entryIdx--)
      {
        var entry = profilingTsData.Level(1).Entry((entryIdx + circularOffset) % profilingEntryCount);
        summarizer.Summarize(entry, dataPerCycleSpan);
      }

      var pcData = MemoryMarshal.Cast<int, long>(dataPerCycleSpan.Slice(pcOffset, assemblerOffset));
      foreach (var pc in pcData)
        workingEnergyPerCycle += pc;

      var tmp_stationStorageExit = new Dictionary<int, Dictionary<int, int>>();
      var tmp_stationStorageEnter = new Dictionary<int, Dictionary<int, int>>();
      var stationData = dataPerCycleSpan.Slice(stationOffset, stationSize);
      var curStationOffset = 0;
      for (int i = 0; i < stationIds.Length; i++)
      {
        var station = factory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
        {
          var stationStorage = station.storage[j];
          var itemId = stationStorage.itemId;
          //var effectiveLogic = stationStorage.remoteLogic == ELogisticStorage.None ? stationStorage.localLogic : stationStorage.remoteLogic;
          if (itemId > 0 && stationData[curStationOffset] != 0)
          {
            if (stationData[curStationOffset] > 0)
            {
              if (!tmp_stationStorageExit.ContainsKey(i))
                tmp_stationStorageExit[i] = new Dictionary<int, int>();
              tmp_stationStorageExit[i][itemId] = stationData[curStationOffset];
            }
            else
            {
              if (!tmp_stationStorageEnter.ContainsKey(i))
                tmp_stationStorageEnter[i] = new Dictionary<int, int>();
              tmp_stationStorageEnter[i][itemId] = -stationData[curStationOffset];
            }
          }
          curStationOffset++;
        }
      }

      var tmp_produces = new Dictionary<int, int>();
      var tmp_consumes = new Dictionary<int, int>();

      var assemblerData = dataPerCycleSpan.Slice(assemblerOffset, assemblerSize);
      var curAssemblerOffset = 0;
      for (int i = 0; i < assemblerIds.Length; i++)
      {
        var assembler = factory.factorySystem.assemblerPool[assemblerIds[i]];

        for (int j = 0; j < assembler.served.Length; j++)
        {
          var itemId = assembler.requires[j];
          if (!tmp_consumes.ContainsKey(itemId))
            tmp_consumes[itemId] = 0;
          tmp_consumes[itemId] += assemblerData[curAssemblerOffset++];
        }
        for (int j = 0; j < assembler.produced.Length; j++)
        {
          var itemId = assembler.products[j];
          if (!tmp_produces.ContainsKey(itemId))
            tmp_produces[itemId] = 0;
          tmp_produces[itemId] += -assemblerData[curAssemblerOffset++];
        }
      }

      Debug.Log($"Idle Energy per cycle: {idleEnergyPerCycle}");
      Debug.Log($"Working Energy per cycle: {workingEnergyPerCycle}");
      Debug.Log($"Idle Power: {(idleEnergyPerCycle / this.observedCycleLength) * TicksPerSecond}");
      Debug.Log($"Working Power: {(workingEnergyPerCycle / this.observedCycleLength) * TicksPerSecond}");

      Debug.Log("Consumed");
      foreach (var item in tmp_consumes)
      {
        var itemName = LDB.ItemName(item.Key);
        Debug.Log($"  {item.Value} {itemName}");
      }

      Debug.Log("Produced");
      foreach (var item in tmp_produces)
      {
        var itemName = LDB.ItemName(item.Key);
        Debug.Log($"  {item.Value} {itemName}");
      }

      Debug.Log("Inputs");
      foreach (var stationIdx in tmp_stationStorageExit)
      {
        Debug.Log($"  Station #{stationIdx.Key}:");
        foreach (var itemId in stationIdx.Value)
        {
          var itemName = LDB.ItemName(itemId.Key);
          Debug.Log($"    {itemId.Value} {itemName}");
        }
      }

      Debug.Log("Outputs");
      foreach (var stationIdx in tmp_stationStorageEnter)
      {
        Debug.Log($"  Station #{stationIdx.Key}:");
        foreach (var itemId in stationIdx.Value)
        {
          var itemName = LDB.ItemName(itemId.Key);
          Debug.Log($"    {itemId.Value} {itemName}");
        }
      }

      Debug.Log($"Time (in ticks): {this.observedCycleLength}");
      Debug.Log($"Time (in seconds): {this.observedCycleLength / (float)TicksPerSecond}");
    }

    public override void LogPowerConsumer()
    {
      var profilingData = MemoryMarshal.Cast<int, long>(profilingTsData.LevelEntryOffset(0, profilingTick).Slice(pcOffset, assemblerOffset));
      for (int i = 0; i < pcIds.Length; i++)
      {
        var consumer = factory.powerSystem.consumerPool[pcIds[i]];
        profilingData[i] = consumer.requiredEnergy;
      }
    }

    public override void LogAssemblerBefore()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(assemblerOffset, assemblerSize);
      var curAssemblerOffset = 0;
      for (int i = 0; i < assemblerIds.Length; i++)
      {
        var assembler = factory.factorySystem.assemblerPool[assemblerIds[i]];
        
        for (int j = 0; j < assembler.served.Length; j++)
          profilingData[curAssemblerOffset++] = assembler.served[j];
        for (int j = 0; j < assembler.produced.Length; j++)
          profilingData[curAssemblerOffset++] = assembler.produced[j];
      }
    }

    public override void LogAssemblerAfter()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(assemblerOffset, assemblerSize);
      var curAssemblerOffset = 0;
      for (int i = 0; i < assemblerIds.Length; i++)
      {
        var assembler = factory.factorySystem.assemblerPool[assemblerIds[i]];

        for (int j = 0; j < assembler.served.Length; j++)
          profilingData[curAssemblerOffset++] -= assembler.served[j];
        for (int j = 0; j < assembler.produced.Length; j++)
          profilingData[curAssemblerOffset++] -= assembler.produced[j];
      }
    }

    public override void LogStationBefore()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(stationOffset, stationSize);
      var curStationOffset = 0;
      for (int i = 0; i < stationIds.Length; i++)
      {
        var station = factory.transport.stationPool[stationIds[i]];

        for (int j = 0; j < station.storage.Length; j++)
        {
          station.storage[j].count = station.storage[j].max / 2;
          profilingData[curStationOffset++] = station.storage[j].count;
        }
      }
    }

    public override void LogStationAfter()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(stationOffset, stationSize);
      var curStationOffset = 0;
      for (int i = 0; i < stationIds.Length; i++)
      {
        var station = factory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
          profilingData[curStationOffset++] -= station.storage[j].count;
      }
    }

    public override void LogInserter()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(inserterOffset, inserterSize);
      var curInserterOffset = 0;
      for (int i = 0; i < inserterIds.Length; i++)
      {
        var inserter = factory.factorySystem.inserterPool[inserterIds[i]];
        //profilingData[curInserterOffset + AnalysisData.fo_itemId] = inserter.itemId;
        profilingData[curInserterOffset + AnalysisData.fo_stackCount] = inserter.stackCount;
        profilingData[curInserterOffset + AnalysisData.fo_stage] = (int)inserter.stage;
        curInserterOffset += AnalysisData.size_inserter;
      }
    }
  }
}