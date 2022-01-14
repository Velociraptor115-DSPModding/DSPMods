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
    Initialized = 0,
    SelectionExpanding = 1,
    SelectionFinalized = 2,
    Fingerprinted = 3,
    Invalid = 4,
    InAnalysis = 5,
    AnalysisFailed = 6,
    RecipeObtained = 7,
    Blackboxed = 8
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

    const int saveLogicVersion = 1;

    public void PreserveVanillaSaveBefore()
    {
      Simulation?.PreserveVanillaSaveBefore();
    }

    public void PreserveVanillaSaveAfter()
    {
      Simulation?.PreserveVanillaSaveAfter();
    }

    public void Export(BinaryWriter w)
    {
      w.Write(saveLogicVersion);
      w.Write(Id);
      Selection.Export(w);
      w.Write(Name);
      w.Write(analyseInBackground);

      var status = Status;
      if (status == BlackboxStatus.InAnalysis)
        status = BlackboxStatus.Fingerprinted;

      w.Write((int)status);

      var isFingerprinted = Fingerprint != null;
      w.Write(isFingerprinted);

      var isRecipeObtained = Recipe != null;
      w.Write(isRecipeObtained);
      if (isRecipeObtained)
      {
        Recipe.Export(w);
      }

      var isSimulationPresent = Simulation != null;
      w.Write(isSimulationPresent);
      if (isSimulationPresent)
      {
        Simulation.Export(w);
      }
    }

    public static Blackbox Import(BinaryReader r)
    {
      var saveLogicVersion = r.ReadInt32();
      var id = r.ReadInt32();
      var selection = BlackboxSelection.Import(r);
      var blackbox = new Blackbox(id, selection);
      blackbox.Name = r.ReadString();
      blackbox.analyseInBackground = r.ReadBoolean();

      blackbox.Status = (BlackboxStatus)r.ReadInt32();

      var isFingerprinted = r.ReadBoolean();
      if (isFingerprinted)
        blackbox.Fingerprint = BlackboxFingerprint.CreateFrom(blackbox.Selection);

      var isRecipeObtained = r.ReadBoolean();
      if (isRecipeObtained)
        blackbox.Recipe = BlackboxRecipe.Import(r);

      var isSimulationPresent = r.ReadBoolean();
      if (isSimulationPresent)
      {
        blackbox.Simulation = new BlackboxSimulation(blackbox);
        blackbox.Simulation.CreateBlackboxingResources();
        blackbox.Simulation.Import(r);
      }

      return blackbox;
    }
  }
}