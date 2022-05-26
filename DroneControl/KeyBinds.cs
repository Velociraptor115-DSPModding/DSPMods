using CommonAPI.Systems;
using UnityEngine;

namespace DysonSphereProgram.Modding.DroneControl;

public record KeyBind(string Id, string Description, CombineKey DefaultBinding, int ConflictGroup)
{
  public bool IsActive => CustomKeyBindSystem.GetKeyBind(Id)?.keyValue ?? false;
}

public static class KeyBinds
{
  public static readonly KeyBind ToggleDroneControl = new(
    nameof(ToggleDroneControl)
    , "Toggle Drone Control"
    , new CombineKey((int)KeyCode.B, CombineKey.CTRL_COMB | CombineKey.SHIFT_COMB, ECombineKeyAction.OnceClick, false)
    , 4095
  );

  private static readonly KeyBind[] keyBinds = new KeyBind[]
  {
    ToggleDroneControl
  };

  public static void RegisterKeyBinds()
  {
    foreach (var keyBind in keyBinds)
    {
      if (!CustomKeyBindSystem.HasKeyBind(keyBind.Id))
      {
        var builtinKey = new BuiltinKey
        {
          name = keyBind.Id,
          id = 0,
          key = keyBind.DefaultBinding,
          canOverride = true,
          conflictGroup = keyBind.ConflictGroup
        };
        if (builtinKey.key.action == ECombineKeyAction.LongPress)
          CustomKeyBindSystem.RegisterKeyBind<HoldKeyBind>(builtinKey);
        else
          CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(builtinKey);
        ProtoRegistry.RegisterString("KEY" + keyBind.Id, keyBind.Description);
      }
    }
  }
}