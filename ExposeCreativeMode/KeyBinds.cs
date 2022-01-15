using System.Collections.Generic;
using UnityEngine;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public static class KeyBinds
  {
    public const string ToggleCreativeMode = nameof(ToggleCreativeMode);
    public const string ToggleInfiniteInventory = nameof(ToggleInfiniteInventory);
    public const string ToggleInfiniteStation = nameof(ToggleInfiniteStation);
    public const string ToggleInstantBuild = nameof(ToggleInstantBuild);
    public const string FlattenPlanet = nameof(FlattenPlanet);
    public const string UnlockAllPublishedTech = nameof(UnlockAllPublishedTech);
    public const string ToggleInstantResearch = nameof(ToggleInstantResearch);
    public const string ToggleAllVeinsOnPlanet = nameof(ToggleAllVeinsOnPlanet);

    private static List<string> keyBinds = new List<string>
    {
      ToggleCreativeMode,
      ToggleInfiniteInventory,
      ToggleInfiniteStation,
      ToggleInstantBuild,
      FlattenPlanet,
      UnlockAllPublishedTech,
      ToggleInstantResearch,
      ToggleAllVeinsOnPlanet,
    };

    private static List<CombineKey> defaultBindings = new List<CombineKey>
    {
      new CombineKey((int)KeyCode.F4, CombineKey.SHIFT_COMB, ECombineKeyAction.OnceClick, false),
      new CombineKey((int)KeyCode.Keypad1, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
      new CombineKey((int)KeyCode.Keypad0, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
      new CombineKey((int)KeyCode.Keypad2, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
      new CombineKey((int)KeyCode.Keypad3, 0, ECombineKeyAction.OnceClick, false),
      new CombineKey((int)KeyCode.T, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
      new CombineKey((int)KeyCode.Keypad6, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
      new CombineKey((int)KeyCode.Keypad4, 0, ECombineKeyAction.OnceClick, false),
    };

    private static List<string> keyBindDescriptions = new List<string>
    {
      "Toggle Creative Mode",
      "(Creative Mode) Toggle Infinite Inventory",
      "(Creative Mode) Toggle Infinite Station",
      "(Creative Mode) Toggle Instant Build",
      "(Creative Mode) Flatten Planet",
      "(Creative Mode) Unlock all Tech",
      "(Creative Mode) Toggle Instant Research",
      "(Creative Mode) Toggle All Veins on Planet",
    };

    private static int keyBindId(string keyBind) => keyBinds.IndexOf(keyBind) + 200;

    public static void RegisterKeyBinds()
    {
      foreach (var keyBind in keyBinds)
      {
        if (!CustomKeyBindSystem.HasKeyBind(keyBind))
        {
          CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(new BuiltinKey
          {
            name = keyBind,
            id = keyBindId(keyBind),
            key = defaultBindings[keyBinds.IndexOf(keyBind)],
            canOverride = true,
            conflictGroup = 4095 // I have no idea what this is, but to be on the safer side
                                 // I'm going to make it conflict with everything that isn't a mouse key
          });
          ProtoRegistry.RegisterString("KEY" + keyBind, keyBindDescriptions[keyBinds.IndexOf(keyBind)]);
        }
      }
    }
  }
}