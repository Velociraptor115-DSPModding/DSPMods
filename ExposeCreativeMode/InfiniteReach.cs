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
  public interface IInfiniteReachProvider
  {
    bool IsEnabled { get; }
    void Enable();
    void Disable();
  }

  [HarmonyPatch]
  public static class InfiniteReachPatch
  {
    private static IInfiniteReachProvider provider;

    public static void Register(IInfiniteReachProvider p)
    {
      provider = p;
    }

    public static void Unregister(IInfiniteReachProvider p)
    {
      if (provider == p)
        provider = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerAction_Inspect), nameof(PlayerAction_Inspect.GetObjectSelectDistance))]
    static void PatchObjectSelectDistance(ref float __result, EObjectType objType, int objid)
    {
      var isInfiniteReachActive = provider?.IsEnabled ?? false;
      if (!isInfiniteReachActive)
        return;

      __result = 600;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    static void BeforeSaveCurrentGame(ref bool __state)
    {
      __state = false;
      if (provider != null && provider.IsEnabled)
      {
        __state = true;
        provider.Disable();
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    static void AfterSaveCurrentGame(ref bool __state)
    {
      if (__state && provider != null)
      {
        provider.Enable();
      }
    }
  }
}