using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace DysonSphereProgram.Modding.Blackbox
{
  public enum BlackboxStatus
  {
    Initialized,
    SelectionExpanding,
    SelectionFinalized,
    Fingerprinted,
    Invalid,
    InAnalysis,
    AnalysisFailed,
    RecipeObtained,
    Blackboxed
  }

  public class Blackbox
  {
    public readonly int Id;
    public BlackboxSelection Selection { get; private set; }
    public BlackboxFingerprint Fingerprint { get; private set; }
    public BlackboxRecipe Recipe { get; private set; }
    public BlackboxAnalysis Analysis { get; private set; }
    public BlackboxSimulation Simulation { get; private set; }
    public BlackboxStatus Status { get; private set; }
    public string Name;

    public int FactoryIndex => Selection.factoryIndex;
    public WeakReference<PlanetFactory> FactoryRef => Selection.factoryRef;

    internal bool analyseInBackground;
    public static bool analyseInBackgroundConfig = true;

    internal Blackbox(int id, BlackboxSelection selection)
    {
      Id = id;
      Selection = selection;
      Status = BlackboxStatus.Initialized;
      Name = "Blackbox #" + Id;
      analyseInBackground = analyseInBackgroundConfig;
    }

    public void NotifyBlackboxed(BlackboxRecipe recipe)
    {
      if (Status != BlackboxStatus.InAnalysis)
        return;

      // TODO: Implement other checks if necessary
      Recipe = recipe;
      Status = BlackboxStatus.RecipeObtained;
    }

    public void NotifyAnalysisFailed()
    {
      if (Status != BlackboxStatus.InAnalysis)
        return;

      Status = BlackboxStatus.AnalysisFailed;
    }

    public void GameTick()
    {
      if (!FactoryRef.TryGetTarget(out var factory))
      {
        Plugin.Log.LogError("PlanetFactory instance pulled out from under " + nameof(Blackbox) + " in " + nameof(GameTick));
        return;
      }

      switch (Status)
      {
        case BlackboxStatus.Initialized:
          Status = BlackboxStatus.SelectionExpanding;
          break;
        case BlackboxStatus.SelectionExpanding:
          Selection = BlackboxSelection.Expand(Selection);
          if (BlackboxSelection.IsInvalid(Selection))
            Status = BlackboxStatus.Invalid;
          else
            Status = BlackboxStatus.SelectionFinalized;
          break;
        case BlackboxStatus.SelectionFinalized:
          // TODO: Ensure that fingerprinting happens
          Fingerprint = BlackboxFingerprint.CreateFrom(Selection);
          Status = BlackboxStatus.Fingerprinted;
          break;
        case BlackboxStatus.Fingerprinted:
          // TODO: Check the blackbox for FingerprintedRecipe, else move to InAnalysis
          Analysis = new BlackboxBenchmark(this);
          Status = BlackboxStatus.InAnalysis;
          Analysis.Begin();
          break;
        case BlackboxStatus.AnalysisFailed:
          Analysis?.Free();
          Analysis = null;
          break;
        case BlackboxStatus.RecipeObtained:
          Analysis?.Free();
          Analysis = null;
          Status = BlackboxStatus.Blackboxed;
          if (Simulation == null)
          {
            Simulation = new BlackboxSimulation(this);
            Simulation.BeginBlackboxing();
          }
          break;
      }
    }
  }
}