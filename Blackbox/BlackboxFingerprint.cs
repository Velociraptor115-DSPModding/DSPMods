using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using HarmonyLib;

namespace DysonSphereProgram.Modding.Blackbox
{ 
  public class BlackboxFingerprint
  {
    private BlackboxSelection selection;

    private BlackboxFingerprint(BlackboxSelection selection)
    {
      this.selection = selection;
    }

    public static BlackboxFingerprint CreateFrom(BlackboxSelection selection)
    {
      return new BlackboxFingerprint(selection);
    }

    public override bool Equals(object obj)
    {
      var otherFingerprint = obj as BlackboxFingerprint;
      if (obj == null)
        return false;

      if (this.selection.factoryIndex == otherFingerprint.selection.factoryIndex)
      {
        if (this.selection.entityIds.SetEquals(otherFingerprint.selection.entityIds))
          return true;
      }
      return false;
    }

    public override int GetHashCode()
    {
      return selection.entityIds.GetHashCode();
    }
  }
}