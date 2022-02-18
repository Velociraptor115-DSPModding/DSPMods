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
    
    public bool IsEnabled;
    
    public InstantBuild(Player player)
    {
      this.player = player;
    }

    public void Enable()
    {
      IsEnabled = true;
      Plugin.Log.LogDebug("Instant Build Enabled");
    }

    public void Disable()
    {
      IsEnabled = false;
      Plugin.Log.LogDebug("Instant Build Disabled");
    }

    public void Toggle()
    {
      if (!IsEnabled)
        Enable();
      else
        Disable();
    }

    public void GameTick()
    {
      if (!IsEnabled)
        return;

      if (player.factory == null || player.factory.prebuildCount == 0)
        return;

      void BuildInstantly(int prebuildIdx)
      {
        ref var prebuild = ref player.factory.prebuildPool[prebuildIdx];
        if (prebuild.id != prebuildIdx)
          return;
        if (prebuild.itemRequired > 0)
        {
          int protoId = prebuild.protoId;
          int itemRequired = prebuild.itemRequired;
          player.package.TakeTailItems(ref protoId, ref itemRequired, out _, false);
          prebuild.itemRequired -= itemRequired;
          player.factory.AlterPrebuildModelState(prebuildIdx);
        }
        if (prebuild.itemRequired <= 0)
        {
          player.factory.BuildFinally(player, prebuild.id);
        }
      }

      if (player.factory.prebuildRecycleCursor > 0)
      {
        // This means that we can probably get away with just looking at the recycle instances
        for (int i = player.factory.prebuildRecycleCursor; i < player.factory.prebuildCursor; i++)
          BuildInstantly(player.factory.prebuildRecycle[i]);
      }
      else
      {
        // Highly probable that a prebuildPool resize took place this tick.
        // Better to go over the entire array

        // Don't ask me why the loop starts from 1. I'm merely following `MechaDroneLogic.UpdateTargets()`
        for (int i = 1; i < player.factory.prebuildCursor; i++)
          BuildInstantly(i);
      }
    }
  }
}