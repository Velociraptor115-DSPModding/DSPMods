using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace DysonSphereProgram.Modding.Blackbox
{
  public abstract class BlackboxAnalysis
  {
    internal Blackbox blackbox;
    internal WeakReference<PlanetFactory> factoryRef;
    public BlackboxAnalysis(Blackbox blackbox)
    {
      this.blackbox = blackbox;
      this.factoryRef = blackbox.FactoryRef;
    }

    public abstract BlackboxRecipe EffectiveRecipe { get; } 
    public abstract void Begin();
    public abstract void Free();
  }
}