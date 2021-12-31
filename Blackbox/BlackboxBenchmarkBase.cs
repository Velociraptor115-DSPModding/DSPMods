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
  public abstract class BlackboxBenchmarkBase : BlackboxAnalysis
  {
    protected BlackboxBenchmarkBase(Blackbox blackbox) : base(blackbox)
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
      var benchmarks =
        from x in BlackboxManager.Instance.blackboxes
        where (x.Status == BlackboxStatus.InAnalysis && !x.analyseInBackground && x.Analysis is BlackboxBenchmarkBase)
        select x.Analysis as BlackboxBenchmarkBase
        ;

      foreach (var benchmark in benchmarks)
      {
        if (!benchmark.factoryRef.TryGetTarget(out var benchmarkFactory))
        {
          Plugin.Log.LogError("PlanetFactory instance pulled out from under " + nameof(BlackboxBenchmark) + " in " + nameof(GameTick_AfterPowerConsumerComponents));
          continue;
        }

        if (benchmarkFactory == factory)
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
      var benchmarks =
        from x in BlackboxManager.Instance.blackboxes
        where (x.Status == BlackboxStatus.InAnalysis && !x.analyseInBackground && x.Analysis is BlackboxBenchmarkBase)
        select x.Analysis as BlackboxBenchmarkBase
        ;

      foreach (var benchmark in benchmarks)
      {
        if (!benchmark.factoryRef.TryGetTarget(out var benchmarkFactory))
        {
          Plugin.Log.LogError("PlanetFactory instance pulled out from under " + nameof(BlackboxBenchmark) + " in " + nameof(GameTick_AfterFactorySystem));
          continue;
        }

        if (benchmarkFactory == factory)
        {
          benchmark.LogAssemblerAfter();
          benchmark.LogLabAfter();
          benchmark.LogStationBefore();
          //Debug.Log("Noting production and consumption");
        }
      }
    }

    public static void GameTick_AfterStationBeltOutput(PlanetFactory factory)
    {
      var benchmarks =
        from x in BlackboxManager.Instance.blackboxes
        where (x.Status == BlackboxStatus.InAnalysis && !x.analyseInBackground && x.Analysis is BlackboxBenchmarkBase)
        select x.Analysis as BlackboxBenchmarkBase
        ;

      foreach (var benchmark in benchmarks)
      {
        if (!benchmark.factoryRef.TryGetTarget(out var benchmarkFactory))
        {
          Plugin.Log.LogError("PlanetFactory instance pulled out from under " + nameof(BlackboxBenchmark) + " in " + nameof(GameTick_AfterStationBeltOutput));
          continue;
        }

        if (benchmarkFactory == factory)
        {
          benchmark.LogStationAfter();
          benchmark.LogInserter();
        }
      }
    }

    public static void GameTick_End()
    {
      var benchmarks =
        from x in BlackboxManager.Instance.blackboxes
        where (x.Status == BlackboxStatus.InAnalysis && !x.analyseInBackground && x.Analysis is BlackboxBenchmarkBase)
        select x.Analysis as BlackboxBenchmarkBase
        ;
      
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