using System.Linq.Expressions;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using DysonSphereProgram.Modding.ExposeCreativeMode.UI.Builder;
using HarmonyLib;
using UnityEngine.Events;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  using static UIBuilderDSL;
  public class UIManager
  {
    private static UIManager instance;
    public static UIManager Instance => instance;
    
    public static void CreateUIManager()
    {
      if (instance != null)
        return;
      
      instance = new UIManager();
      instance.CreateUI();
    }

    public static void DestroyUIManager()
    {
      if (instance == null)
        return;

      instance.Destroy();
      instance = null;
    }

    private CreativeModeController controller;
    public UIModWindowBase window;

    private UIManager() {}

    public void Init(CreativeModeController controller)
    {
      this.controller = controller;
      this.controller = controller;
    }

    public void Free()
    {
      this.controller = null;
    }

    void Destroy()
    {
      Object.Destroy(window.gameObject);
      window = null;
    }

    void CreateUI()
    {
      var windowsObj = UIRoot.instance.uiGame.statWindow.gameObject.transform.parent;

      var settingsWindow =
          Create.PlainWindow("Creative Mode Settings")
            .ChildOf(windowsObj)
            .WithAnchor(Anchor.TopLeft)
            .OfSize(750, 500)
            .At(300, -180)
            .WithScrollCapture()
            .WithTitle("Creative Mode Settings")
            .InitializeComponent(out window)
            .uiElement
        ;

      var windowContentObj =
          Create.ScrollView("window-content", new ScrollViewConfiguration(ScrollViewAxis.BothVerticalAndHorizontal))
            .ChildOf(settingsWindow)
            .WithAnchor(Anchor.Stretch)
            .WithPivot(0, 1)
            .OfSize(-20, -55)
            .At(10, -45)
        ;
      
      var contentRoot = windowContentObj.scrollRect.content.gameObject;
      var viewportBg =
        Create.UIElement("bg")
          .ChildOf(windowContentObj.scrollRect.viewport)
          .WithAnchor(Anchor.Stretch)
          .At(0, 0)
          .WithComponent((Image x) => x.color = Color.black.AlphaMultiplied(0.5f));
      
      viewportBg.transform.SetAsFirstSibling();

      var mainPadding = 10;
      const int verticalSpacing = 10;
      var rootLayoutGroup = 
        Select.VerticalLayoutGroup(contentRoot)
          .WithPadding(new RectOffset(mainPadding, mainPadding, mainPadding + verticalSpacing, mainPadding + verticalSpacing))
          .WithSpacing(verticalSpacing)
          .WithChildAlignment(TextAnchor.UpperLeft)
          .ForceExpand(width: false, height: false)
          .ChildControls(width: true, height: true)
          .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
          ;
      
      static HorizontalLayoutGroupContext CreateSection(VerticalLayoutGroupContext root, IOneWayDataBindSource<bool> sectionEnabled, System.Action<VerticalLayoutGroupContext> sectionDef)
      {
        var horizontalSpacing = 2;
        
        var currentEntry = 
          Create.HorizontalLayoutGroup("config-section")
            .WithChildAlignment(TextAnchor.MiddleLeft)
            .WithPadding(new RectOffset(horizontalSpacing, horizontalSpacing, 0, 0))
            .WithSpacing(0)
            .ForceExpand(width: false, height: false)
            .ChildControls(width: true, height: true)
            .ChildOf(root)
            // .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
            ;

        var bannerSize = 3;

        var onColor = new Color(1f, 0.5f, 0f).RGBMultiplied(0.8f).AlphaMultiplied(0.75f);
        var offColor = Color.white.RGBMultiplied(0.6f).AlphaMultiplied(0.2f);

        {
          Create.UIElement("banner")
            .WithLayoutSize(bannerSize, 0, flexibleHeight: 1f)
            .ChildOf(currentEntry)
            .WithComponent(out Image bannerImg, img =>
            {
              img.material = UIBuilder.materialWidgetAlpha5x;
              img.color = sectionEnabled.Value ? onColor : offColor;
            })
            .WithComponent((DataBindValueChangedHandlerBool x) =>
            {
              x.Binding = sectionEnabled;
              x.Handler = isOn => bannerImg.color = isOn ? onColor : offColor;
            })
            ;
        }

        var sectionEntries = Create.VerticalLayoutGroup("section-entries")
          .ChildOf(currentEntry)
          .WithPadding(new RectOffset(0, 0, 0, 0))
          .WithSpacing(verticalSpacing)
          .WithChildAlignment(TextAnchor.UpperLeft)
          .ForceExpand(width: false, height: false)
          .ChildControls(width: true, height: true)
          // .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
          ;

        sectionDef(sectionEntries);

        return currentEntry;
      }

      static HorizontalLayoutGroupContext CreateEntry(VerticalLayoutGroupContext root, string configName, params RectTransform[] children)
      {
        var horizontalSpacing = 8;
        
        var currentEntry = 
          Create.HorizontalLayoutGroup("config-entry")
            .WithChildAlignment(TextAnchor.MiddleLeft)
            .WithPadding(new RectOffset(horizontalSpacing, horizontalSpacing, 0, 0))
            .WithSpacing(horizontalSpacing)
            .ForceExpand(width: false, height: false)
            .ChildControls(width: true, height: true)
            .ChildOf(root)
            // .WithContentSizeFitter(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
            ;

        Create.Text("name")
          .WithLocalizer(configName)
          .WithFontSize(20)
          .WithAlignment(TextAnchor.MiddleLeft)
          .WithLayoutSize(250, 34)
          .ChildOf(currentEntry)
          ;

        currentEntry.AddChildren(children);

        return currentEntry;
      }

      static ToggleButtonContext CreateOnOffToggleButton(IDataBindSource<bool> binding, string onText = "Enabled", string offText = "Disabled")
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

        return Create.ToggleButton("toggle-button", onText)
            .Bind(binding)
            .WithVisuals((IProperties<Image>) UIBuilder.buttonImgProperties.WithColor(Color.white))
            .WithOnOffVisualsAndText(onState, offState, onText, offText)
            .WithLayoutSize(100, 30)
          ;
      }

      static ToggleButtonContext CreateAutoEnableButton(ConfigEntry<bool> entry)
        => CreateOnOffToggleButton((ConfigEntryDataBindSource<bool>)entry, "Auto-Enable", "Auto-Enable");

      {
        var creativeModeBinding = new DelegateDataBindSource<bool>(() => controller?.Active ?? false, value => { if (controller != null) controller.Active = value; });
        CreateEntry(rootLayoutGroup, "Creative Mode", CreateOnOffToggleButton(creativeModeBinding).transform);
        CreateSection(rootLayoutGroup, creativeModeBinding, mainSection =>
        {
          {
            var binding = new DelegateDataBindSource<bool>(() => controller?.infiniteInventory?.IsEnabled ?? false, value => { if (controller?.infiniteInventory != null) controller.infiniteInventory.IsEnabled = value; });
            CreateEntry(mainSection, "Infinite Inventory"
              , CreateOnOffToggleButton(binding).BindInteractive(creativeModeBinding).transform
              , CreateAutoEnableButton(CreativeModeConfig.autoEnableInfiniteInventory).transform
            );
            CreateSection(mainSection, binding, section =>
            {
              var onlyUnlocked = new DelegateDataBindSource<bool>(() => controller?.infiniteInventory?.IncludeLocked ?? false, value => { if (controller?.infiniteInventory != null) controller.infiniteInventory.IncludeLocked = value; });
              CreateEntry(section, "Include Locked Items", CreateOnOffToggleButton(onlyUnlocked, "Yes", "No").transform);
            }); 
          }
          
          {
            var binding = new DelegateDataBindSource<bool>(() => controller?.infiniteStation?.IsEnabled ?? false, value => { if (controller?.infiniteStation != null) controller.infiniteStation.IsEnabled = value; });
            CreateEntry(mainSection, "Infinite Station", CreateOnOffToggleButton(binding).BindInteractive(creativeModeBinding).transform);
          }
          
          {
            var binding = new DelegateDataBindSource<bool>(() => controller?.infiniteReach?.IsEnabled ?? false, value => { if (controller?.infiniteReach != null) controller.infiniteReach.IsEnabled = value; });
            CreateEntry(mainSection, "Infinite Reach"
              , CreateOnOffToggleButton(binding).BindInteractive(creativeModeBinding).transform
              , CreateAutoEnableButton(CreativeModeConfig.autoEnableInfiniteReach).transform
            );
          }
          
          {
            var binding = new DelegateDataBindSource<bool>(() => controller?.infinitePower?.IsEnabled ?? false, value => { if (controller?.infinitePower != null) controller.infinitePower.IsEnabled = value; });
            CreateEntry(mainSection, "Infinite Power"
              , CreateOnOffToggleButton(binding).BindInteractive(creativeModeBinding).transform
              , CreateAutoEnableButton(CreativeModeConfig.autoEnableInfinitePower).transform
            );
          }
          
          {
            var binding = new DelegateDataBindSource<bool>(() => controller?.instantResearch?.IsEnabled ?? false, value => { if (controller?.instantResearch != null) controller.instantResearch.IsEnabled = value; });
            CreateEntry(mainSection, "Instant Research"
              , CreateOnOffToggleButton(binding).BindInteractive(creativeModeBinding).transform
              , CreateAutoEnableButton(CreativeModeConfig.autoEnableInstantResearch).transform
            );
          }
          
          {
            var binding = new DelegateDataBindSource<bool>(() => controller?.instantBuild?.IsEnabled ?? false, value => { if (controller?.instantBuild != null) controller.instantBuild.IsEnabled = value; });
            CreateEntry(mainSection, "Instant Build"
              , CreateOnOffToggleButton(binding).BindInteractive(creativeModeBinding).transform
              , CreateAutoEnableButton(CreativeModeConfig.autoEnableInstantBuild).transform
            );
          }
          
          {
            var binding = new DelegateDataBindSource<bool>(() => controller?.instantReplicate?.IsEnabled ?? false, value => { if (controller?.instantReplicate != null) controller.instantReplicate.IsEnabled = value; });
            CreateEntry(mainSection, "Instant/Free Replicate"
              , CreateOnOffToggleButton(binding).BindInteractive(creativeModeBinding).transform
              , CreateAutoEnableButton(CreativeModeConfig.autoEnableInstantReplicate).transform
            );
            CreateSection(mainSection, binding, section =>
            {
              var isInstant = new DelegateDataBindSource<bool>(() => controller?.instantReplicate?.IsInstant ?? false, value => { if (controller?.instantReplicate != null) controller.instantReplicate.IsInstant = value; });
              CreateEntry(section, "Is Instant", CreateOnOffToggleButton(isInstant, "Yes", "No").transform);
              var isFree = new DelegateDataBindSource<bool>(() => controller?.instantReplicate?.IsFree ?? false, value => { if (controller?.instantReplicate != null) controller.instantReplicate.IsFree = value; });
              CreateEntry(section, "Is Free", CreateOnOffToggleButton(isFree, "Yes", "No").transform);
              var allowAll = new DelegateDataBindSource<bool>(() => controller?.instantReplicate?.AllowAll ?? false, value => { if (controller?.instantReplicate != null) controller.instantReplicate.AllowAll = value; });
              CreateEntry(section, "Allow All", CreateOnOffToggleButton(allowAll, "Yes", "No").transform);
            });
          }

          static ButtonContext MakeFancyButton(string text)
          {
            var ctx = Create.Button("action-button", text, null)
              .WithLayoutSize(100, 30)
              .WithVisuals((IProperties<Image>)UIBuilder.buttonImgProperties);
            (ctx.button as Selectable).CopyFrom(UIBuilder.buttonSelectableProperties);
            return ctx;
          }

          ButtonContext SetModLevelButton(string text, int modLevel)
          {
            return MakeFancyButton(text).SetClickListener(() =>
              {
                var factory = controller?.player?.factory;
                if (factory != null)
                  PlanetReform.SetPlanetModLevel(factory, controller.veinsBury, modLevel);
              });
          }
          
          ButtonContext VeinModifyButton(string text, bool toBury)
          {
            return MakeFancyButton(text).SetClickListener(() =>
            {
              controller.veinsBury = toBury;
              var factory = controller?.player?.factory;
              if (factory != null)
                PlanetReform.ModifyAllVeinsHeight(factory, toBury);
            });
          }
          
          {
            var entry = CreateEntry(mainSection, "Reform Planet"
              , SetModLevelButton("Ground Level", 3).BindInteractive(creativeModeBinding).transform
              , SetModLevelButton("Shallow Oceans", 2).BindInteractive(creativeModeBinding).transform
              , SetModLevelButton("Medium Oceans", 1).BindInteractive(creativeModeBinding).transform
              , SetModLevelButton("Deep Oceans", 0).BindInteractive(creativeModeBinding).transform
            );
            
            var veinsBuryBinding = new DelegateDataBindSource<bool>(() => controller?.veinsBury ?? false, value => { if (controller != null) controller.veinsBury = value; });
            Select.UIElement(entry.uiElement.SelectChild("name"))
              .WithComponent(out Localizer localizer, x =>
              {
                x.stringKey = veinsBuryBinding.Value ? "Reform Planet (Will Bury Veins)" : "Reform Planet (Will Restore Veins)";
              })
              .WithComponent((DataBindValueChangedHandlerBool x) =>
              {
                x.Binding = veinsBuryBinding;
                x.Handler = willBury =>
                {
                  localizer.stringKey = willBury ? "Reform Planet (Will Bury Veins)" : "Reform Planet (Will Restore Veins)";
                  localizer.Refresh();
                };
              });

            CreateEntry(mainSection, "Planet Veins"
              , VeinModifyButton("Bury", true).BindInteractive(creativeModeBinding).transform
              , VeinModifyButton("Restore", false).BindInteractive(creativeModeBinding).transform
            );
          }
        });
      }
      
      window._Close();
    }
  }

  [HarmonyPatch(typeof(UIGame))]
  class UIPatches
  {
    [HarmonyPostfix]
    [HarmonyPatch(nameof(UIGame.isAnyFunctionWindowActive), MethodType.Getter)]
    static void PatchIsSettingsWindowActive(ref bool __result)
    {
      var window = UIManager.Instance?.window;
      __result = __result || window && window.active;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(UIGame.ShutInventoryConflictsWindows))]
    [HarmonyPatch(nameof(UIGame.ShutAllFunctionWindow))]
    static void PatchShutSettingsWindow()
    {
      var window = UIManager.Instance?.window;
      if (window)
        window._Close();
    }
  }
}