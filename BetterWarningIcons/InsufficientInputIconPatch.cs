using System.IO;
using System.Linq;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using BepInEx.Configuration;

namespace DysonSphereProgram.Modding.BetterWarningIcons
{
  public static class InsufficientInputIconPatch
  {
    public const string ConfigSection = "Insufficient Input Warning";
    public static ConfigEntry<bool> enablePatch;

    public static void InitConfig(ConfigFile confFile)
    {
      enablePatch = confFile.Bind(ConfigSection, "Enable Patch", true, ConfigDescription.Empty);
    }

    public static Texture entitySignRendererTextureOriginal;
    public static Texture2D entitySignRendererTextureModified;

    public static Texture2D insufficientInputTex;
    public static Texture2D insufficientInputTexSmall;

    public static Sprite iconSprite;

    public static bool contentLoaded = false;

    const int texAtlasEntrySize = 256;

    public const uint INSUFFICIENT_INPUT_SIGN_TYPE = 31U;
    public const int InsufficientInputSignalId = (int)(500 + INSUFFICIENT_INPUT_SIGN_TYPE);

    static void EnsureLoaded()
    {
      if (contentLoaded)
        return;

      insufficientInputTex = new Texture2D(1, 1, TextureFormat.RGBA32, true);
      insufficientInputTexSmall = new Texture2D(1, 1, TextureFormat.RGBA32, true);
      var data = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Plugin.Path), "InsufficientInputIcon.png"));
      insufficientInputTex.LoadImage(data);
      var dataSmall = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Plugin.Path), "InsufficientInputIconSmall.png"));
      insufficientInputTexSmall.LoadImage(dataSmall);
      iconSprite = Sprite.Create(insufficientInputTexSmall, new Rect(0, 0, 80, 80), new Vector2(0.5f, 0.5f));

      contentLoaded = true;
    }
    
    static bool CheckIfMaterialIsPresent()
    {
      var builtInConfigs = Configs.builtin;

      if (builtInConfigs == null)
      {
        Plugin.Log.LogError("Configs.builtin is null");
        return false;
      }

      var entitySignMat = builtInConfigs.entitySignMat;

      if (entitySignMat == null)
      {
        Plugin.Log.LogError("Configs.builtin.entitySignMat is null");
        return false;
      }

      var mainTexture = entitySignMat.mainTexture;

      if (mainTexture == null)
      {
        Plugin.Log.LogError("Configs.builtin.entitySignMat.mainTexture is null");
        return false;
      }

      return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Configs), nameof(Configs.Awake))]
    static void PatchMaterialTexture()
    {
      if (!CheckIfMaterialIsPresent())
        return;

      EnsureLoaded();

      var material = Configs.builtin.entitySignMat;

      entitySignRendererTextureOriginal = material.mainTexture;

      // Create a temporary RenderTexture of the same size as the texture
      var tmpRenderTexture =
        RenderTexture.GetTemporary(
          entitySignRendererTextureOriginal.width,
          entitySignRendererTextureOriginal.height,
          0,
          RenderTextureFormat.Default,
          RenderTextureReadWrite.sRGB
        );

      // Blit the pixels on texture to the RenderTexture
      Graphics.Blit(entitySignRendererTextureOriginal, tmpRenderTexture);

      // Backup the currently set RenderTexture
      var previousRenderTexture = RenderTexture.active;

      // Set the current RenderTexture to the temporary one we created
      RenderTexture.active = tmpRenderTexture;

      // Create a new readable Texture2D to copy the pixels to it
      entitySignRendererTextureModified = new Texture2D(entitySignRendererTextureOriginal.width, entitySignRendererTextureOriginal.height);

      // Copy the pixels from the RenderTexture to the new Texture
      entitySignRendererTextureModified.ReadPixels(new Rect(0, 0, tmpRenderTexture.width, tmpRenderTexture.height), 0, 0);
      entitySignRendererTextureModified.Apply();

      // Reset the active RenderTexture
      RenderTexture.active = previousRenderTexture;

      // Release the temporary RenderTexture
      RenderTexture.ReleaseTemporary(tmpRenderTexture);

      var pixels = insufficientInputTex.GetPixels(0, 0, texAtlasEntrySize, texAtlasEntrySize);
      entitySignRendererTextureModified.SetPixels(3 * texAtlasEntrySize, 7 * texAtlasEntrySize, texAtlasEntrySize, texAtlasEntrySize, pixels);
      entitySignRendererTextureModified.Apply();

      material.mainTexture = entitySignRendererTextureModified;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(IconSet), nameof(IconSet.Create))]
    static void PatchIconSet(IconSet __instance)
    {
      EnsureLoaded();

      var iconSetId = __instance.spriteIndexMap.Values.Max() + 1;

      if (iconSetId >= 625)
        return;

      var xOffset = (int)(iconSetId % 25);
      var yOffset = (int)(iconSetId / 25);
      Graphics.CopyTexture(iconSprite.texture, 0, 0, 0, 0, 80, 80, __instance.texture, 0, 0, xOffset * 80, yOffset * 80);

      __instance.spriteIndexMap[iconSprite] = iconSetId;
      __instance.signalIconIndex[InsufficientInputSignalId] = iconSetId;
      __instance.signalIconIndexBuffer.SetData(__instance.signalIconIndex);
      __instance.texture.Apply(true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SignalProtoSet), nameof(SignalProtoSet.IconSprite))]
    static void SignalProtoSet__IconSprite(int signalId, ref Sprite __result)
    {
      if (signalId == InsufficientInputSignalId)
        __result = iconSprite;
    }

    static void Patch_Assemblers(FactorySystem __instance, int start, int end)
    {
      var signPool = __instance.factory.entitySignPool;

      for (var i = start; i < end; i++)
      {
        ref readonly var assembler = ref __instance.assemblerPool[i];
        if (assembler.id != i || assembler.replicating || assembler.time >= assembler.timeSpend)
          continue;

        if (signPool[assembler.entityId].signType != SignData.NOT_WORKING)
          continue;

        for (var j = 0; j < assembler.requireCounts.Length; j++)
        {
          if (assembler.served[j] < assembler.requireCounts[j])
          {
            signPool[assembler.entityId].signType = INSUFFICIENT_INPUT_SIGN_TYPE;
            break;
          }
        }
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTick), typeof(long), typeof(bool))]
    static void Patch_Assemblers_SingleThread(FactorySystem __instance) => Patch_Assemblers(__instance, 1, __instance.assemblerCursor);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTick), typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int))]
    static void Patch_Assemblers_MultiThread(FactorySystem __instance, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
      if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.assemblerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var start, out var end))
        Patch_Assemblers(__instance, start, end);
    }

    static void Patch_LabProduce(FactorySystem __instance, int start, int end)
    {
      var signPool = __instance.factory.entitySignPool;

      for (var i = start; i < end; i++)
      {
        ref readonly var lab = ref __instance.labPool[i];
        if (lab.id != i || lab.researchMode || lab.replicating || lab.time >= lab.timeSpend)
          continue;

        if (signPool[lab.entityId].signType != SignData.NOT_WORKING)
          continue;

        for (var j = 0; j < lab.requireCounts.Length; j++)
        {
          if (lab.served[j] < lab.requireCounts[j])
          {
            signPool[lab.entityId].signType = INSUFFICIENT_INPUT_SIGN_TYPE;
            break;
          }
        }
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabProduceMode), typeof(long), typeof(bool))]
    static void Patch_LabProduce_SingleThread(FactorySystem __instance) => Patch_LabProduce(__instance, 1, __instance.labCursor);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabProduceMode), typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int))]
    static void Patch_LabProduce_MultiThread(FactorySystem __instance, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
    {
      if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.labCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var start, out var end))
        Patch_LabProduce(__instance, start, end);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabResearchMode))]
    static void Patch_LabResearch(FactorySystem __instance)
    {
      var signPool = __instance.factory.entitySignPool;

      for (var i = 1; i < __instance.labCursor; i++)
      {
        ref readonly var lab = ref __instance.labPool[i];
        if (lab.id != i || !lab.researchMode || lab.replicating)
          continue;

        if (signPool[lab.entityId].signType != SignData.NOT_WORKING)
          continue;

        for (var j = 0; j < lab.matrixServed.Length; j++)
        {
          if (lab.matrixServed[j] < lab.matrixPoints[j])
          {
            signPool[lab.entityId].signType = INSUFFICIENT_INPUT_SIGN_TYPE;
            break;
          }
        }
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow._OnCreate))]
    static void PatchBuildingWarnMask(UIOptionWindow __instance)
    {
      EnsureLoaded();

      var existingBuildingWarnMaskArr = __instance.buildingWarnButtons;

      var rightmostPosition = existingBuildingWarnMaskArr.Max(x => (x.transform as RectTransform).anchoredPosition.x);
      var existingIconToCopy = existingBuildingWarnMaskArr.Last();
      var sizeDelta = (existingIconToCopy.transform as RectTransform).sizeDelta;

      var insufficientInputWarnMask = Object.Instantiate(existingIconToCopy, existingIconToCopy.transform.parent);

      {
        var rectTransform = insufficientInputWarnMask.transform as RectTransform;
        rectTransform.anchoredPosition = new Vector2(rightmostPosition + sizeDelta.x, rectTransform.anchoredPosition.y);

        var image = insufficientInputWarnMask.transform.Find("image")?.gameObject?.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
          image.sprite = iconSprite;

        insufficientInputWarnMask.data = 1 << (int)(INSUFFICIENT_INPUT_SIGN_TYPE - 1);

        insufficientInputWarnMask.tips.tipTitle = "Insufficient Input";
        insufficientInputWarnMask.tips.tipText = "This sign will display when there is insufficient input to the building";
      }

      var prevCount = existingBuildingWarnMaskArr.Length;
      __instance.buildingWarnButtons = new UIButton[prevCount + 1];
      System.Array.Copy(existingBuildingWarnMaskArr, __instance.buildingWarnButtons, prevCount);
      __instance.buildingWarnButtons[prevCount] = insufficientInputWarnMask;
    }
  }
}
