using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace DysonSphereProgram.Modding.Blackbox
{
  public class BlackboxManager
  {
    public static BlackboxManager Instance { get; } = new BlackboxManager();
    private static int blackboxIdCounter = 1;

    public List<Blackbox> blackboxes = new List<Blackbox>();
    public List<Blackbox> blackboxesMarkedForRemoval = new List<Blackbox>();

    public AutoBlackbox autoBlackbox = new AutoBlackbox();
    public BlackboxHighlight highlight = new BlackboxHighlight();
    public Blackbox CreateForSelection(BlackboxSelection selection)
    {
      Blackbox blackbox;
      lock (Instance)
      {
        var id = blackboxIdCounter;
        blackboxIdCounter++;
        blackbox = new Blackbox(id, selection);
        blackboxes.Add(blackbox);
      }
      return blackbox;
    }

    public void MarkBlackboxForRemoval(Blackbox blackbox)
    {
      lock (Instance)
      {
        blackboxesMarkedForRemoval.Add(blackbox);
      }
    }

    public void RemoveMarkedBlackboxes()
    {
      lock (Instance)
      {
        foreach (var blackbox in blackboxesMarkedForRemoval)
        {
          blackbox.Simulation?.EndBlackboxing();
          blackbox.Analysis?.Free();
          blackboxes.Remove(blackbox);
        }
        blackboxesMarkedForRemoval.Clear();
      }
    }

    public void SimulateBlackboxes()
    {
      foreach (var blackbox in blackboxes)
      {
        blackbox.Simulation?.Simulate();
      }
    }

    public void GameTick()
    {
      foreach (var blackbox in blackboxes)
      {
        blackbox.GameTick();
      }
      autoBlackbox.GameTick();
    }

    public void ClearAll()
    {
      lock (Instance)
      {
        foreach (var blackbox in blackboxes)
        {
          blackbox.Simulation?.EndBlackboxing();
          blackbox.Analysis?.Free();
        }
        blackboxes.Clear();
        blackboxesMarkedForRemoval.Clear();
        blackboxIdCounter = 1;
      }
      autoBlackbox.isActive = false;
      highlight.ClearHighlight();
    }

    const int saveLogicVersion = 1;

    public void PreserveVanillaSaveBefore()
    {
      RemoveMarkedBlackboxes();
      highlight.PreserveVanillaSaveBefore();
      for (int i = 0; i < blackboxes.Count; i++)
      {
        blackboxes[i].PreserveVanillaSaveBefore();
      }
    }

    public void PreserveVanillaSaveAfter()
    {
      highlight.PreserveVanillaSaveAfter();
      for (int i = 0; i < blackboxes.Count; i++)
      {
        blackboxes[i].PreserveVanillaSaveAfter();
      }
    }

    public void Export(BinaryWriter w)
    {
      w.Write(saveLogicVersion);
      w.Write(blackboxIdCounter);
      w.Write(blackboxes.Count);
      for (int i = 0; i < blackboxes.Count; i++)
      {
        blackboxes[i].Export(w);
      }
      w.Write(blackboxesMarkedForRemoval.Count);
      for (int i = 0; i < blackboxesMarkedForRemoval.Count; i++)
      {
        w.Write(blackboxesMarkedForRemoval[i].Id);
      }
      autoBlackbox.Export(w);
      highlight.Export(w);
    }

    public void Import(BinaryReader r)
    {
      var saveLogicVersion = r.ReadInt32();
      blackboxIdCounter = r.ReadInt32();
      var blackboxesCount = r.ReadInt32();
      blackboxes.Clear();
      blackboxes.Capacity = blackboxesCount;
      for (int i = 0; i < blackboxesCount; i++)
      {
        blackboxes.Add(Blackbox.Import(r));
      }

      var blackboxesMarkedForRemovalCount = r.ReadInt32();
      blackboxesMarkedForRemoval.Clear();
      blackboxesMarkedForRemoval.Capacity = blackboxesMarkedForRemovalCount;
      for (int i = 0; i < blackboxesMarkedForRemovalCount; i++)
      {
        var id = r.ReadInt32();
        var blackbox = blackboxes.Find(b => b.Id == id);
        blackboxesMarkedForRemoval.Add(blackbox);
      }
      autoBlackbox.Import(r);
      highlight.Import(r);
    }
  }

  class BlackboxPatch
  {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameStatData), nameof(GameStatData.GameTick))]
    public static void GameStatData__GameTick()
    {
      BlackboxManager.Instance.RemoveMarkedBlackboxes();
      BlackboxManager.Instance.GameTick();
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