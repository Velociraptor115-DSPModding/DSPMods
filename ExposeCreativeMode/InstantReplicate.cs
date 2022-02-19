/*
 * The code in this file is adapted from the sources of the excellent DSPCheats mod at
 * https://github.com/Windows10CE/DSPPlugins/blob/master/DSPCheats/Cheats/FreeHandcraft.cs
 * and
 * https://github.com/Windows10CE/DSPPlugins/blob/master/DSPCheats/Cheats/InstantHandcraft.cs
 *
 * The following notice(s) are inserted as per the license requirements
 */

/*
 * MIT License
 *
 * Copyright (c) 2021 Aaron Robinson
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public class InstantReplicate
  {
    private bool isEnabled;
    
    public bool IsEnabled
    {
      get => isEnabled;
      set
      {
        if (isEnabled == value)
          return;
        if (value) Enable(); else Disable();
      }
    }
    public bool IsInstant = true;
    public bool IsFree = true;
    public bool AllowAll = true;
    
    public void Enable()
    {
      isEnabled = true;
      Plugin.Log.LogDebug("Instant Replicate Enabled");
    }

    public void Disable()
    {
      isEnabled = false;
      Plugin.Log.LogDebug("Instant Replicate Disabled");
    }

    public void Toggle()
    {
      if (!isEnabled)
        Enable();
      else
        Disable();
    }
  }

  public static class InstantReplicatePatch
  {
    private static InstantReplicate instantReplicate;

    public static void Register(InstantReplicate instance)
    {
      instantReplicate = instance;
    }

    public static void Unregister(InstantReplicate instance)
    {
      if (instantReplicate == instance)
        instantReplicate = null;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ForgeTask), MethodType.Constructor, new Type[] { typeof(int), typeof(int) })]
    public static void ForgeTaskCreatePostfix(ref ForgeTask __instance)
    {
      if (instantReplicate is not { IsEnabled: true, IsInstant: true })
        return;

      __instance.tickSpend = 1;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.PredictTaskCount))]
    public static void MechaForgePredictTaskCountPrefix(ref int __result, ref int maxShowing, ref bool __runOriginal)
    {
      if (instantReplicate is not { IsEnabled: true, IsFree: true })
        return;
      
      __result = maxShowing;
      __runOriginal = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.TryAddTask))]
    public static void TryAddTaskPrefix(ref bool __result, ref bool __runOriginal)
    {
      if (instantReplicate is not { IsEnabled: true, IsFree: true })
        return;
      
      __result = true;
      __runOriginal = false;
    }

    public static int InterceptReplicateTaskTakeItems(StorageComponent instance, int itemId, int count, out int inc)
    {
      if (instantReplicate is { IsEnabled: true, IsFree: true })
      {
        inc = 0;
        return count;
      }
      
      return instance.TakeItem(itemId, count, out inc);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.AddTaskIterate))]
    public static IEnumerable<CodeInstruction>
      DontTakeItemsPatch(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
      var matcher = new CodeMatcher(code, generator);

      matcher.MatchForward(false,
        new CodeMatch(ci => ci.Calls(AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeItem))))
      );

      matcher.SetAndAdvance(OpCodes.Call,
        AccessTools.Method(typeof(InstantReplicatePatch), nameof(InterceptReplicateTaskTakeItems))
      );
      
      return matcher.InstructionEnumeration();
    }

    public static bool InterceptReplicateHandcraftCheck(RecipeProto instance)
      => instantReplicate is { IsEnabled: true, AllowAll: true } || instance.Handcraft;

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIReplicatorWindow), nameof(UIReplicatorWindow.OnOkButtonClick))]
    [HarmonyPatch(typeof(UIReplicatorWindow), nameof(UIReplicatorWindow._OnUpdate))]
    public static IEnumerable<CodeInstruction>
      ReplicatorOkButtonPatch(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
      var matcher = new CodeMatcher(code, generator);

      matcher.MatchForward(false,
        new CodeMatch(ci => ci.LoadsField(AccessTools.Field(typeof(UIReplicatorWindow), nameof(UIReplicatorWindow.selectedRecipe)))),
        new CodeMatch(ci => ci.LoadsField(AccessTools.Field(typeof(RecipeProto), nameof(RecipeProto.Handcraft))))
      );

      matcher.Advance(1);
      matcher.SetAndAdvance(OpCodes.Call,
        AccessTools.Method(typeof(InstantReplicatePatch), nameof(InterceptReplicateHandcraftCheck))
      );
      
      return matcher.InstructionEnumeration();
    }
  }
}