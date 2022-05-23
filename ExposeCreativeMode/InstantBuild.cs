using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using CommonAPI;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public class InstantBuild
  {
    private readonly Player player;
    
    private bool isEnabled;
    public bool IsEnabled
    {
      get => isEnabled;
      set
      {
        if (isEnabled == value)
          return;
        if (value) Enable(); else Disable();
      }
    }
    
    public InstantBuild(Player player)
    {
      this.player = player;
    }

    public void Enable()
    {
      isEnabled = true;
      Plugin.Log.LogDebug("Instant Build Enabled");
    }

    public void Disable()
    {
      isEnabled = false;
      Plugin.Log.LogDebug("Instant Build Disabled");
    }

    public void Toggle()
    {
      if (!isEnabled)
        Enable();
      else
        Disable();
    }

    public void GameTick(bool skipInventory)
    {
      if (!isEnabled)
        return;

      var factory = player.factory;
      if (factory == null || factory.prebuildCount == 0)
        return;

      var startIdx = 1;

      // This means that we can probably get away with just looking at the recycle instances
      if (factory.prebuildRecycleCursor > 0)
        startIdx = factory.prebuildRecycleCursor;

      var endIdx = factory.prebuildCursor;
      var prebuildRecycle = factory.prebuildRecycle;
      var prebuildPool = factory.prebuildPool;
      var playerPackage = player.package;

      for (int i = startIdx; i < endIdx; i++)
      {
        var prebuildIdx = prebuildRecycle[i] > 0 ? prebuildRecycle[i] : i;
        ref var prebuild = ref prebuildPool[prebuildIdx];
        
        if (prebuild.id != prebuildIdx)
          continue;
        
        if (skipInventory)
        {
          factory.BuildFinally(player, prebuild.id);
          continue;
        }

        if (prebuild.itemRequired > 0)
        {
          int protoId = prebuild.protoId;
          int itemRequired = prebuild.itemRequired;
          playerPackage.TakeTailItems(ref protoId, ref itemRequired, out _, false);
          prebuild.itemRequired -= itemRequired;
        }
        if (prebuild.itemRequired <= 0)
        {
          factory.BuildFinally(player, prebuild.id);
        }
      }
    }
  }
}