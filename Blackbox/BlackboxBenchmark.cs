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

  public abstract class BlackboxBenchmark : BlackboxAnalysis
  {
    public BlackboxStatus status = BlackboxStatus.Uninitialized;
    public override BlackboxStatus Status => status;

    protected BlackboxBenchmark(PlanetFactory factory, ICollection<int> entityIds) : base(factory, entityIds)
    {
    }

    public virtual void EndGameTick() { }

    public virtual void LogPowerConsumer() { }

    public virtual void LogAssemblerBefore() { }

    public virtual void LogAssemblerAfter() { }

    public virtual void LogLabBefore() { }

    public virtual void LogLabAfter() { }

    public virtual void LogStationBefore() { }

    public virtual void LogStationAfter() { }

    public virtual void LogInserter() { }
  }

  public class BlackboxGatewayMethods
  {
    public static void GameTick_AfterPowerConsumerComponents(PlanetFactory factory)
    {
      var benchmarks = BlackboxManager.Instance.analyses.OfType<BlackboxBenchmark>();
      foreach (var benchmark in benchmarks)
      {
        if (benchmark.status == BlackboxStatus.Profiling && benchmark.Factory == factory)
        {
          benchmark.LogPowerConsumer();
          benchmark.LogAssemblerBefore();
          benchmark.LogLabBefore();
          //Debug.Log("Setting up initial values");
        }
      }
    }

    public static void GameTick_AfterFactorySystem(PlanetFactory factory)
    {
      var benchmarks = BlackboxManager.Instance.analyses.OfType<BlackboxBenchmark>();
      foreach (var benchmark in benchmarks)
      {
        if (benchmark.status == BlackboxStatus.Profiling && benchmark.Factory == factory)
        {
          benchmark.LogAssemblerAfter();
          benchmark.LogLabAfter();
          benchmark.LogStationBefore();
          //Debug.Log("Noting production and consumption");
        }
      }
    }

    public static void GameTick_AfterStationBeltInput(PlanetFactory factory)
    {
    }

    public static void GameTick_AfterStationBeltOutput(PlanetFactory factory)
    {
      var benchmarks = BlackboxManager.Instance.analyses.OfType<BlackboxBenchmark>();
      foreach (var benchmark in benchmarks)
      {
        if (benchmark.status == BlackboxStatus.Profiling && benchmark.Factory == factory)
        {
          benchmark.LogStationAfter();
          benchmark.LogInserter();
        }
      }
    }

    public static void GameTick_AfterInserter(PlanetFactory factory)
    {
      //var benchmarks = BlackboxManager.Instance.analyses.OfType<BlackboxBenchmark>();
      //foreach (var benchmark in benchmarks)
      //{
      //  if (benchmark.status == BlackboxStatus.Profiling && benchmark.Factory == factory)
      //  {
      //    benchmark.LogInserter();
      //  }
      //}
    }

    public static void GameTick_End()
    {
      var benchmarks = BlackboxManager.Instance.analyses.OfType<BlackboxBenchmark>();
      foreach (var benchmark in benchmarks)
      {
        benchmark.EndGameTick();
      }
    }
  }

  [HarmonyPatch]
  class BlackboxBenchmarkPatch
  {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickBeforePower))]
    public static void FactorySystem__GameTickBeforePower(FactorySystem __instance)
    {
      BlackboxGatewayMethods.GameTick_AfterPowerConsumerComponents(__instance.factory);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.PreparePowerSystemFactoryData))]
    public static void MultithreadSystem__PreparePowerSystemFactoryData(PlanetFactory[] _factories)
    {
      foreach (var factory in _factories)
        BlackboxGatewayMethods.GameTick_AfterPowerConsumerComponents(factory);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabProduceMode), typeof(long), typeof(bool))]
    public static void FactorySystem__GameTickLabProduceMode(FactorySystem __instance)
    {
      BlackboxGatewayMethods.GameTick_AfterFactorySystem(__instance.factory);
    }

    //[HarmonyPostfix]
    //[HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.PrepareTransportData))]
    //public static void MultithreadSystem__PrepareTransportData(MultithreadSystem __instance, PlanetData _localPlanet, PlanetFactory[] _factories, int _transportFactoryCnt, long _time)
    //{
    //  foreach (var factory in _factories)
    //    BlackboxGatewayMethods.GameTick_AfterFactorySystem(factory);
    //}


    [HarmonyPrefix]
    [HarmonyPatch(typeof(MultithreadSystem), nameof(MultithreadSystem.PrepareLabOutput2NextData))]
    public static void MultithreadSystem__PrepareLabOutput2NextData(PlanetFactory[] _factories)
    {
      foreach (var factory in _factories)
        BlackboxGatewayMethods.GameTick_AfterFactorySystem(factory);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.GameTick_OutputToBelt))]
    public static void PlanetTransport__GameTick_OutputToBelt(PlanetTransport __instance)
    {
      BlackboxGatewayMethods.GameTick_AfterStationBeltOutput(__instance.factory);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    public static void GameData__GameTick()
    {
      BlackboxGatewayMethods.GameTick_End();
    }
  }
}