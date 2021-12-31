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