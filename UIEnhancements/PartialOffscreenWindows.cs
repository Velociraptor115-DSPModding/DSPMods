using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;

namespace DysonSphereProgram.Modding.UIEnhancements;

public class PartialOffscreenWindows: EnhancementBase
{
  public static ConfigEntry<int> thresholdWidth;
  public static ConfigEntry<int> thresholdHeight;

  private static Vector2 thresholdVector => new Vector2(thresholdWidth.Value, thresholdHeight.Value);
  
  [HarmonyTranspiler]
  [HarmonyPatch(typeof(UIWindowDrag), nameof(UIWindowDrag.Update))]
  static IEnumerable<CodeInstruction> PatchUpdate(IEnumerable<CodeInstruction> code, ILGenerator generator)
  {
    var matcher = new CodeMatcher(code, generator);

    TranspilerHelpers.LocalHelper? minLocal = null, maxLocal = null;
    matcher.MatchForward(true
      , new (ci => ci.Calls(AccessTools.Method(typeof(UIRoot), nameof(UIRoot.WorldToScreenPoint))))
      , new (ci => ci.StoresLocal(out minLocal))
    );
    
    if (matcher.IsInvalid)
      Plugin.Log.LogDebug("Invalid on the first stloc");
    
    Plugin.Log.LogDebug(matcher.Opcode);
    Plugin.Log.LogDebug(matcher.Pos);

    matcher.MatchForward(true
      , new (ci => ci.Calls(AccessTools.Method(typeof(UIRoot), nameof(UIRoot.WorldToScreenPoint))))
      , new (ci => ci.StoresLocal(out maxLocal))
    );
    
    if (matcher.IsInvalid)
      Plugin.Log.LogDebug("Invalid on the second stloc");
    
    Plugin.Log.LogDebug(matcher.Opcode);
    Plugin.Log.LogDebug(matcher.Pos);

    matcher.Advance(1);
    
    Plugin.Log.LogDebug(minLocal);
    Plugin.Log.LogDebug(maxLocal);

    matcher.Insert(
      minLocal?.Ldloca(),
      maxLocal?.Ldloca(),
      CodeInstruction.Call(typeof(PartialOffscreenWindows), nameof(PartialOffscreenWindows.AdjustWindowRectParams))
    );

    return matcher.InstructionEnumeration();
  }

  public static void AdjustWindowRectParams(ref Vector2 pos1, ref Vector2 pos2)
  {
    if (pos1.x < pos2.x)
    {
      var tmp = pos2.x;
      pos2.x = pos1.x + thresholdVector.x;
      pos1.x = tmp - thresholdVector.x;
    }
    else
    {
      var tmp = pos1.x;
      pos1.x = pos2.x + thresholdVector.x;
      pos2.x = tmp - thresholdVector.x;
    }
    
    if (pos1.y > pos2.y)
    {
      var tmp = pos2.y;
      pos2.y = pos1.y - thresholdVector.y;
      pos1.y = tmp + thresholdVector.y;
    }
    else
    {
      var tmp = pos1.y;
      pos1.y = pos2.y - thresholdVector.y;
      pos2.y = tmp + thresholdVector.y;
    }
  }
  
  protected override void UseConfig(ConfigFile configFile)
  {
    thresholdWidth = configFile.Bind(ConfigSection, "Threshold Width", 100);
    thresholdHeight = configFile.Bind(ConfigSection, "Threshold Height", 100);
  }
  protected override void Patch(Harmony _harmony)
  {
    _harmony.PatchAll(typeof(PartialOffscreenWindows));
  }
  protected override void Unpatch()
  {
    
  }
  protected override void CreateUI()
  {
    
  }
  protected override void DestroyUI()
  {
    
  }
  protected override string Name => "Partial Off-screen Windows";
}