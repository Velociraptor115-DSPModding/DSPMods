using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using crecheng.DSPModSave;
using HarmonyLib;

namespace DysonSphereProgram.Modding.DroneControl
{
  [BepInPlugin(GUID, NAME, VERSION)]
  [BepInProcess("DSPGAME.exe")]
  [BepInDependency(CommonAPIPlugin.GUID)]
  [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(CustomKeyBindSystem))]
  public class Plugin : BaseUnityPlugin, IModCanSave
  {
    public const string GUID = "dev.raptor.dsp.DroneControl";
    public const string NAME = "DroneControl";
    public const string VERSION = "1.0.0";

    private Harmony _harmony;
    internal static ManualLogSource Log;

    private void Awake()
    {
      Plugin.Log = Logger;
      _harmony = new Harmony(GUID);
      _harmony.PatchAll(typeof(PatchController));
      KeyBinds.RegisterKeyBinds();
      Plugin.Log.LogInfo(nameof(Modding.DroneControl) + " Awake() called");
    }

    private void OnDestroy()
    {
      Plugin.Log.LogInfo(nameof(Modding.DroneControl) + " OnDestroy() called");
      if (UIManager.Patched)
        UIManager.DestroyUI();
      _harmony?.UnpatchSelf();
      Plugin.Log = null;
    }

    private void Update()
    {
      if (KeyBinds.ToggleDroneControl.IsActive)
      {
        PatchController.SetDisableDrones(!PatchController.DisableDrones);
      }
    }

    public void Export(BinaryWriter w) => PatchController.Export(w);
    public void Import(BinaryReader r) => PatchController.Import(r);
    public void IntoOtherSave() => PatchController.IntoOtherSave();
  }
  
  [HarmonyPatch]
  public static class PatchController
  {
    public static bool DisableDrones { get; private set; } = false;

    public static void SetDisableDrones(bool value)
    {
      DisableDrones = value;
      UIManager.RefreshUI();
    }
  
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MechaDroneLogic), nameof(MechaDroneLogic.UpdateTargets))]
    static void SkipIfNecessary(ref bool __runOriginal)
    {
      if (!__runOriginal)
        return;
    
      if (!UIManager.Patched)
        UIManager.CreateUI();

      if (DisableDrones)
        __runOriginal = false;
    }
  
    private const int saveLogicVersion = 1;
  
    public static void Export(BinaryWriter w)
    {
      w.Write(saveLogicVersion);
      w.Write(DisableDrones);
    }
    public static void Import(BinaryReader r)
    {
      var saveLogicVersion = r.ReadInt32();
      SetDisableDrones(r.ReadBoolean());
    }
    public static void IntoOtherSave()
    {
      SetDisableDrones(false);
    }
  }
}

namespace System.Runtime.CompilerServices
{
  public record IsExternalInit;
}