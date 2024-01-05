using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using DysonSphereProgram.Modding.UIEnhancements.UI.Builder;
using UnityEngine.UI;

namespace DysonSphereProgram.Modding.UIEnhancements;

using static UIBuilderDSL;

public class UnrestrictedUIScaler: EnhancementBase
{
  public static ConfigEntry<int> uiScale;
  
  public static Slider uiScaleSlider;
  public static Canvas uiScaleSliderCanvas;

  public static RectTransform vanillaUiSliderRectTransform;
  public static Button livePreviewButton;

  private static int? restoreUiLayoutHeight;
  
  protected override void UseConfig(ConfigFile configFile)
  {
    uiScale = configFile.Bind(ConfigSection, "UI Reference Height", 900);
  }
  protected override void Patch(Harmony _harmony)
  {
    _harmony.PatchAll(typeof(UnrestrictedUIScaler));
  }
  protected override void Unpatch()
  {
    
  }
  protected override void CreateUI()
  {
    if (uiScaleSlider != null)
      return;
    
    var minHeight = Screen.resolutions.Min(x => x.height);
    var minWidth = Screen.resolutions.Min(x => x.width);
    var minBoth = Mathf.Min(minWidth, minHeight);
    var maxHeight = Screen.resolutions.Max(x => x.height);
    var maxWidth = Screen.resolutions.Max(x => x.width);
    var maxBoth = Mathf.Max(maxWidth, maxHeight);
    
    var binding = new ConfigEntryDataBindSource<int, float>(uiScale,
        DataBindTransform.From<int, float>(x => (float)x, x => (int)x));

    var sliderHandleCtx =
      Create.UIElement("slider-handle-container")
        .WithComponent(out Image sliderHandleImg, x => x.color = new Color(0.13f, 0.18f, 0.2f, 1f));
    
    var inputHighlightColorBlock = ColorBlock.defaultColorBlock with
    {
      normalColor = Color.clear,
      highlightedColor = Color.white.AlphaMultiplied(0.1f),
      pressedColor = Color.clear,
      disabledColor = Color.clear
    };

    var sliderHandleInputFieldCtx =
      Create.InputField("slider-handle-input-field")
        .ChildOf(sliderHandleCtx)
        .WithAnchor(Anchor.Center)
        .OfSize(40, 20)
        .WithVisuals((IProperties<Image>)new ImageProperties() { color = Color.white})
        .WithTransition(colors: inputHighlightColorBlock)
        .Bind(binding.WithTransform(x => x.ToString(), x => int.TryParse(x, out var res) ? res : 900))
        ;

    Create.UIElement("bg")
      .ChildOf(sliderHandleInputFieldCtx)
      .WithAnchor(Anchor.Stretch)
      .OfSize(0, 0)
      .WithComponent(out Image _, new ImageProperties() { raycastTarget = false, sprite = UIBuilder.spriteBorder1, type = Image.Type.Sliced, color = Color.white.AlphaMultiplied(0.6f)});

    var sliderHandleConfiguration = new SliderHandleConfiguration(80f, 0.6f, sliderHandleImg);
    var sliderConfiguration = new SliderConfiguration(minBoth, maxBoth, true, handle: sliderHandleConfiguration);

    var overlayCanvas = UIRoot.instance.overlayCanvas;
    var overlayCanvasParent = overlayCanvas.transform.parent;

    Create.UIElement("ui-scale-slider-canvas")
      .ChildOf(overlayCanvasParent)
      .WithComponent((CanvasScaler scaler) =>
      {
        var height = DSPGame.globalOption.resolution.height;
        scaler.referenceResolution = new Vector2(height, height);
      })
      .WithComponent(out uiScaleSliderCanvas)
      .WithComponent(out GraphicRaycaster _)
      ;
    
    uiScaleSliderCanvas.gameObject.SetActive(false);
    uiScaleSliderCanvas.planeDistance = overlayCanvas.planeDistance;
    uiScaleSliderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    uiScaleSliderCanvas.worldCamera = overlayCanvas.worldCamera;

    var uiScaleSliderCtx =
      Create.Slider("ui-scale-slider", sliderConfiguration)
        .ChildOf(uiScaleSliderCanvas.transform)
        .WithMinMaxAnchor(new Vector2(0.1f, 1f), new Vector2(0.9f, 1f))
        .OfSize(0, 30)
        .At(0, 0)
        .Bind(binding)
        .WithTransition(colors: UIBuilder.buttonSelectableProperties.colors)
        .WithComponent(out SliderValueChangedHandler vch);

    vch.slider = uiScaleSliderCtx.slider;
    vch.Handler = value => UICanvasScalerHandler.uiLayoutHeight = (int)value;

    var applyCancelContainer =
      Create.UIElement("btn-container")
        .ChildOf(sliderHandleCtx)
        .WithAnchor(Anchor.Bottom)
        .WithPivot(0.5f, 1f)
        .At(0, -20)
        ;

    Create.Button("apply-btn", "Apply", () =>
      {
        uiScaleSliderCanvas.gameObject.SetActive(false);
        restoreUiLayoutHeight = null;
      })
      .ChildOf(applyCancelContainer)
      .WithAnchor(Anchor.Right)
      .At(-5, 0)
      .OfSize(60, 25)
      .WithVisuals((IProperties<Image>)UIBuilder.buttonImgProperties)
      .WithTransition(UIBuilder.buttonSelectableProperties.transition)
      ;
    
    Create.Button("cancel-btn", "Cancel", () =>
      {
        uiScaleSliderCanvas.gameObject.SetActive(false);
        if (restoreUiLayoutHeight.HasValue)
        {
          UICanvasScalerHandler.uiLayoutHeight = restoreUiLayoutHeight.Value;
          restoreUiLayoutHeight = null;
        }
      })
      .ChildOf(applyCancelContainer)
      .WithAnchor(Anchor.Left)
      .At(5, 0)
      .OfSize(60, 25)
      .WithVisuals((IProperties<Image>)UIBuilder.buttonImgProperties.WithColor(new Color(1f, 0.3f, 0.37f, 0.8471f)))
      .WithTransition(UIBuilder.buttonSelectableProperties.transition)
      ;

    uiScaleSlider = uiScaleSliderCtx.slider;
    
    // Patch UIOptionWindow to activate the scaler game object
    {
      var optionWindow = UIRoot.instance.optionWindow;
      vanillaUiSliderRectTransform = optionWindow.uiLayoutHeightComp.GetComponent<RectTransform>();

      var baseColor = Color.white;
      var normalTint = new Color(0.4887f, 0.6525f, 0.7453f, 0.4886f);
      var highlightedTint = new Color(0.4887f, 0.6525f, 0.7453f, 0.7255f);

      var livePreviewButtonCtx = 
        Create.Button("ui-scaler-activate-button", "Enable Live Preview", () =>
          {
            restoreUiLayoutHeight = UICanvasScalerHandler.uiLayoutHeight;
            uiScale.Value = restoreUiLayoutHeight.Value;
            uiScaleSliderCanvas.gameObject.SetActive(true);
          })
          .ChildOf(vanillaUiSliderRectTransform.parent)
          .WithAnchor(Anchor.Left)
          .At(250, 0)
          .WithLayoutSize(120, 30)
          .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
          .WithFontSize(14)
          .WithVisuals((IProperties<Image>) new ImageProperties() { color = baseColor })
          .WithTransition(colors: ColorBlock.defaultColorBlock with
          {
            normalColor = normalTint, pressedColor = normalTint, disabledColor = normalTint,
            highlightedColor = highlightedTint
          })
          ;

      livePreviewButtonCtx.text.resizeTextForBestFit = false;

      livePreviewButton = livePreviewButtonCtx.button;
      vanillaUiSliderRectTransform.gameObject.SetActive(false);
    }
  }
  
  protected override void DestroyUI()
  {
    if (uiScaleSlider != null)
    {
      Object.Destroy(uiScaleSlider.gameObject);
      uiScaleSlider = null; 
    }

    if (uiScaleSliderCanvas != null)
    {
      Object.Destroy(uiScaleSliderCanvas.gameObject);
      uiScaleSliderCanvas = null;
    }

    if (vanillaUiSliderRectTransform != null)
    {
      vanillaUiSliderRectTransform.gameObject.SetActive(true);
      vanillaUiSliderRectTransform = null;
    }

    if (livePreviewButton != null)
    {
      Object.Destroy(livePreviewButton.gameObject);
      livePreviewButton = null;
    }
  }
  
  protected override string Name => "Unrestricted UI Scaler";

  [HarmonyPostfix]
  [HarmonyPatch(typeof(GameOption), nameof(GameOption.Apply))]
  public static void ApplyUIScaleAgain()
  {
    if (restoreUiLayoutHeight.HasValue)
      UICanvasScalerHandler.uiLayoutHeight = restoreUiLayoutHeight.Value;
    else
      UICanvasScalerHandler.uiLayoutHeight = uiScale.Value;
  }
}