using System;
using System.Collections;
using DysonSphereProgram.Modding.CustomResolutions.UI.Builder;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DysonSphereProgram.Modding.CustomResolutions;

using static UIBuilderDSL;

public class UIManager
{
  private RectTransform origResolutionsTransform;
  private UIComboBox origResolutionsComboBox;
  private RectTransform origFullscreenTransform;
  private Vector2 origResolutionsAnchorPos;
  
  // private bool isRotatedResolution;
  private bool isResizable;

  private GameObject preferredWindowModeGO;
  private GameObject customResolutionGO;
  
  private GameObject videoResolutionContainerGO;
  private GameObject customResolutionsContainerGO;
  private GameObject resizableToggleBtnGO;

  
  
  private readonly CustomResolutionsController controller;
  private SyncCustomResolution syncCustomResolution;

  internal UIManager(CustomResolutionsController controller)
  {
    this.controller = controller;
  }

  public void CreateUI()
  {
    origResolutionsComboBox = UIRoot.instance.optionWindow.resolutionComp;
    origResolutionsTransform = origResolutionsComboBox.transform.parent.GetComponent<RectTransform>();
    origFullscreenTransform = UIRoot.instance.optionWindow.fullscreenComp.transform.parent.GetComponent<RectTransform>();
    origResolutionsAnchorPos = origResolutionsTransform.anchoredPosition; 
    origResolutionsTransform.anchoredPosition = origFullscreenTransform.anchoredPosition;
    origFullscreenTransform.gameObject.SetActive(false);

    var windowModeContainer =
      Create.HorizontalLayoutGroup("window-mode")
        .ChildOf(origResolutionsTransform.parent)
        .WithMinMaxAnchor(origResolutionsTransform.anchorMin, origResolutionsTransform.anchorMax)
        .WithPivot(origResolutionsTransform.pivot)
        .OfSize(origResolutionsTransform.sizeDelta)
        .At(origResolutionsAnchorPos)
        .WithChildAlignment(TextAnchor.MiddleLeft)
        .WithSpacing(20)
        .ForceExpand(width: false, height: false)
        .ChildControls(width: true, height: true)
        .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
        ;

    var binding = new DelegateDataBindSource<Enum>(
      () => controller.windowMode.Value, x => SetWindowMode((WindowMode)x)
    );
    
    var group = windowModeContainer.uiElement.GetOrCreateComponent<ToggleGroup>();
    
    var fullscreenModeContainer =
      Create.HorizontalLayoutGroup("window-mode-fullscreen")
        .ChildOf(windowModeContainer)
        .WithChildAlignment(TextAnchor.MiddleLeft)
        .WithSpacing(10)
        .ForceExpand(width: false, height: false)
        .ChildControls(width: true, height: true)
        .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
        ;

    Create.Text("group-desc")
      .WithLocalizer("Fullscreen: ")
      .WithFontSize(18)
      .ChildOf(fullscreenModeContainer)
      ;

    CreateEnumToggle(fullscreenModeContainer, group, binding, WindowMode.Fullscreen, "Regular");
    CreateEnumToggle(fullscreenModeContainer, group, binding, WindowMode.FullscreenExclusive, "Exclusive");
    CreateEnumToggle(fullscreenModeContainer, group, binding, WindowMode.FullscreenBorderless, "Borderless");
    
    var windowedModeContainer =
      Create.HorizontalLayoutGroup("window-mode-windowed")
        .ChildOf(windowModeContainer)
        .WithChildAlignment(TextAnchor.MiddleLeft)
        .WithSpacing(10)
        .ForceExpand(width: false, height: false)
        .ChildControls(width: true, height: true)
        .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
        ;
    
    Create.Text("group-desc")
      .WithLocalizer("Windowed: ")
      .WithFontSize(18)
      .ChildOf(windowedModeContainer)
      ;
    
    CreateEnumToggle(windowedModeContainer, group, binding, WindowMode.Windowed, "Regular");
    CreateEnumToggle(windowedModeContainer, group, binding, WindowMode.WindowedBorderless, "Borderless");

    preferredWindowModeGO = windowModeContainer.uiElement;
    
    var customResolutionsContainer = 
      Create.HorizontalLayoutGroup("custom-resolutions")
        .ChildOf(origResolutionsTransform)
        .WithAnchor(Anchor.BottomLeft)
        .WithPivot(0, 0f)
        .OfSize(200, origResolutionsTransform.sizeDelta.y)
        .At(120, 0)
        .WithChildAlignment(TextAnchor.MiddleLeft)
        // .WithPadding(new RectOffset(horizontalSpacing, horizontalSpacing, 0, 0))
        .WithSpacing(10)
        .ForceExpand(width: false, height: false)
        .ChildControls(width: true, height: true)
        .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
        ;
    
    var buttonBlueIvyColor = new Color(0.24f, 0.6f, 0.72f);
    var buttonBlueFountainColor = new Color(0.37f, 0.72f, 0.84f);
        
    var onState = UIBuilder.buttonSelectableProperties.colors.Value with
    {
      normalColor = buttonBlueIvyColor,
      highlightedColor = buttonBlueFountainColor,
      pressedColor = buttonBlueFountainColor,
      fadeDuration = 0.05f
    };
    var offState = onState with
    {
      normalColor = Color.white.RGBMultiplied(0.55f), // 0.8235f
      highlightedColor = Color.white.RGBMultiplied(0.6f),
      pressedColor = Color.white.RGBMultiplied(0.6f) // new Color(0.5566f, 0.5566f, 0.5566f, 1f)
    };

    var isCustomResolutionBinding = new DelegateDataBindSource<bool>(
      () => controller.isCustomResolution.Value, SetIsCustomResolution
    );
    
    // var isRotatedToggleBtn =
    //   Create.ToggleButton("is-rotated-btn", "Rotated")
    //     .ChildOf(customResolutionsContainer)
    //     .WithLayoutSize(100, 30)
    //     .Bind(new DelegateDataBindSource<bool>(() => isRotatedResolution, x => isRotatedResolution = x))
    //     .BindInteractive(isCustomResolutionBinding.WithTransform(x => !x, x => !x))
    //     .WithVisuals((IProperties<Image>) UIBuilder.buttonImgProperties.WithColor(Color.white))
    //     .WithOnOffVisuals(onState, offState)
    //     ;

    Create.UIElement("spacer")
      .WithLayoutSize(10, 0)
      .ChildOf(customResolutionsContainer)
      ;
    
    var isCustomToggleBtn =
      Create.ToggleButton("is-custom-btn", "Custom")
        .ChildOf(customResolutionsContainer)
        .WithLayoutSize(100, 30)
        .Bind(isCustomResolutionBinding)
        .WithVisuals((IProperties<Image>) UIBuilder.buttonImgProperties.WithColor(Color.white))
        .WithOnOffVisuals(onState, offState)
        ;

    customResolutionGO = customResolutionsContainer.uiElement;

    ConfigEntryDataBindSource<int> widthBinding = controller.customResolutionWidth;
    ConfigEntryDataBindSource<int> heightBinding = controller.customResolutionHeight;

    var customResolutionsInputContainer = 
      Create.HorizontalLayoutGroup("custom-resolutions-input-container")
        .ChildOf(customResolutionsContainer)
        .WithChildAlignment(TextAnchor.MiddleLeft)
        .WithSpacing(5)
        .ForceExpand(width: false, height: false)
        .ChildControls(width: true, height: true)
        .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
        .WithComponent(out syncCustomResolution)
        ;

    syncCustomResolution.widthBinding = widthBinding;
    syncCustomResolution.heightBinding = heightBinding;
    syncCustomResolution.enabled = false;
    
    var isResizableBinding = new DelegateDataBindSource<bool>(
      () => isResizable, x => SetIsResizable(x, true)
    );
    var notTransform = DataBindTransform.From((bool x) => !x, x => !x);
    var stringIntTransform = DataBindTransform.From((int x) => x.ToString(), (string x) => int.TryParse(x, out int val) ? val : 0);

    var customResolutionWidthInputCtx =
      CreateTextInput(customResolutionsInputContainer, "input-width", widthBinding.WithTransform(stringIntTransform))
        .BindInteractive(isResizableBinding.WithTransform(notTransform));

    Create.Text("x")
      .WithText(" x ")
      .ChildOf(customResolutionsInputContainer)
      ;
    
    var customResolutionHeightInputCtx =
      CreateTextInput(customResolutionsInputContainer, "input-height", heightBinding.WithTransform(stringIntTransform))
        .BindInteractive(isResizableBinding.WithTransform(notTransform));

    var isResizableToggleBtn =
      Create.ToggleButton("is-resizable-btn", "Resizable Window")
        .ChildOf(customResolutionsInputContainer)
        .WithLayoutSize(135, 30)
        .Bind(isResizableBinding)
        .WithVisuals((IProperties<Image>) UIBuilder.buttonImgProperties.WithColor(Color.white))
        .WithOnOffVisuals(onState, offState)
        ;

    videoResolutionContainerGO = origResolutionsTransform.gameObject;
    customResolutionsContainerGO = customResolutionsInputContainer.uiElement;
    resizableToggleBtnGO = isResizableToggleBtn.uiElement;

    RefreshGameObjectVisibility(controller.windowMode.Value, controller.isCustomResolution.Value);
  }

  public void DestroyUI()
  {
    origResolutionsTransform.anchoredPosition = origResolutionsAnchorPos;
    origResolutionsComboBox.gameObject.SetActive(true);
    origResolutionsTransform.gameObject.SetActive(true);
    origFullscreenTransform.gameObject.SetActive(true);

    videoResolutionContainerGO = null;
    customResolutionsContainerGO = null;
    resizableToggleBtnGO = null;

    if (preferredWindowModeGO)
      Object.Destroy(preferredWindowModeGO);
    if (customResolutionGO)
      Object.Destroy(customResolutionGO);
  }
  
  private ColorBlock inputHighlightColorBlock = ColorBlock.defaultColorBlock with
  {
    normalColor = Color.clear,
    highlightedColor = Color.white.AlphaMultiplied(0.1f),
    pressedColor = Color.clear,
    disabledColor = Color.clear
  };

  private InputFieldContext CreateTextInput(UIElementContext root, string name, IDataBindSource<string> binding = null)
  {
    var inputFieldCtx = 
      Create.InputField(name)
        .ChildOf(root)
        .WithAlignment(TextAnchor.MiddleCenter)
        .WithLayoutSize(80, 30)
        .WithFontSize(16)
        .WithVisuals((IProperties<Image>)new ImageProperties() { color = Color.white})
        .WithTransition(colors: inputHighlightColorBlock)
        ;

    if (binding != null)
      inputFieldCtx.Bind(binding);
    
    Create.UIElement("bg")
      .ChildOf(inputFieldCtx)
      .WithAnchor(Anchor.Stretch)
      .OfSize(0, 0)
      .WithComponent(out Image _, new ImageProperties() { raycastTarget = false, sprite = UIBuilder.spriteBorder1, type = Image.Type.Sliced, color = Color.white.AlphaMultiplied(0.6f)});

    return inputFieldCtx;
  }
  
  private void CreateEnumToggle(UIElementContext currentEntry, ToggleGroup group, IDataBindSource<Enum> binding, Enum enumValue, string name)
  {
    var buttonBlueIvyColor = new Color(0.24f, 0.6f, 0.72f);
    var buttonBlueFountainColor = new Color(0.37f, 0.72f, 0.84f);
      
    var onState = UIBuilder.buttonSelectableProperties.colors.Value with
    {
      normalColor = buttonBlueIvyColor,
      highlightedColor = buttonBlueFountainColor,
      pressedColor = buttonBlueFountainColor,
      fadeDuration = 0.05f
    };
    var offState = onState with
    {
      normalColor = Color.white.RGBMultiplied(0.55f), // 0.8235f
      highlightedColor = Color.white.RGBMultiplied(0.6f),
      pressedColor = Color.white.RGBMultiplied(0.6f) // new Color(0.5566f, 0.5566f, 0.5566f, 1f)
    };

    Create.ToggleButton("toggle-button", name)
      .WithToggleGroup(group)
      .Bind(binding, enumValue)
      .WithVisuals((IProperties<Image>) UIBuilder.buttonImgProperties.WithColor(Color.white))
      .WithOnOffVisuals(onState, offState)
      .WithLayoutSize(100, 30)
      .ChildOf(currentEntry)
      ;
  }

  private void SetIsCustomResolution(bool newValue)
  {
    customResolutionsContainerGO.SetActive(newValue);
    origResolutionsComboBox.gameObject.SetActive(!newValue);

    if (!controller.isCustomResolution.Value && newValue)
    {
      var prevRes = UIRoot.instance.optionWindow.tempOption.resolution;
      controller.customResolutionWidth.Value = prevRes.width;
      controller.customResolutionHeight.Value = prevRes.height;
      controller.customResolutionRefreshRate.Value = prevRes.refreshRate; 
    }

    controller.isCustomResolution.Value = newValue;
  }

  private void SetWindowMode(WindowMode newMode)
  {
    if (controller.windowMode.Value == newMode)
      return;

    controller.windowMode.Value = newMode;
    controller.ApplySettings(in UIRoot.instance.optionWindow.tempOption);
    RefreshGameObjectVisibility(newMode, controller.isCustomResolution.Value);
  }

  private void RefreshGameObjectVisibility(WindowMode mode, bool isCustomResolution)
  {
    switch (mode)
    {
      case WindowMode.Fullscreen:
        videoResolutionContainerGO.SetActive(true);
        customResolutionGO.SetActive(true);
        customResolutionsContainerGO.SetActive(isCustomResolution);
        resizableToggleBtnGO.SetActive(false);
        SetIsResizable(false);
        break;
      case WindowMode.FullscreenExclusive:
        videoResolutionContainerGO.SetActive(true);
        customResolutionGO.SetActive(true);
        customResolutionsContainerGO.SetActive(isCustomResolution);
        resizableToggleBtnGO.SetActive(false);
        SetIsResizable(false);
        break;
      case WindowMode.FullscreenBorderless:
        videoResolutionContainerGO.SetActive(false);
        SetIsResizable(false);
        break;
      case WindowMode.Windowed:
        videoResolutionContainerGO.SetActive(true);
        customResolutionGO.SetActive(true);
        customResolutionsContainerGO.SetActive(isCustomResolution);
        resizableToggleBtnGO.SetActive(true);
        break;
      case WindowMode.WindowedBorderless:
        videoResolutionContainerGO.SetActive(true);
        customResolutionGO.SetActive(true);
        customResolutionsContainerGO.SetActive(isCustomResolution);
        resizableToggleBtnGO.SetActive(false);
        SetIsResizable(false);
        break;
    }
  }
  private void SetIsResizable(bool newValue, bool applyWindowStyle = false)
  {
    if (applyWindowStyle)
      controller.ApplySettings(in UIRoot.instance.optionWindow.tempOption, newValue);
    syncCustomResolution.enabled = newValue;
    isResizable = newValue;
  }
}

public class UIController : MonoBehaviour
{
  public GameObject videoResolutionContainer;
  public GameObject customResolutionsContainer;
  public GameObject resizableToggleBtn;

  public IOneWayDataBindSource<WindowMode> selectedWindowMode;
  public IOneWayDataBindSource<bool> isCustomResolution;

  private void Update()
  {
    switch (selectedWindowMode.Value)
    {
      case WindowMode.Fullscreen:
        break;
      case WindowMode.FullscreenExclusive:
        break;
      case WindowMode.FullscreenBorderless:
        break;
      case WindowMode.Windowed:
        break;
      case WindowMode.WindowedBorderless:
        break;
      default:
        throw new ArgumentOutOfRangeException();
    }
  }
}

public class SyncCustomResolution : MonoBehaviour
{
  public IDataBindSource<int> widthBinding;
  public IDataBindSource<int> heightBinding;

  private void Update()
  {
    widthBinding.Value = Screen.width;
    heightBinding.Value = Screen.height;
  }
}