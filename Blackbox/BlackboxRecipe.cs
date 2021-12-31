using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace DysonSphereProgram.Modding.Blackbox
{
  public class BlackboxRecipe
  {
    public long idleEnergyPerTick;
    public long workingEnergyPerTick;
    public int timeSpend;
    // TODO: The "StationId" here needs to be linked to the Fingerprint and then through to the Selection
    // Dictionary<StationId, Dictionary<ItemId, CountPerCycle>>
    public Dictionary<int, Dictionary<int, int>> inputs;
    public Dictionary<int, Dictionary<int, int>> outputs;
    // Dictionary<ItemId, CountPerCycle>
    public Dictionary<int, int> produces;
    public Dictionary<int, int> consumes;
  }
}