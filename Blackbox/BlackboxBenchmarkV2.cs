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
  [StructLayout(LayoutKind.Sequential)]
  public struct ProduceConsumePair
  {
    public int Produced;
    public int Consumed;
  }

  public class BlackboxBenchmarkV2: BlackboxBenchmark
  {
    int[] stationIds;
    int[] assemblerIds;
    int[] inserterIds;
    int[] labIds;
    int[] pcIds;
    int[] cargoPathIds;
    int[] splitterIds;

    const int TicksPerSecond = 60;
    const int TicksPerMinute = TicksPerSecond * 60;

    const bool profileInserters = false;
    const bool logProfiledData = false;
    const bool analyzeInserterStackEffect = false;
    const bool forceNoStacking = false;
    
    const bool continuousLogging = true;
    StreamWriter continuousLogger;


    // Dictionary<ItemId, Index>
    Dictionary<int, int> itemIndices;

    ProduceConsumePair[] totalStats;
    ISummarizer<int> summarizer;
    int[] cycleDetectionData;
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
    int labOffset;
    int labSize;
    int statsOffset;
    int statsSize;
    int statsDiffOffset;
    int statsDiffSize;

    int perTickProfilingSize;
    int profilingTick = 0;
    int stabilizedTick = -1;

    const int analysisVerificationCount = 4;
    int timeSpendGCD;
    int timeSpendLCM;
    int profilingTickCount;
    int profilingEntryCount;
    int observedCycleLength;

    BlackboxRecipe analysedRecipe;
    public override BlackboxRecipe EffectiveRecipe => analysedRecipe;

    public BlackboxBenchmarkV2(PlanetFactory factory, ICollection<int> entityIds) : base(factory, entityIds)
    {
    }

    class BlackboxBenchmarkV2Summarizer : ISummarizer<int>
    {
      public BlackboxBenchmarkV2 analysis;

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

        var restDetailed = detailed.Slice(analysis.assemblerOffset, analysis.assemblerSize + analysis.stationSize + analysis.inserterSize + analysis.labSize + analysis.statsSize);
        var restSummary = summary.Slice(analysis.assemblerOffset, analysis.assemblerSize + analysis.stationSize + analysis.inserterSize + analysis.labSize + analysis.statsSize);
        for (int i = 0; i < restSummary.Length; i++)
          restSummary[i] += restDetailed[i];

        var statsDiffDetailed = detailed.Slice(analysis.statsDiffOffset, analysis.statsDiffSize);
        var statsDiffSummary = summary.Slice(analysis.statsDiffOffset, analysis.statsDiffSize);
        for (int i = 0; i < statsDiffSummary.Length; i++)
          statsDiffSummary[i] = statsDiffDetailed[i];
      }
    }

    public override void Begin()
    {
      var tmp_stationIds = new List<int>();
      var tmp_assemblerIds = new List<int>();
      var tmp_assemblerTimeSpends = new List<int>();
      var tmp_inserterIds = new List<int>();
      var tmp_labIds = new List<int>();
      var tmp_pcIds = new List<int>();
      var tmp_itemIds = new HashSet<int>();
      var tmp_cargoPathIds = new HashSet<int>();
      var tmp_splitterIds = new List<int>();

      foreach (var entityId in entityIds)
      {
        var entity = factory.entityPool[entityId];
        if (entity.stationId > 0)
        {
          tmp_stationIds.Add(entity.stationId);
          var station = factory.transport.stationPool[entity.stationId];
          for (int j = 0; j < station.storage.Length; j++)
          {
            var stationStorage = station.storage[j];
            var effectiveLogic = stationStorage.remoteLogic == ELogisticStorage.None ? stationStorage.localLogic : stationStorage.remoteLogic;
            if (effectiveLogic != ELogisticStorage.None && stationStorage.itemId > 0)
              tmp_itemIds.Add(stationStorage.itemId);
          }
        }
        if (entity.assemblerId > 0)
        {
          tmp_assemblerIds.Add(entity.assemblerId);
          var assembler = factory.factorySystem.assemblerPool[entity.assemblerId];
          tmp_assemblerTimeSpends.Add(assembler.timeSpend / assembler.speed);
          for (int i = 0; i < assembler.requires.Length; i++)
            tmp_itemIds.Add(assembler.requires[i]);
          for (int i = 0; i < assembler.products.Length; i++)
            tmp_itemIds.Add(assembler.products[i]);
          tmp_pcIds.Add(entity.powerConId);
        }
        if (entity.inserterId > 0)
        {
          tmp_inserterIds.Add(entity.inserterId);
          if (analyzeInserterStackEffect)
          {
            factory.factorySystem.inserterPool[entity.inserterId].canStack = false;
            var stackSize = 6 - id;
            factory.factorySystem.inserterPool[entity.inserterId].stackSize = stackSize;
            factory.factorySystem.inserterPool[entity.inserterId].delay = stackSize > 1 ? 110000 : 0;
          }
          if (forceNoStacking)
          {
            factory.factorySystem.inserterPool[entity.inserterId].canStack = false;
            factory.factorySystem.inserterPool[entity.inserterId].stackSize = 1;
            factory.factorySystem.inserterPool[entity.inserterId].delay = 0;
          }
          tmp_pcIds.Add(entity.powerConId);
        }
        if (entity.labId > 0)
        {
          var lab = factory.factorySystem.labPool[entity.labId];
          if (lab.matrixMode)
          {
            tmp_labIds.Add(entity.labId);
            tmp_assemblerTimeSpends.Add(lab.timeSpend / 10000);
            for (int i = 0; i < lab.requires.Length; i++)
              tmp_itemIds.Add(lab.requires[i]);
            for (int i = 0; i < lab.products.Length; i++)
              tmp_itemIds.Add(lab.products[i]);
            tmp_pcIds.Add(entity.powerConId);
          }
        }
        if (entity.beltId > 0)
        {
          var belt = factory.cargoTraffic.beltPool[entity.beltId];
          tmp_cargoPathIds.Add(belt.segPathId);
        }
        if (entity.splitterId > 0)
        {
          tmp_splitterIds.Add(entity.splitterId);
        }
      }

      this.pcIds = tmp_pcIds.ToArray();
      this.assemblerIds = tmp_assemblerIds.ToArray();
      this.assemblerOffsets = new int[assemblerIds.Length];
      this.stationIds = tmp_stationIds.ToArray();
      this.stationOffsets = new int[stationIds.Length];
      this.inserterIds = tmp_inserterIds.ToArray();
      this.labIds = tmp_labIds.ToArray();
      var tmp_itemIds_list = tmp_itemIds.ToList();
      this.itemIndices = tmp_itemIds.ToDictionary(x => x, x => tmp_itemIds_list.IndexOf(x));
      this.cargoPathIds = tmp_cargoPathIds.ToArray();
      this.splitterIds = tmp_splitterIds.ToArray();

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
      this.inserterSize = profileInserters ? AnalysisData.size_inserter * inserterIds.Length : 0;
      this.labSize = 0;
      for (int i = 0; i < labIds.Length; i++)
      {
        //assemblerOffsets[i] = assemblerSize;
        var lab = factory.factorySystem.labPool[labIds[i]];
        this.labSize += lab.served.Length + lab.produced.Length;
      }
      this.statsSize = itemIndices.Count * 2;
      this.statsDiffSize = itemIndices.Count;

      this.assemblerOffset = pcOffset + pcSize;
      this.stationOffset = assemblerOffset + assemblerSize;
      this.inserterOffset = stationOffset + stationSize;
      this.labOffset = inserterOffset + inserterSize;
      this.statsOffset = labOffset + labSize;
      this.statsDiffOffset = statsOffset + statsSize;
      this.perTickProfilingSize = pcSize + assemblerSize + stationSize + inserterSize + labSize + statsSize + statsDiffSize;

      this.totalStats = new ProduceConsumePair[itemIndices.Count];

      var distinctTimeSpends = tmp_assemblerTimeSpends.Distinct().DefaultIfEmpty(60);
      this.timeSpendGCD = Utils.GCD(distinctTimeSpends);
      this.timeSpendLCM = Utils.LCM(distinctTimeSpends);
      this.profilingTickCount = timeSpendLCM * ((analysisVerificationCount * 2 * /* to account for sorter stacking */ 6) + 2);
      this.profilingEntryCount = profilingTickCount / timeSpendGCD;

      var mlg = new MultiLevelGranularity();
      mlg.levels = 2;
      mlg.entryCounts = new[] { timeSpendGCD, this.profilingEntryCount };
      mlg.ratios = new[] { timeSpendGCD };

      this.summarizer = new BlackboxBenchmarkV2Summarizer() { analysis = this };

      profilingTsData = new TimeSeriesData<int>(this.perTickProfilingSize, mlg, summarizer);
      this.cycleDetectionData = new int[this.perTickProfilingSize * 2];
      profilingTick = 0;

      if (continuousLogging)
      {
        continuousLogger = new StreamWriter($@"D:\Raptor\Workspace\Personal\Projects\DSPMods\Blackbox\DataAnalysis\BenchmarkV2_CL_{this.id}.csv");

        WriteContinuousLoggingHeader();
      }

      status = BlackboxStatus.Profiling;
    }

    private void DumpAnalysisToFile()
    {
      using (var f = new FileStream($@"D:\Raptor\Workspace\Personal\Projects\DSPMods\Blackbox\DataAnalysis\BenchmarkV2_{this.id}.txt", FileMode.Create))
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
          s.WriteLine();
          foreach (var kvp in itemIndices)
          {
            var itemName = LDB.ItemName(kvp.Key);
            s.WriteLine(itemName);
            var stats = totalStats[kvp.Value];
            s.WriteLine($"  Produced: {stats.Produced}");
            s.WriteLine($"  Consumed: {stats.Consumed}");
            s.WriteLine($"  Difference: {stats.Produced - stats.Consumed}");
          }
        }
      }
    }

    private void WriteContinuousLoggingHeader()
    {
      for (int i = 0; i < pcIds.Length; i++)
        continuousLogger.Write($"PC{i},");

      for (int i = 0; i < assemblerIds.Length; i++)
      {
        var assembler = factory.factorySystem.assemblerPool[assemblerIds[i]];

        for (int j = 0; j < assembler.served.Length; j++)
        {
          continuousLogger.Write($"A{i}_I{j},");
        }
        for (int j = 0; j < assembler.produced.Length; j++)
        {
          continuousLogger.Write($"A{i}_O{j},");
        }
      }

      for (int i = 0; i < stationIds.Length; i++)
      {
        var station = factory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
        {
          continuousLogger.Write($"S{i}_{j},");
        }
      }

      if (profileInserters)
      {
        for (int i = 0; i < inserterIds.Length; i++)
        {
          continuousLogger.Write($"I{i}_C,");
          continuousLogger.Write($"I{i}_S,");
          continuousLogger.Write($"I{i}_I,");
        }
      }

      for (int i = 0; i < labIds.Length; i++)
      {
        var lab = factory.factorySystem.labPool[labIds[i]];

        for (int j = 0; j < lab.served.Length; j++)
        {
          continuousLogger.Write($"L{i}_I{j},");
        }
        for (int j = 0; j < lab.produced.Length; j++)
        {
          continuousLogger.Write($"L{i}_O{j},");
        }
      }

      for (int i = 0; i < itemIndices.Count; i++)
      {
        var itemId = itemIndices.First(x => x.Value == i).Key;
        var itemName = LDB.ItemName(itemId).Replace(" ", "").Trim();
        continuousLogger.Write($"P_{itemName},");
        continuousLogger.Write($"C_{itemName},");
        continuousLogger.Write($"T_P_{itemName},");
        continuousLogger.Write($"T_C_{itemName},");
        continuousLogger.Write($"T_D_{itemName},");
      }
      continuousLogger.WriteLine($"EOL");
    }

    private void WriteContinuousLoggingData(int level)
    {
      var entry = profilingTsData.LevelEntryOffset(level, profilingTick);

      var pcData = MemoryMarshal.Cast<int, long>(entry.Slice(pcOffset, pcSize));
      for (int i = 0; i < pcIds.Length; i++)
      {
        continuousLogger.Write(pcData[i]);
        continuousLogger.Write(',');
      }

      var assemblerData = entry.Slice(assemblerOffset, assemblerSize);
      var curAssemblerOffset = 0;
      for (int i = 0; i < assemblerIds.Length; i++)
      {
        var assembler = factory.factorySystem.assemblerPool[assemblerIds[i]];

        for (int j = 0; j < assembler.served.Length; j++)
        {
          continuousLogger.Write(assemblerData[curAssemblerOffset]);
          continuousLogger.Write(',');
          curAssemblerOffset++;
        }
        for (int j = 0; j < assembler.produced.Length; j++)
        {
          continuousLogger.Write(assemblerData[curAssemblerOffset]);
          continuousLogger.Write(',');
          curAssemblerOffset++;
        }
      }

      var stationData = entry.Slice(stationOffset, stationSize);
      var curStationOffset = 0;
      for (int i = 0; i < stationIds.Length; i++)
      {
        var station = factory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
        {
          continuousLogger.Write(stationData[curStationOffset]);
          continuousLogger.Write(',');
          curStationOffset++;
        }
      }

      if (profileInserters)
      {
        var inserterData = entry.Slice(inserterOffset, inserterSize);
        var curInserterOffset = 0;
        for (int i = 0; i < inserterIds.Length; i++)
        {
          continuousLogger.Write(inserterData[curInserterOffset + AnalysisData.fo_stackCount]);
          continuousLogger.Write(',');
          continuousLogger.Write((EInserterStage)inserterData[curInserterOffset + AnalysisData.fo_stage]);
          continuousLogger.Write(',');
          continuousLogger.Write(inserterData[curInserterOffset + AnalysisData.fo_idleTick]);
          continuousLogger.Write(',');
          curInserterOffset += AnalysisData.size_inserter;
        }
      }

      var labData = entry.Slice(labOffset, labSize);
      var curLabOffset = 0;
      for (int i = 0; i < labIds.Length; i++)
      {
        var lab = factory.factorySystem.labPool[labIds[i]];

        for (int j = 0; j < lab.served.Length; j++)
        {
          continuousLogger.Write(labData[curLabOffset]);
          continuousLogger.Write(',');
          curLabOffset++;
        }
        for (int j = 0; j < lab.produced.Length; j++)
        {
          continuousLogger.Write(labData[curLabOffset]);
          continuousLogger.Write(',');
          curLabOffset++;
        }
      }

      var statsData = MemoryMarshal.Cast<int, ProduceConsumePair>(entry.Slice(statsOffset, statsSize));
      for (int i = 0; i < itemIndices.Count; i++)
      {
        continuousLogger.Write(statsData[i].Produced);
        continuousLogger.Write(',');
        continuousLogger.Write(statsData[i].Consumed);
        continuousLogger.Write(',');
        continuousLogger.Write(totalStats[i].Produced);
        continuousLogger.Write(',');
        continuousLogger.Write(totalStats[i].Consumed);
        continuousLogger.Write(',');
        continuousLogger.Write(totalStats[i].Produced - totalStats[i].Consumed);
        continuousLogger.Write(',');
      }
      continuousLogger.WriteLine(0);
    }

    private void LogItemStats()
    {
      var levelEntrySpan = profilingTsData.LevelEntryOffset(0, profilingTick);

      var statsData = MemoryMarshal.Cast<int, ProduceConsumePair>(levelEntrySpan.Slice(statsOffset, statsSize));

      var assemblerData = levelEntrySpan.Slice(assemblerOffset, assemblerSize);
      var curAssemblerOffset = 0;
      for (int i = 0; i < assemblerIds.Length; i++)
      {
        var assembler = factory.factorySystem.assemblerPool[assemblerIds[i]];
        for (int j = 0; j < assembler.served.Length; j++)
        {
          var itemIdx = itemIndices[assembler.requires[j]];
          statsData[itemIdx].Consumed += assemblerData[curAssemblerOffset];
          curAssemblerOffset++;
        }
        for (int j = 0; j < assembler.produced.Length; j++)
        {
          var itemIdx = itemIndices[assembler.products[j]];
          statsData[itemIdx].Produced += -assemblerData[curAssemblerOffset];
          curAssemblerOffset++;
        }
      }

      var stationData = levelEntrySpan.Slice(stationOffset, stationSize);
      var curStationOffset = 0;
      for (int i = 0; i < stationIds.Length; i++)
      {
        var station = factory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
        {
          var stationStorage = station.storage[j];
          var effectiveLogic = stationStorage.remoteLogic == ELogisticStorage.None ? stationStorage.localLogic : stationStorage.remoteLogic;
          if (effectiveLogic != ELogisticStorage.None && stationStorage.itemId > 0)
          {
            var itemIdx = itemIndices[stationStorage.itemId];
            if (effectiveLogic == ELogisticStorage.Supply)
              statsData[itemIdx].Consumed += -stationData[curStationOffset];
            if (effectiveLogic == ELogisticStorage.Demand)
              statsData[itemIdx].Produced += stationData[curStationOffset];
          }
          curStationOffset++;
        }
      }

      var labData = levelEntrySpan.Slice(labOffset, labSize);
      var curLabOffset = 0;
      for (int i = 0; i < labIds.Length; i++)
      {
        var lab = factory.factorySystem.labPool[labIds[i]];
        for (int j = 0; j < lab.served.Length; j++)
        {
          var itemIdx = itemIndices[lab.requires[j]];
          statsData[itemIdx].Consumed += labData[curLabOffset];
          curLabOffset++;
        }
        for (int j = 0; j < lab.produced.Length; j++)
        {
          var itemIdx = itemIndices[lab.products[j]];
          statsData[itemIdx].Produced += -labData[curLabOffset];
          curLabOffset++;
        }
      }
    }

    private void LogTotalItemStats()
    {
      var entrySpan = profilingTsData.LevelEntryOffset(0, profilingTick);
      var itemStatsSpan = entrySpan.Slice(statsOffset, statsSize);
      var totalStatsSpan = MemoryMarshal.Cast<ProduceConsumePair, int>(new Span<ProduceConsumePair>(totalStats));
      for (int i = 0; i < totalStatsSpan.Length; i++)
        totalStatsSpan[i] += itemStatsSpan[i];
      var totalStatsDiffSpan = entrySpan.Slice(statsDiffOffset, statsDiffSize);
      for (int i = 0; i < totalStats.Length; i++)
        totalStatsDiffSpan[i] = totalStats[i].Produced - totalStats[i].Consumed;
    }

    private void ClearItemStats()
    {
      var itemStatsSpan = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(statsOffset, statsSize);
      itemStatsSpan.Clear();
    }

    private void EndGameTick_Profiling()
    {
      LogItemStats();
      LogTotalItemStats();
      profilingTsData.SummarizeAtHigherGranularity(profilingTick);
      if (continuousLogging && (profilingTick + 1) % timeSpendGCD == 0)  WriteContinuousLoggingData(1);
      profilingTick += 1;
      ClearItemStats();

      if (profilingTick % timeSpendGCD == 0)
      {
        Debug.Log("Profiling Tick: " + profilingTick);
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

          for (int i = this.statsDiffOffset; i < this.statsDiffOffset + this.statsDiffSize; i++)
            if (span1[i] != span2[i])
              return false;
          return true;
        });

        var summarizeEquals = new Func<int, int, int, bool>((int i1, int i2, int stride) =>
        {
          var span1Summary = new Span<int>(cycleDetectionData, 0, perTickProfilingSize);
          var span2Summary = new Span<int>(cycleDetectionData, perTickProfilingSize, perTickProfilingSize);

          summarizer.Initialize(span1Summary);
          summarizer.Initialize(span2Summary);

          for (int j = stride - 1; j >= 0; j--)
          {
            var span1 = profilingTsData.Level(1).Entry((i1 - j + circularOffset) % profilingEntryCount);
            var span2 = profilingTsData.Level(1).Entry((i2 - j + circularOffset) % profilingEntryCount);

            summarizer.Summarize(span1, span1Summary);
            summarizer.Summarize(span2, span2Summary);
          }

          for (int i = this.statsDiffOffset; i < this.statsDiffOffset + this.statsDiffSize; i++)
            if (span1Summary[i] != span2Summary[i])
              return false;
          return true;
        });

        if (CycleDetection.TryDetectCyclesV2(endIndex, 0, analysisVerificationCount, indexEquals, summarizeEquals, out int cycleLength))
        {
          this.observedCycleLength = cycleLength * timeSpendGCD;
          Debug.Log($"Cycle Length of {this.observedCycleLength} detected");
          if (logProfiledData)
          {
            DumpAnalysisToFile();
          }
          this.GenerateRecipe(endIndex, circularOffset, cycleLength);
          this.GenerateBlackbox();
          this.status = BlackboxStatus.Blackboxed;
          BlackboxManager.Instance.MarkAnalysisForRemoval(this);
        }
      }
      if (profilingTick >= this.profilingTickCount * 100)
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

      var summarizer = new BlackboxBenchmarkV2Summarizer() { analysis = this };

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

      var labData = dataPerCycleSpan.Slice(labOffset, labSize);
      var curLabOffset = 0;
      for (int i = 0; i < labIds.Length; i++)
      {
        var lab = factory.factorySystem.labPool[labIds[i]];

        for (int j = 0; j < lab.served.Length; j++)
        {
          var itemId = lab.requires[j];
          if (!tmp_consumes.ContainsKey(itemId))
            tmp_consumes[itemId] = 0;
          tmp_consumes[itemId] += labData[curLabOffset++];
        }
        for (int j = 0; j < lab.produced.Length; j++)
        {
          var itemId = lab.products[j];
          if (!tmp_produces.ContainsKey(itemId))
            tmp_produces[itemId] = 0;
          tmp_produces[itemId] += -labData[curLabOffset++];
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

      this.analysedRecipe = new BlackboxRecipe()
      {
        idleEnergyPerTick = idleEnergyPerTick,
        workingEnergyPerTick = workingEnergyPerCycle / this.observedCycleLength,
        timeSpend = this.observedCycleLength,
        produces = tmp_produces,
        consumes = tmp_consumes,
        inputs = tmp_stationStorageExit,
        outputs = tmp_stationStorageEnter
      };
    }

    void GenerateBlackbox()
    {
      var blackbox = new Blackbox(this.analysedRecipe, this.factory, this.id)
      {
        entityIds = this.entityIds,
        assemblerIds = this.assemblerIds,
        labIds = this.labIds,
        cargoPathIds = this.cargoPathIds,
        inserterIds = this.inserterIds,
        pcIds = this.pcIds,
        splitterIds = this.splitterIds,
        stationIds = this.stationIds
      };
      BlackboxManager.Instance.AddBlackbox(blackbox);
      blackbox.BeginBlackboxing();
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

    public override void LogLabBefore()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(labOffset, labSize);
      var curLabOffset = 0;
      for (int i = 0; i < labIds.Length; i++)
      {
        var lab = factory.factorySystem.labPool[labIds[i]];

        for (int j = 0; j < lab.served.Length; j++)
          profilingData[curLabOffset++] = lab.served[j];
        for (int j = 0; j < lab.produced.Length; j++)
          profilingData[curLabOffset++] = lab.produced[j];
      }
    }

    public override void LogLabAfter()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(labOffset, labSize);
      var curLabOffset = 0;
      for (int i = 0; i < labIds.Length; i++)
      {
        var lab = factory.factorySystem.labPool[labIds[i]];

        for (int j = 0; j < lab.served.Length; j++)
          profilingData[curLabOffset++] -= lab.served[j];
        for (int j = 0; j < lab.produced.Length; j++)
          profilingData[curLabOffset++] -= lab.produced[j];
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
      if (BlackboxBenchmarkV3.adaptiveStacking)
      {
        for (int i = 0; i < inserterIds.Length; i++)
        {
          ref var inserter = ref factory.factorySystem.inserterPool[inserterIds[i]];
          PlanetFactorySimulation.DoAdaptiveStacking(ref inserter, factory);
        }
      }

      if (profileInserters)
      {
        var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(inserterOffset, inserterSize);
        var curInserterOffset = 0;
        for (int i = 0; i < inserterIds.Length; i++)
        {
          var inserter = factory.factorySystem.inserterPool[inserterIds[i]];
          //profilingData[curInserterOffset + AnalysisData.fo_itemId] = inserter.itemId;
          profilingData[curInserterOffset + AnalysisData.fo_stackCount] = inserter.stackCount;
          profilingData[curInserterOffset + AnalysisData.fo_stage] = (int)inserter.stage;
          profilingData[curInserterOffset + AnalysisData.fo_idleTick] = (int)inserter.idleTick;
          curInserterOffset += AnalysisData.size_inserter;
        }
      }
    }
  }
}