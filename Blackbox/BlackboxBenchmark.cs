using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DysonSphereProgram.Modding.Blackbox
{
  public class AnalysisData
  {
    public const int fo_storage0delta = 0;
    public const int fo_storage1delta = 1;
    public const int fo_storage2delta = 2;
    public const int fo_storage3delta = 3;
    public const int fo_storage4delta = 4;
    public const int size_station = 5;

    public const int fo_served0 = 0;
    public const int fo_served1 = 1;
    public const int fo_served2 = 2;
    public const int fo_served3 = 3;
    public const int fo_served4 = 4;
    public const int fo_served5 = 5;
    public const int fo_produced0 = 6;
    public const int fo_produced1 = 7;
    public const int size_assembler = 8;

    //public const int fo_itemId = 0;
    //public const int fo_stackCount = 1;
    //public const int fo_stage = 2;
    //public const int size_inserter = 3;
    public const int fo_stackCount = 0;
    public const int fo_stage = 1;
    public const int fo_idleTick = 2;
    public const int size_inserter = 3;

    public const int fo_requiredEnergy_low = 0;
    public const int fo_requiredEnergy_high = 1;
    public const int size_powerConsumer = 2;
  }

  public struct AnalysisStationInfo
  {
    int storage0delta;
    int storage1delta;
    int storage2delta;
    int storage3delta;
    int storage4delta;
  }

  public struct AnalysisAssemblerInfo
  {
    int served0;
    int served1;
    int served2;
    int served3;
    int served4;
    int served5;
    int produced0;
    int produced1;
  }

  public struct AnalysisInserterInfo
  {
    //int itemId;
    int stackCount;
    EInserterStage stage;
  }

  public struct AnalysisPowerConsumerInfo
  {
    long requiredEnergy;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct ProduceConsumePair
  {
    public int Produced;
    public int Consumed;
  }

  public class BlackboxBenchmark: BlackboxBenchmarkBase
  {
    internal readonly ImmutableSortedSet<int> entityIds;
    internal readonly ImmutableSortedSet<int> pcIds;
    internal readonly ImmutableSortedSet<int> assemblerIds;
    internal readonly ImmutableSortedSet<int> labIds;
    internal readonly ImmutableSortedSet<int> inserterIds;
    internal readonly ImmutableSortedSet<int> stationIds;
    internal readonly ImmutableSortedSet<int> cargoPathIds;
    internal readonly ImmutableSortedSet<int> splitterIds;
    internal readonly ImmutableSortedSet<int> itemIds;

    const int TicksPerSecond = 60;
    const int TicksPerMinute = TicksPerSecond * 60;

    const bool profileInserters = false;
    public static bool logProfiledData = false;
    const bool analyzeInserterStackEffect = false;
    public static bool forceNoStackingConfig = false;
    public static bool adaptiveStackingConfig = false;
    public bool adaptiveStacking;
    public bool forceNoStacking;

    public static bool continuousLogging = false;
    StreamWriter continuousLogger;

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
    int[] stabilityDetectionData;
    int stabilizedTick = -1;

    const int analysisVerificationCount = 4;
    int timeSpendGCD;
    int timeSpendLCM;
    int profilingTickCount;
    int profilingEntryCount;
    int observedCycleLength;

    BlackboxRecipe analysedRecipe;
    public override BlackboxRecipe EffectiveRecipe => analysedRecipe;

    internal PlanetFactory simulationFactory;
    internal Task profilingTask;
    internal CancellationTokenSource profilingTaskCancel;

    public static string FileLogPath
    {
      //get => Path.GetDirectoryName(@"D:\Raptor\Workspace\Personal\Projects\DSPMods\Blackbox\");
      get => Path.GetDirectoryName(Plugin.Path);
    }

    public BlackboxBenchmark(Blackbox blackbox) : base(blackbox)
    {
      this.entityIds = blackbox.Selection.entityIds;
      this.pcIds = blackbox.Selection.pcIds;
      this.assemblerIds = blackbox.Selection.assemblerIds;
      this.labIds = blackbox.Selection.labIds;
      this.inserterIds = blackbox.Selection.inserterIds;
      this.stationIds = blackbox.Selection.stationIds;
      this.cargoPathIds = blackbox.Selection.cargoPathIds;
      this.splitterIds = blackbox.Selection.splitterIds;
      this.itemIds = blackbox.Selection.itemIds;
    }

    class BlackboxBenchmarkSummarizer : ISummarizer<int>
    {
      public BlackboxBenchmark analysis;

      public void Initialize(Span<int> data)
      {
        data.Clear();
      }

      public void Summarize(Span<int> detailed, Span<int> summary)
      {
        var pcCount = analysis.pcIds.Count;
        var pcDetailed = MemoryMarshal.Cast<int, long>(detailed.Slice(pcOffset, pcCount * 2));
        var pcSummary = MemoryMarshal.Cast<int, long>(summary.Slice(pcOffset, pcCount * 2));
        for (int i = 0; i < pcSummary.Length; i++)
          pcSummary[i] += pcDetailed[i];

        var restDetailed = detailed.Slice(analysis.assemblerOffset, analysis.assemblerSize + analysis.stationSize + analysis.inserterSize + analysis.labSize + analysis.statsSize);
        var restSummary = summary.Slice(analysis.assemblerOffset, analysis.assemblerSize + analysis.stationSize + analysis.inserterSize + analysis.labSize + analysis.statsSize);
        for (int i = 0; i < restSummary.Length; i++)
          restSummary[i] += restDetailed[i];

        var statsDiffDetailed = detailed.Slice(analysis.statsDiffOffset, analysis.statsDiffSize);
        var statsDiffSummary = summary.Slice(analysis.statsDiffOffset, analysis.statsDiffSize);
        for (int i = 0; i < statsDiffSummary.Length; i++)
          statsDiffSummary[i] = Math.Max(statsDiffSummary[i], statsDiffDetailed[i]);
      }
    }

    public override void Begin()
    {
      if (!factoryRef.TryGetTarget(out var factory))
      {
        Plugin.Log.LogError("PlanetFactory instance pulled out from under " + nameof(BlackboxBenchmark) + " in " + nameof(Begin));
        return;
      }

      this.simulationFactory = blackbox.analyseInBackground ? PlanetFactorySimulation.CloneForSimulation(factory, blackbox.Selection) : factory;

      var tmp_assemblerTimeSpends = new List<int>();

      foreach (var entityId in entityIds)
      {
        ref readonly var entity = ref simulationFactory.entityPool[entityId];
        if (entity.assemblerId > 0)
        {
          ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[entity.assemblerId];
          tmp_assemblerTimeSpends.Add(assembler.timeSpend / assembler.speed);
        }
        if (entity.labId > 0)
        {
          ref readonly var lab = ref simulationFactory.factorySystem.labPool[entity.labId];
          if (lab.matrixMode)
          {
            tmp_assemblerTimeSpends.Add(lab.timeSpend / 10000);
          }
        }
      }

      this.assemblerOffsets = new int[assemblerIds.Count];
      this.stationOffsets = new int[stationIds.Count];

      this.pcSize = AnalysisData.size_powerConsumer * pcIds.Count;
      this.assemblerSize = 0;
      for (int i = 0; i < assemblerIds.Count; i++)
      {
        assemblerOffsets[i] = assemblerSize;
        ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[assemblerIds[i]];
        this.assemblerSize += assembler.served.Length + assembler.produced.Length;
      }
      this.stationSize = 0;
      for (int i = 0; i < stationIds.Count; i++)
      {
        stationOffsets[i] = stationSize;
        ref readonly var station = ref simulationFactory.transport.stationPool[stationIds[i]];
        this.stationSize += station.storage.Length;
      }
      this.inserterSize = profileInserters ? AnalysisData.size_inserter * inserterIds.Count : 0;
      this.labSize = 0;
      for (int i = 0; i < labIds.Count; i++)
      {
        //assemblerOffsets[i] = assemblerSize;
        ref readonly var lab = ref simulationFactory.factorySystem.labPool[labIds[i]];
        this.labSize += lab.served.Length + lab.produced.Length;
      }
      this.statsSize = itemIds.Count * 2;
      this.statsDiffSize = itemIds.Count;

      this.assemblerOffset = pcOffset + pcSize;
      this.stationOffset = assemblerOffset + assemblerSize;
      this.inserterOffset = stationOffset + stationSize;
      this.labOffset = inserterOffset + inserterSize;
      this.statsOffset = labOffset + labSize;
      this.statsDiffOffset = statsOffset + statsSize;
      this.perTickProfilingSize = pcSize + assemblerSize + stationSize + inserterSize + labSize + statsSize + statsDiffSize;

      this.totalStats = new ProduceConsumePair[itemIds.Count];

      var distinctTimeSpends = tmp_assemblerTimeSpends.Distinct().DefaultIfEmpty(60);
      this.timeSpendGCD = Utils.GCD(distinctTimeSpends);
      this.timeSpendLCM = Utils.LCM(distinctTimeSpends);
      this.profilingTickCount = timeSpendLCM * ((analysisVerificationCount * 2 * /* to account for sorter stacking */ 6) + 2);
      this.profilingEntryCount = profilingTickCount / timeSpendGCD;

      var mlg = new MultiLevelGranularity();
      mlg.levels = 2;
      mlg.entryCounts = new[] { timeSpendGCD, this.profilingEntryCount };
      mlg.ratios = new[] { timeSpendGCD };

      this.summarizer = new BlackboxBenchmarkSummarizer() { analysis = this };

      profilingTsData = new TimeSeriesData<int>(this.perTickProfilingSize, mlg, summarizer);
      this.cycleDetectionData = new int[this.perTickProfilingSize * 2];
      this.stabilityDetectionData = new int[this.statsDiffSize];
      profilingTick = 0;

      this.forceNoStacking = forceNoStackingConfig;
      this.adaptiveStacking = adaptiveStackingConfig;

      if (continuousLogging)
      {
        Directory.CreateDirectory($@"{FileLogPath}\DataAnalysis");
        continuousLogger = new StreamWriter($@"{FileLogPath}\DataAnalysis\BenchmarkV2_CL_{blackbox.Id}.csv");

        WriteContinuousLoggingHeader();
      }

      if (blackbox.analyseInBackground)
      {
        profilingTaskCancel = new CancellationTokenSource();
        var ct = profilingTaskCancel.Token;
        profilingTask = Task.Factory.StartNew(() => SimulateTillProfilingDone(ct), profilingTaskCancel.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
      }
    }
    
    private void SimulateTillProfilingDone(CancellationToken ct)
    {
      while (blackbox.Status == BlackboxStatus.InAnalysis)
      {
        ct.ThrowIfCancellationRequested();
        PlanetFactorySimulation.SimulateGameTick(this);
      }
    }

    public override void Free()
    {
      if (continuousLogging)
      {
        continuousLogger.Dispose();
        continuousLogger = null;
      }

      analysedRecipe = null;
      if (blackbox.analyseInBackground)
      {
        if (profilingTask != null && profilingTaskCancel != null)
        {
          try
          {
            if (!profilingTask.IsCompleted)
            {
              profilingTaskCancel.Cancel();
              profilingTask.Wait();
            }
          }
          catch (AggregateException ex)
          {
            if (ex.InnerException is OperationCanceledException)
              Plugin.Log.LogInfo("Blackbox #" + blackbox.Id + " benchmarking was cancelled");
            else
              Plugin.Log.LogError(ex);
          }
          catch (Exception ex)
          {
            Plugin.Log.LogError(ex);
          }
          finally
          {
            profilingTaskCancel.Dispose();
            profilingTaskCancel = null;
          }
        }

        PlanetFactorySimulation.FreeSimulationFactory(simulationFactory);
      }
      simulationFactory = null;
    }

    private void DumpAnalysisToFile()
    {
      Directory.CreateDirectory($@"{FileLogPath}\DataAnalysis");
      using (var f = new FileStream($@"{FileLogPath}\DataAnalysis\BenchmarkV2_{blackbox.Id}.txt", FileMode.Create))
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
          for (int i = 0; i < itemIds.Count; i++)
          {
            var itemName = LDB.ItemName(itemIds[i]);
            s.WriteLine(itemName);
            var stats = totalStats[i];
            s.WriteLine($"  Produced: {stats.Produced}");
            s.WriteLine($"  Consumed: {stats.Consumed}");
            s.WriteLine($"  Difference: {stats.Produced - stats.Consumed}");
          }
        }
      }
    }

    private void WriteContinuousLoggingHeader()
    {
      for (int i = 0; i < pcIds.Count; i++)
        continuousLogger.Write($"PC{i},");

      for (int i = 0; i < assemblerIds.Count; i++)
      {
        ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[assemblerIds[i]];

        for (int j = 0; j < assembler.served.Length; j++)
        {
          continuousLogger.Write($"A{i}_I{j},");
        }
        for (int j = 0; j < assembler.produced.Length; j++)
        {
          continuousLogger.Write($"A{i}_O{j},");
        }
      }

      for (int i = 0; i < stationIds.Count; i++)
      {
        ref readonly var station = ref simulationFactory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
        {
          continuousLogger.Write($"S{i}_{j},");
        }
      }

      if (profileInserters)
      {
        for (int i = 0; i < inserterIds.Count; i++)
        {
          continuousLogger.Write($"I{i}_C,");
          continuousLogger.Write($"I{i}_S,");
          continuousLogger.Write($"I{i}_I,");
        }
      }

      for (int i = 0; i < labIds.Count; i++)
      {
        ref readonly var lab = ref simulationFactory.factorySystem.labPool[labIds[i]];

        for (int j = 0; j < lab.served.Length; j++)
        {
          continuousLogger.Write($"L{i}_I{j},");
        }
        for (int j = 0; j < lab.produced.Length; j++)
        {
          continuousLogger.Write($"L{i}_O{j},");
        }
      }

      for (int i = 0; i < itemIds.Count; i++)
      {
        var itemId = itemIds[i];
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
      for (int i = 0; i < pcIds.Count; i++)
      {
        continuousLogger.Write(pcData[i]);
        continuousLogger.Write(',');
      }

      var assemblerData = entry.Slice(assemblerOffset, assemblerSize);
      var curAssemblerOffset = 0;
      for (int i = 0; i < assemblerIds.Count; i++)
      {
        ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[assemblerIds[i]];

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
      for (int i = 0; i < stationIds.Count; i++)
      {
        ref readonly var station = ref simulationFactory.transport.stationPool[stationIds[i]];
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
        for (int i = 0; i < inserterIds.Count; i++)
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
      for (int i = 0; i < labIds.Count; i++)
      {
        ref readonly var lab = ref simulationFactory.factorySystem.labPool[labIds[i]];

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
      for (int i = 0; i < itemIds.Count; i++)
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
      for (int i = 0; i < assemblerIds.Count; i++)
      {
        ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[assemblerIds[i]];
        for (int j = 0; j < assembler.served.Length; j++)
        {
          var itemIdx = itemIds.IndexOf(assembler.requires[j]);
          statsData[itemIdx].Consumed += assemblerData[curAssemblerOffset];
          curAssemblerOffset++;
        }
        for (int j = 0; j < assembler.produced.Length; j++)
        {
          var itemIdx = itemIds.IndexOf(assembler.products[j]);
          statsData[itemIdx].Produced += -assemblerData[curAssemblerOffset];
          curAssemblerOffset++;
        }
      }

      var stationData = levelEntrySpan.Slice(stationOffset, stationSize);
      var curStationOffset = 0;
      for (int i = 0; i < stationIds.Count; i++)
      {
        ref readonly var station = ref simulationFactory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
        {
          var stationStorage = station.storage[j];
          var effectiveLogic = stationStorage.remoteLogic == ELogisticStorage.None ? stationStorage.localLogic : stationStorage.remoteLogic;
          if (effectiveLogic != ELogisticStorage.None && stationStorage.itemId > 0)
          {
            var itemIdx = itemIds.IndexOf(stationStorage.itemId);
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
      for (int i = 0; i < labIds.Count; i++)
      {
        ref readonly var lab = ref simulationFactory.factorySystem.labPool[labIds[i]];
        for (int j = 0; j < lab.served.Length; j++)
        {
          var itemIdx = itemIds.IndexOf(lab.requires[j]);
          statsData[itemIdx].Consumed += labData[curLabOffset];
          curLabOffset++;
        }
        for (int j = 0; j < lab.produced.Length; j++)
        {
          var itemIdx = itemIds.IndexOf(lab.products[j]);
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

    private void CheckStabilization()
    {
      var entrySpan = profilingTsData.LevelEntryOffset(0, profilingTick);
      var totalStatsDiffSpan = entrySpan.Slice(statsDiffOffset, statsDiffSize);
      for (int i = 0; i < totalStatsDiffSpan.Length; i++)
        if (totalStatsDiffSpan[i] > stabilityDetectionData[i])
        {
          this.stabilizedTick = this.profilingTick;
          stabilityDetectionData[i] = totalStatsDiffSpan[i];
        }
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
      CheckStabilization();
      profilingTsData.SummarizeAtHigherGranularity(profilingTick);
      if (continuousLogging && (profilingTick + 1) % timeSpendGCD == 0)  WriteContinuousLoggingData(1);
      profilingTick += 1;
      ClearItemStats();

      if (profilingTick % timeSpendGCD == 0)
      {
        Plugin.Log.LogDebug("Profiling Tick: " + profilingTick);
      }
      if (profilingTick - stabilizedTick > timeSpendLCM && profilingTick % timeSpendGCD == 0)
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

          for (int i = this.statsOffset; i < this.statsOffset + this.statsSize; i++)
            if (span1[i] != span2[i])
              return false;

          for (int i = this.stationOffset; i < this.stationOffset + this.stationSize; i++)
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

          for (int i = this.statsOffset; i < this.statsOffset + this.statsSize; i++)
            if (span1Summary[i] != span2Summary[i])
              return false;

          for (int i = this.stationOffset; i < this.stationOffset + this.stationSize; i++)
            if (span1Summary[i] != span2Summary[i])
              return false;

          return true;
        });

        if (CycleDetection.TryDetectCycles(endIndex, 0, analysisVerificationCount, indexEquals, summarizeEquals, out int cycleLength))
        {
          this.observedCycleLength = cycleLength * timeSpendGCD;
          Debug.Log($"Cycle Length of {this.observedCycleLength} detected");
          if (logProfiledData)
          {
            DumpAnalysisToFile();
          }

          this.GenerateRecipe(endIndex, circularOffset, cycleLength);
          blackbox.NotifyBlackboxed(this.analysedRecipe);
        }
      }
      if (profilingTick >= this.profilingTickCount * 40)
      {
        profilingTick = 0;
        Plugin.Log.LogDebug($"Analysis Failed");
        if (logProfiledData)
        {
          DumpAnalysisToFile();
        }
        blackbox.NotifyAnalysisFailed();
      }
    }

    public override void EndGameTick()
    {
      if (blackbox.Status == BlackboxStatus.InAnalysis)
      {
        EndGameTick_Profiling();
      }
    }

    void GenerateRecipe(int endIndex, int circularOffset, int cycleLength)
    {
      long idleEnergyPerTick = 0;
      for (int i = 0; i < pcIds.Count; i++)
        idleEnergyPerTick += simulationFactory.powerSystem.consumerPool[pcIds[i]].idleEnergyPerTick;

      long idleEnergyPerCycle = idleEnergyPerTick * this.observedCycleLength;

      long workingEnergyPerCycle = 0;

      var summarizer = new BlackboxBenchmarkSummarizer() { analysis = this };

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
      for (int i = 0; i < stationIds.Count; i++)
      {
        ref readonly var station = ref simulationFactory.transport.stationPool[stationIds[i]];
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
      for (int i = 0; i < assemblerIds.Count; i++)
      {
        ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[assemblerIds[i]];

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
      for (int i = 0; i < labIds.Count; i++)
      {
        ref readonly var lab = ref simulationFactory.factorySystem.labPool[labIds[i]];

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

      Plugin.Log.LogDebug($"Idle Energy per cycle: {idleEnergyPerCycle}");
      Plugin.Log.LogDebug($"Working Energy per cycle: {workingEnergyPerCycle}");
      Plugin.Log.LogDebug($"Idle Power: {(idleEnergyPerCycle / this.observedCycleLength) * TicksPerSecond}");
      Plugin.Log.LogDebug($"Working Power: {(workingEnergyPerCycle / this.observedCycleLength) * TicksPerSecond}");

      Plugin.Log.LogDebug("Consumed");
      foreach (var item in tmp_consumes)
      {
        var itemName = LDB.ItemName(item.Key);
        Plugin.Log.LogDebug($"  {item.Value} {itemName}");
      }

      Plugin.Log.LogDebug("Produced");
      foreach (var item in tmp_produces)
      {
        var itemName = LDB.ItemName(item.Key);
        Plugin.Log.LogDebug($"  {item.Value} {itemName}");
      }

      Plugin.Log.LogDebug("Inputs");
      foreach (var stationIdx in tmp_stationStorageExit)
      {
        Plugin.Log.LogDebug($"  Station #{stationIdx.Key}:");
        foreach (var itemId in stationIdx.Value)
        {
          var itemName = LDB.ItemName(itemId.Key);
          Plugin.Log.LogDebug($"    {itemId.Value} {itemName}");
        }
      }

      Plugin.Log.LogDebug("Outputs");
      foreach (var stationIdx in tmp_stationStorageEnter)
      {
        Plugin.Log.LogDebug($"  Station #{stationIdx.Key}:");
        foreach (var itemId in stationIdx.Value)
        {
          var itemName = LDB.ItemName(itemId.Key);
          Plugin.Log.LogDebug($"    {itemId.Value} {itemName}");
        }
      }

      Plugin.Log.LogDebug($"Time (in ticks): {this.observedCycleLength}");
      Plugin.Log.LogDebug($"Time (in seconds): {this.observedCycleLength / (float)TicksPerSecond}");

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

    public override void LogPowerConsumer()
    {
      var profilingData = MemoryMarshal.Cast<int, long>(profilingTsData.LevelEntryOffset(0, profilingTick).Slice(pcOffset, assemblerOffset));
      for (int i = 0; i < pcIds.Count; i++)
      {
        ref readonly var consumer = ref simulationFactory.powerSystem.consumerPool[pcIds[i]];
        profilingData[i] = consumer.requiredEnergy;
      }
    }

    public override void LogAssemblerBefore()
    {
      var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(assemblerOffset, assemblerSize);
      var curAssemblerOffset = 0;
      for (int i = 0; i < assemblerIds.Count; i++)
      {
        ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[assemblerIds[i]];
        
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
      for (int i = 0; i < assemblerIds.Count; i++)
      {
        ref readonly var assembler = ref simulationFactory.factorySystem.assemblerPool[assemblerIds[i]];

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
      for (int i = 0; i < labIds.Count; i++)
      {
        ref readonly var lab = ref simulationFactory.factorySystem.labPool[labIds[i]];

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
      for (int i = 0; i < labIds.Count; i++)
      {
        ref readonly var lab = ref simulationFactory.factorySystem.labPool[labIds[i]];

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
      for (int i = 0; i < stationIds.Count; i++)
      {
        ref readonly var station = ref simulationFactory.transport.stationPool[stationIds[i]];

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
      for (int i = 0; i < stationIds.Count; i++)
      {
        ref readonly var station = ref simulationFactory.transport.stationPool[stationIds[i]];
        for (int j = 0; j < station.storage.Length; j++)
          profilingData[curStationOffset++] -= station.storage[j].count;
      }
    }

    public override void LogInserter()
    {
      if (adaptiveStacking || forceNoStacking)
      {
        for (int i = 0; i < inserterIds.Count; i++)
        {
          ref var inserter = ref simulationFactory.factorySystem.inserterPool[inserterIds[i]];
          PlanetFactorySimulation.DoAdaptiveStacking(ref inserter, simulationFactory, forceNoStacking);
        }
      }

      if (profileInserters)
      {
        var profilingData = profilingTsData.LevelEntryOffset(0, profilingTick).Slice(inserterOffset, inserterSize);
        var curInserterOffset = 0;
        for (int i = 0; i < inserterIds.Count; i++)
        {
          ref readonly var inserter = ref simulationFactory.factorySystem.inserterPool[inserterIds[i]];
          profilingData[curInserterOffset + AnalysisData.fo_stackCount] = inserter.stackCount;
          profilingData[curInserterOffset + AnalysisData.fo_stage] = (int)inserter.stage;
          profilingData[curInserterOffset + AnalysisData.fo_idleTick] = (int)inserter.idleTick;
          curInserterOffset += AnalysisData.size_inserter;
        }
      }
    }

    public override float Progress => this.profilingTick / (float)(profilingTickCount * 40);
    public override string ProgressText => $"{profilingTick} / {profilingTickCount * 40}";
  }
}