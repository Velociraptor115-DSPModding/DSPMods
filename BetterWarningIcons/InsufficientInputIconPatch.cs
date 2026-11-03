using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DysonSphereProgram.Modding.BetterWarningIcons
{
  public static class InsufficientInputIconPatch
  {
    public const string ConfigSection = "Insufficient Input Warning";
    public static ConfigEntry<bool> enablePatch;

    public static Texture entitySignRendererTextureOriginal;
    public static Texture2D entitySignRendererTextureModified;

    public static Texture2D insufficientInputTex;
    public static Texture2D insufficientInputTexSmall;

    public static Sprite iconSprite;

    public static bool contentLoaded;

    const int texAtlasEntrySize = 256;

    public const uint INSUFFICIENT_INPUT_SIGN_TYPE = 31U;
    public const int InsufficientInputSignalId = (int)(500 + INSUFFICIENT_INPUT_SIGN_TYPE);

    public static void InitConfig(ConfigFile confFile)
    {
      enablePatch = confFile.Bind(ConfigSection, "Enable Patch", true, ConfigDescription.Empty);
    }

    static void EnsureLoaded()
    {
      if (contentLoaded)
        return;

      insufficientInputTex = new Texture2D(1, 1, TextureFormat.RGBA32, true);
      insufficientInputTexSmall = new Texture2D(1, 1, TextureFormat.RGBA32, true);
      var dir = Path.GetDirectoryName(Plugin.Path);
      ImageConversion.LoadImage(insufficientInputTex, File.ReadAllBytes(Path.Combine(dir, "InsufficientInputIcon.png")));
      ImageConversion.LoadImage(insufficientInputTexSmall, File.ReadAllBytes(Path.Combine(dir, "InsufficientInputIconSmall.png")));
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

      if (entitySignMat.mainTexture == null)
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

      var tmpRenderTexture = RenderTexture.GetTemporary(
        entitySignRendererTextureOriginal.width,
        entitySignRendererTextureOriginal.height,
        0,
        RenderTextureFormat.Default,
        RenderTextureReadWrite.sRGB);

      Graphics.Blit(entitySignRendererTextureOriginal, tmpRenderTexture);
      var previousRenderTexture = RenderTexture.active;
      RenderTexture.active = tmpRenderTexture;

      entitySignRendererTextureModified = new Texture2D(
        entitySignRendererTextureOriginal.width,
        entitySignRendererTextureOriginal.height);
      entitySignRendererTextureModified.ReadPixels(
        new Rect(0, 0, tmpRenderTexture.width, tmpRenderTexture.height), 0, 0);
      entitySignRendererTextureModified.Apply();

      RenderTexture.active = previousRenderTexture;
      RenderTexture.ReleaseTemporary(tmpRenderTexture);

      var pixels = insufficientInputTex.GetPixels(0, 0, texAtlasEntrySize, texAtlasEntrySize);
      entitySignRendererTextureModified.SetPixels(
        3 * texAtlasEntrySize,
        7 * texAtlasEntrySize,
        texAtlasEntrySize,
        texAtlasEntrySize,
        pixels);
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
      Graphics.CopyTexture(
        iconSprite.texture, 0, 0, 0, 0, 80, 80,
        __instance.texture, 0, 0, xOffset * 80, yOffset * 80);

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

    static void PatchAssemblers(FactorySystem factorySystem)
    {
      var signPool = factorySystem.factory.entitySignPool;
      for (var i = 1; i < factorySystem.assemblerCursor; i++)
      {
        ref var assembler = ref factorySystem.assemblerPool[i];
        if (assembler.id != i || assembler.recipeId == 0 || assembler.replicating)
          continue;
        if (assembler.time >= assembler.recipeExecuteData.timeSpend)
          continue;
        if (signPool[assembler.entityId].signType != SignData.NOT_WORKING)
          continue;

        var requireCounts = assembler.recipeExecuteData.requireCounts;
        for (var j = 0; j < requireCounts.Length; j++)
        {
          if (assembler.served[j] < requireCounts[j])
          {
            signPool[assembler.entityId].signType = INSUFFICIENT_INPUT_SIGN_TYPE;
            break;
          }
        }
      }
    }

    static void PatchLabProduce(FactorySystem factorySystem)
    {
      var signPool = factorySystem.factory.entitySignPool;
      for (var i = 1; i < factorySystem.labCursor; i++)
      {
        ref var lab = ref factorySystem.labPool[i];
        if (lab.id != i || lab.researchMode || lab.recipeId == 0 || lab.replicating)
          continue;
        if (lab.time >= lab.recipeExecuteData.timeSpend)
          continue;
        if (signPool[lab.entityId].signType != SignData.NOT_WORKING)
          continue;

        var requireCounts = lab.recipeExecuteData.requireCounts;
        for (var j = 0; j < requireCounts.Length; j++)
        {
          if (lab.served[j] < requireCounts[j])
          {
            signPool[lab.entityId].signType = INSUFFICIENT_INPUT_SIGN_TYPE;
            break;
          }
        }
      }
    }

    static void PatchLabResearch(FactorySystem factorySystem)
    {
      var signPool = factorySystem.factory.entitySignPool;
      for (var i = 1; i < factorySystem.labCursor; i++)
      {
        ref var lab = ref factorySystem.labPool[i];
        if (lab.id != i || !lab.researchMode || lab.replicating)
          continue;
        if (signPool[lab.entityId].signType != SignData.NOT_WORKING)
          continue;

        for (var j = 0; j < lab.matrixServed.Length; j++)
        {
          if (lab.matrixServed[j] < LabComponent.matrixPoints[j])
          {
            signPool[lab.entityId].signType = INSUFFICIENT_INPUT_SIGN_TYPE;
            break;
          }
        }
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic.OnFactoryFrameEnd))]
    static void PatchSignsAfterProcessing()
    {
      var data = GameMain.data;
      if (data?.factories == null)
        return;

      for (var i = 0; i < data.factoryCount; i++)
      {
        var factory = data.factories[i];
        if (factory?.factorySystem == null)
          continue;
        PatchAssemblers(factory.factorySystem);
        PatchLabProduce(factory.factorySystem);
        PatchLabResearch(factory.factorySystem);
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow._OnCreate))]
    static void PatchBuildingWarnMask(UIOptionWindow __instance)
    {
      EnsureLoaded();

      var existingBuildingWarnMaskArr = __instance.buildingWarnButtons;
      var rightmostPosition = existingBuildingWarnMaskArr.Max(x => ((RectTransform)x.transform).anchoredPosition.x);
      var existingIconToCopy = existingBuildingWarnMaskArr.Last();
      var sizeDelta = ((RectTransform)existingIconToCopy.transform).sizeDelta;

      var insufficientInputWarnMask = Object.Instantiate(existingIconToCopy, existingIconToCopy.transform.parent);
      var rectTransform = (RectTransform)insufficientInputWarnMask.transform;
      rectTransform.anchoredPosition = new Vector2(rightmostPosition + sizeDelta.x, rectTransform.anchoredPosition.y);

      var image = insufficientInputWarnMask.transform.Find("image")?.gameObject?.GetComponent<Image>();
      if (image != null)
        image.sprite = iconSprite;

      insufficientInputWarnMask.data = 1 << (int)(INSUFFICIENT_INPUT_SIGN_TYPE - 1);
      insufficientInputWarnMask.tips.tipTitle = "Insufficient Input";
      insufficientInputWarnMask.tips.tipText = "This sign will display when there is insufficient input to the building";

      var prevCount = existingBuildingWarnMaskArr.Length;
      __instance.buildingWarnButtons = new UIButton[prevCount + 1];
      System.Array.Copy(existingBuildingWarnMaskArr, __instance.buildingWarnButtons, prevCount);
      __instance.buildingWarnButtons[prevCount] = insufficientInputWarnMask;
    }
  }
}
