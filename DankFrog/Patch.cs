using HarmonyLib;

namespace DysonSphereProgram.Modding.DankFrog;

[HarmonyPatch]
public class Patch
{
    private static string ReplaceDarkFogWithDankFrog(string value)
    {
        return value
            .Replace("dark fog", "dank frog")
            .Replace("dark Fog", "dank Frog")
            .Replace("Dark fog", "Dank frog")
            .Replace("Dark Fog", "Dank Frog")
            ;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Localization), nameof(Localization.Translate))]
    public static void ReplaceDarkFogWithDankFrogTranslate(ref string __result)
    {
        __result = ReplaceDarkFogWithDankFrog(__result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIMainMenu), nameof(UIMainMenu.UpdateLogText))]
    public static void ReplaceDarkFogWithDankFrogUpdateLog(UIMainMenu __instance)
    {
        for (int i = 0; i < __instance.updateLogs.Length; i++)
        {
            var logs = __instance.updateLogs[i].logs;
            for (int j = 0; j < logs.Length; j++)
                logs[i].logEn = ReplaceDarkFogWithDankFrog(logs[i].logEn);
        }
    }
    
    
}