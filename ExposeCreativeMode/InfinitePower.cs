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
  public interface IInfinitePowerProvider
  {
    bool IsEnabled { get; }
  }

  [HarmonyPatch]
  public static class InfinitePowerPatch
  {
    private static IInfinitePowerProvider provider;

    public static void Register(IInfinitePowerProvider p)
    {
      provider = p;
    }

    public static void Unregister(IInfinitePowerProvider p)
    {
      if (provider == p)
        provider = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
    static void PatchNetworkServes(PowerSystem __instance, bool isActive)
    {
      var isInfinitePowerEnabled = provider?.IsEnabled ?? false;
      if (!isInfinitePowerEnabled)
        return;

      for (int i = 1; i < __instance.netCursor; i++)
      {
        __instance.networkServes[i] = 1;

        // Reset the no / low power signs
        if (isActive)
        {
          var entitySignPool = __instance.factory.entitySignPool;
          var powerNetwork = __instance.netPool[i];
          if (powerNetwork != null && powerNetwork.id == i)
          {
            var consumers = powerNetwork.consumers;
            for (int j = 0; j < consumers.Count; j++)
            {
              entitySignPool[__instance.consumerPool[consumers[j]].entityId].signType = 0U;
            }
          }
        }
      }
    }
  }
}