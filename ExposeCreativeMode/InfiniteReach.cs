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
  public class InfiniteReach
  {
    private readonly Player player;
    private float? buildAreaRestore;
    
    public bool IsEnabled;

    public InfiniteReach(Player player)
    {
      this.player = player;
    }
    
    public void Enable()
    {
      buildAreaRestore = player.mecha.buildArea;
      player.mecha.buildArea = 600;

      IsEnabled = true;
      Plugin.Log.LogDebug("Infinite Reach Enabled");
    }

    public void Disable()
    {
      player.mecha.buildArea = buildAreaRestore.GetValueOrDefault(Configs.freeMode.mechaBuildArea);

      IsEnabled = false;
      Plugin.Log.LogDebug("Infinite Reach Disabled");
    }

    public void Toggle()
    {
      if (!IsEnabled)
        Enable();
      else
        Disable();
    }
    
  }

  [HarmonyPatch]
  public static class InfiniteReachPatch
  {
    private static InfiniteReach infiniteReach;

    public static void Register(InfiniteReach instance)
    {
      infiniteReach = instance;
    }

    public static void Unregister(InfiniteReach instance)
    {
      if (infiniteReach == instance)
        infiniteReach = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerAction_Inspect), nameof(PlayerAction_Inspect.GetObjectSelectDistance))]
    static void PatchObjectSelectDistance(ref float __result, EObjectType objType, int objid)
    {
      var isInfiniteReachActive = infiniteReach?.IsEnabled ?? false;
      if (!isInfiniteReachActive)
        return;

      __result = 600;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    static void BeforeSaveCurrentGame(ref bool __state)
    {
      __state = false;
      if (infiniteReach != null && infiniteReach.IsEnabled)
      {
        __state = true;
        infiniteReach.Disable();
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    static void AfterSaveCurrentGame(ref bool __state)
    {
      if (__state && infiniteReach != null)
      {
        infiniteReach.Enable();
      }
    }
  }
}