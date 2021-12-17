using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace DysonSphereProgram.Modding.Blackbox
{
  public abstract class BlackboxAnalysis
  {
    protected PlanetFactory factory;
    protected ICollection<int> entityIds;
    public BlackboxAnalysis(PlanetFactory factory, ICollection<int> entityIds)
    {
      this.factory = factory;
      this.entityIds = entityIds;
    }

    public abstract BlackboxStatus Status { get; }
    public abstract BlackboxRecipe EffectiveRecipe { get; } 
    public PlanetFactory Factory { get => factory; }

    public int id;
    public abstract void Begin();
  }
}