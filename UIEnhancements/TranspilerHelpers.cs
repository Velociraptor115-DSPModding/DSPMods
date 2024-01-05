using System;
using System.Reflection.Emit;
using HarmonyLib;

namespace DysonSphereProgram.Modding.UIEnhancements;

public static class TranspilerHelpers
{
  private static int GetLocalIdxFromOperand(object operand)
  {
    if (operand is LocalBuilder builder)
      return builder.LocalIndex;
    try
    {
      return Convert.ToInt32(operand);
    }
    catch (Exception)
    {
      Plugin.Log.LogError("Failed to convert operand to int: " + operand);
      Plugin.Log.LogDebug("The type of local operand is " + operand.GetType());
      throw;
    }
  }
  
  public static bool LoadsLocal(this CodeInstruction code, out LocalHelper? local)
  {
    local = null;
    if (!code.IsLdloc())
      return false;
    if (code.opcode == OpCodes.Ldloc_0)
      local = new LocalHelper(0);
    else if (code.opcode == OpCodes.Ldloc_1)
      local = new LocalHelper(1);
    else if (code.opcode == OpCodes.Ldloc_2)
      local = new LocalHelper(2);
    else if (code.opcode == OpCodes.Ldloc_3)
      local = new LocalHelper(3);
    else if (code.opcode == OpCodes.Ldloc_S || code.opcode == OpCodes.Ldloca_S)
      local = new LocalHelper(GetLocalIdxFromOperand(code.operand));
    else if (code.opcode == OpCodes.Ldloc || code.opcode == OpCodes.Ldloca)
      local = new LocalHelper(GetLocalIdxFromOperand(code.operand));
    return true;
  }
  
  public static bool StoresLocal(this CodeInstruction code, out LocalHelper? local)
  {
    local = null;
    if (!code.IsStloc())
      return false;
    if (code.opcode == OpCodes.Stloc_0)
      local = new LocalHelper(0);
    else if (code.opcode == OpCodes.Stloc_1)
      local = new LocalHelper(1);
    else if (code.opcode == OpCodes.Stloc_2)
      local = new LocalHelper(2);
    else if (code.opcode == OpCodes.Stloc_3)
      local = new LocalHelper(3);
    else if (code.opcode == OpCodes.Stloc_S)
      local = new LocalHelper(GetLocalIdxFromOperand(code.operand));
    else if (code.opcode == OpCodes.Stloc)
      local = new LocalHelper(GetLocalIdxFromOperand(code.operand));
    return true;
  }

  public readonly record struct LocalHelper(int localIdx)
  {
    public CodeInstruction Ldloc()
      => localIdx switch
      {
        0 => new CodeInstruction(OpCodes.Ldloc_0),
        1 => new CodeInstruction(OpCodes.Ldloc_1),
        2 => new CodeInstruction(OpCodes.Ldloc_2),
        3 => new CodeInstruction(OpCodes.Ldloc_3),
        <= 127 => new CodeInstruction(OpCodes.Ldloc_S, (sbyte)localIdx),
        > 127 => new CodeInstruction(OpCodes.Ldloc, localIdx)
      };
    
    public CodeInstruction Ldloca()
      => localIdx switch
      {
        <= 127 => new CodeInstruction(OpCodes.Ldloca_S, (sbyte)localIdx),
        > 127 => new CodeInstruction(OpCodes.Ldloca, localIdx)
      };
    
    public CodeInstruction Stloc()
      => localIdx switch
      {
        0 => new CodeInstruction(OpCodes.Stloc_0),
        1 => new CodeInstruction(OpCodes.Stloc_1),
        2 => new CodeInstruction(OpCodes.Stloc_2),
        3 => new CodeInstruction(OpCodes.Stloc_3),
        <= 127 => new CodeInstruction(OpCodes.Stloc_S, (sbyte)localIdx),
        > 127 => new CodeInstruction(OpCodes.Stloc, localIdx)
      };
  }
}

