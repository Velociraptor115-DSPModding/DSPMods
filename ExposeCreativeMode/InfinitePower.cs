using System;
using System.IO;
using System.Linq;
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
  public class InfinitePower
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

    public void Enable()
    {
      isEnabled = true;
      Plugin.Log.LogDebug("Infinite Power Enabled");
    }

    public void Disable()
    {
      isEnabled = false;
      Plugin.Log.LogDebug("Infinite Power Disabled");
    }

    public void Toggle()
    {
      if (!isEnabled)
        Enable();
      else
        Disable();
    }
  }

  [HarmonyPatch]
  public static class InfinitePowerPatch
  {
    private static InfinitePower infinitePower;

    public static void Register(InfinitePower instance)
    {
      infinitePower = instance;
    }

    public static void Unregister(InfinitePower instance)
    {
      if (infinitePower == instance)
        infinitePower = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
    static void PatchNetworkServes(PowerSystem __instance, bool isActive)
    {
      var isInfinitePowerEnabled = infinitePower?.IsEnabled ?? false;
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

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MonitorComponent), nameof(MonitorComponent.InternalUpdate))]
    [HarmonyPatch(typeof(PilerComponent), nameof(PilerComponent.InternalUpdate))]
    [HarmonyPatch(typeof(SpraycoaterComponent), nameof(SpraycoaterComponent.InternalUpdate))]
    static IEnumerable<CodeInstruction> ReplaceConsumerRatioWithNetworkServes(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
      var originalCode = new List<CodeInstruction>(code);
      var matcher = new CodeMatcher(code, generator);
      
      var fldConsumerRatio = AccessTools.Field(typeof(PowerNetwork), nameof(PowerNetwork.consumerRatio));
      var fldNetPool = AccessTools.Field(typeof(PowerSystem), nameof(PowerSystem.netPool));
      var fldNetworkServes = AccessTools.Field(typeof(PowerSystem), nameof(PowerSystem.networkServes));

      static CodeMatch matchLdLocToExtractLocal(string name) => new CodeMatch(ci => ci.IsLdloc(), name: name);
      static CodeMatch matchStLocToExtractLocal(string name) => new CodeMatch(ci => ci.IsStloc(), name: name);

      const string matchPowerNetworkLocal = nameof(matchPowerNetworkLocal);
      const string matchNetworkIdLocal = nameof(matchNetworkIdLocal);

      var matchStlocToExtractPowerNetworkLocal = matchStLocToExtractLocal(matchPowerNetworkLocal);
      var matchLdlocToExtractNetworkIdLocal = matchLdLocToExtractLocal(matchNetworkIdLocal);

      matcher.MatchForward(
        false
        , new CodeMatch(OpCodes.Ldfld, fldNetPool)
        , matchLdlocToExtractNetworkIdLocal
        , new CodeMatch(OpCodes.Ldelem_Ref)
        , matchStlocToExtractPowerNetworkLocal
      );

      if (matcher.IsInvalid)
        return originalCode;

      var ldNetPoolEndPos = matcher.Pos;

      var ldNetworkIdInstruction = matcher.NamedMatch(matchNetworkIdLocal).Clone();

      // Get to the variable from which we get the netPool
      matcher.MatchBack(false, new CodeMatch(ci => ci.IsLdloc() || ci.IsLdarg()));

      if (matcher.IsInvalid)
        return originalCode;

      var ldNetPoolStartPos = matcher.Pos;

      var codeToLoadNetworkServes = (
        from x in matcher.InstructionsInRange(ldNetPoolStartPos, ldNetPoolEndPos)
        select x.Is(OpCodes.Ldfld, fldNetPool) ? new CodeInstruction(OpCodes.Ldfld, fldNetworkServes) : x.Clone()
      ).ToList();

      codeToLoadNetworkServes.Add(ldNetworkIdInstruction);
      codeToLoadNetworkServes.Add(new CodeInstruction(OpCodes.Ldelem_R4));

      matcher.MatchForward(
        false
        , new CodeMatch(ci => ci.IsLdloc())
        , new CodeMatch(OpCodes.Ldfld, fldConsumerRatio)
        , new CodeMatch(OpCodes.Conv_R4)
      );

      if (matcher.IsInvalid)
        return originalCode;

      matcher.SetAndAdvance(codeToLoadNetworkServes[0].opcode, codeToLoadNetworkServes[0].operand);
      matcher.SetInstructionAndAdvance(codeToLoadNetworkServes[1]);
      matcher.SetInstructionAndAdvance(codeToLoadNetworkServes[2]);
      matcher.Insert(codeToLoadNetworkServes.Skip(3));

      return matcher.InstructionEnumeration();
    }
  }
}