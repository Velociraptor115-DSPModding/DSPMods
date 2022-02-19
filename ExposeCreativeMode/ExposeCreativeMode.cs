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
      CreativeModeConfig.Init(Config);
      _harmony.PatchAll(typeof(InfiniteInventoryPatch));
      _harmony.PatchAll(typeof(InfinitePowerPatch));
      _harmony.PatchAll(typeof(InfiniteReachPatch));
      _harmony.PatchAll(typeof(InstantResearchPatch));
      _harmony.PatchAll(typeof(InstantReplicatePatch));
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
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(VFInput), nameof(VFInput.OnUpdate))]
    static void InputUpdate()
    {
      instance?.OnInputUpdate();
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
}