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
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  [BepInDependency(CommonAPIPlugin.GUID)]
  [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(CustomKeyBindSystem))]
  public class Plugin : BaseUnityPlugin
  {
    public const string GUID = "dev.raptor.dsp.ExposeCreativeMode";
    public const string NAME = "ExposeCreativeMode";
    public const string VERSION = "0.0.13";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(InfiniteInventoryPatch));
      _harmony.PatchAll(typeof(InfinitePowerPatch));
      _harmony.PatchAll(typeof(InfiniteReachPatch));
      _harmony.PatchAll(typeof(InfiniteResearchPatch));
      _harmony.PatchAll(typeof(InputHandlerPatch));
      CreativeModeLifecyclePatches.ApplyPatch(_harmony);
      KeyBinds.RegisterKeyBinds();
      Logger.LogInfo("ExposeCreativeMode Awake() called");
    }

    private void OnDestroy()
    {
      Logger.LogInfo("ExposeCreativeMode OnDestroy() called");
      CreativeModeLifecyclePatches.DestroyPatch();
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }
  }
  
  [HarmonyPatch]
  public static class CreativeModeLifecyclePatches
  {
    private static CreativeModeController instance;

    public static void ApplyPatch(Harmony harmony)
    {
      harmony.PatchAll(typeof(CreativeModeLifecyclePatches));
      var player = GameMain.mainPlayer;
      if (player != null)
        Create(ref player);
    }

    public static void DestroyPatch()
    {
      instance.Free();
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.Create))]
    static void Create(ref Player __result)
    {
      if (DSPGame.IsMenuDemo)
        return;
      
      if (instance != null)
        instance.Free();

      instance = new CreativeModeController();
      instance.Init(__result);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.GameTick))]
    static void GameTick()
    {
      instance?.GameTick();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.Free))]
    static void Free()
    {
      if (instance == null)
        return;
      
      instance.Free();
      instance = null;
    }
  }

  public delegate void InputUpdateHandler();

  [HarmonyPatch(typeof(VFInput), nameof(VFInput.OnUpdate))]
  class InputHandlerPatch
  {
    static void Postfix()
    {
      Update?.Invoke();
    }

    public static event InputUpdateHandler Update;
  }
  
  public class CreativeModeFunctions
  {
    public static void FlattenPlanet(PlanetFactory factory, bool bury, int modLevel)
    {
      var planet = factory.planet;
      if (planet == null || planet.type == EPlanetType.Gas)
        return;
      
      // var platformSystem = factory.platformSystem;
      // platformSystem.EnsureReformData();
      // for (int i = 0; i < platformSystem.maxReformCount; i++)
      //   if (!platformSystem.IsTerrainReformed(platformSystem.GetReformType(i)))
      //     platformSystem.SetReformType(i, 1);
      
      var modData = planet.data.modData;
      for (int i = 0; i < modData.Length; i++)
        modData[i] = (byte)((modLevel & 3) | ((modLevel & 3) << 4));
      
      var dirtyFlags = planet.dirtyFlags;
      for (int i = 0; i < dirtyFlags.Length; i++)
        dirtyFlags[i] = true;
      planet.landPercentDirty = true;
      
      if (planet.UpdateDirtyMeshes())
        factory.RenderLocalPlanetHeightmap();
      
      var vegePool = factory.vegePool;
      float groundLevel = planet.realRadius + 0.2f;
      var isFlattened = (modLevel & 3) == 3;
      for (int n = 1; n < factory.vegeCursor; n++)
      {
        var currentPos = vegePool[n].pos;
        var vegeGroundLevel =
          isFlattened ?
            groundLevel :
            planet.data.QueryModifiedHeight(currentPos) - 0.13f;
        vegePool[n].pos = currentPos.normalized * vegeGroundLevel;
        GameMain.gpuiManager.AlterModel((int)vegePool[n].modelIndex, vegePool[n].modelId, n, vegePool[n].pos,
          vegePool[n].rot, false);
      }
      
      ModifyAllVeinsHeight(factory, bury);
    }
    
    public static void ResearchCurrentTechInstantly()
    {
      var history = GameMain.history;
      if (history.currentTech > 0)
      {
        var techState = history.TechState(history.currentTech);
        var hashNeeded = techState.hashNeeded - techState.hashUploaded;
        history.AddTechHash(hashNeeded);
      }
    }
    
    public static void ModifyAllVeinsHeight(PlanetFactory factory, bool bury)
    {
      var planet = factory.planet;
      var physics = planet.physics;
      var veinPool = factory.veinPool;
      for (int i = 1; i < factory.veinCursor; i++)
      {
        var veinPoolPos = veinPool[i].pos;
        var veinColliderId = veinPool[i].colliderId;
        var heightToSet = bury ? planet.realRadius - 50f : planet.data.QueryModifiedHeight(veinPool[i].pos) - 0.13f;
        physics.colChunks[veinColliderId >> 20].colliderPool[veinColliderId & 0xFFFFF].pos = physics.GetColliderData(veinColliderId).pos.normalized * (heightToSet + 0.4f);
        veinPool[i].pos = veinPoolPos.normalized * heightToSet;
        physics.SetPlanetPhysicsColliderDirty();
        GameMain.gpuiManager.AlterModel(veinPool[i].modelIndex, veinPool[i].modelId, i, veinPool[i].pos, false);
      }
      GameMain.gpuiManager.SyncAllGPUBuffer();
    }
  }
}