using HarmonyLib;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  [HarmonyPatch]
  public static class OrbitalMechanicsOverride
  {
    public static long Offset;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GalaxyData), nameof(GalaxyData.UpdatePoses))]
    static void ApplyOffset(ref double time)
    {
      time += Offset;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.Destroy))]
    public static void ResetOffset()
    {
      Offset = 0;
    }

    public static void PreserveVanillaSaveBefore()
    {
      var tmp = Offset;
      Offset = 0;
      GameMain.universeSimulator.galaxyData.UpdatePoses(GameMain.instance.timef);
      GameMain.data.DetermineRelative();
      GameMain.mainPlayer.controller.UpdatePhysicsDirect();
      Offset = tmp;
    }

    public static void PreserveVanillaSaveAfter()
    {
      
    }
  }
}