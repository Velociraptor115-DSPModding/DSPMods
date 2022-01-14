using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace DysonSphereProgram.Modding.Blackbox.UI
{ 
  public class UIBlackboxManagerWindow: ManualBehaviour
  {
    UIComboBox astroComboBox;
    UIComboBox blackboxComboBox;
    Text titleText;
    Button closeBtn;

    TabBar tabBar;
    GameObject tabPrefab;
    GameObject tabPanelPrefab;

    UIBlackboxOverviewPanel overviewPanel;
    UIBlackboxSettingsPanel settingsPanel;

    public class TabControl
    {
      int index;
      string name;
      GameObject tabPrefab;
      ManualBehaviour tabPanel;

      TabBar tabBar;

      GameObject tab;
      RectTransform tabRectTransform;
      Button tabBtn;
      UIButton tabUiButton;
      Text tabText;

      public TabControl(TabBar tabBar, int index, string name, GameObject tabPrefab, ManualBehaviour tabPanel)
      {
        this.tabBar = tabBar;
        this.index = index;
        this.name = name;
        this.tabPrefab = tabPrefab;
        this.tabPanel = tabPanel;
      }

      public void Create()
      {
        tab = Object.Instantiate(tabPrefab, tabPrefab.transform.parent);
        tabRectTransform = tab.GetComponent<RectTransform>();
        var thisPosition = tabRectTransform.anchoredPosition;
        thisPosition.y = -index * tabPrefab.GetComponent<RectTransform>().sizeDelta.y;
        tabRectTransform.anchoredPosition = thisPosition;

        tabBtn = tab.GetComponent<Button>();
        tabUiButton = tab.GetComponent<UIButton>();
        tabText = tab.SelectChild("text").GetComponent<Text>();
        tabText.text = name;

        tabBtn.onClick.AddListener(new UnityAction(OnTabBtnClick));

        tab.SetActive(true);
      }

      public void Open()
      {
        tabPanel._Open();
        tabUiButton.highlighted = true;
      }

      public void Close()
      {
        tabUiButton.highlighted = false;
        tabPanel._Close();
      }

      void OnTabBtnClick()
      {
        tabBar.SelectTab(index);
      }
    }

    public class TabBar
    {
      GameObject tabBar;
      GameObject tabPrefab;

      public List<TabControl> tabs = new List<TabControl>();

      int selectedTabIndex;

      public TabBar(GameObject tabBar, GameObject tabPrefab)
      {
        this.tabBar = tabBar;
        this.tabPrefab = tabPrefab;
      }

      public void Create()
      {

      }

      public void AddTab(string name, ManualBehaviour tabPanel)
      {
        var newIndex = tabs.Count;
        var tab = new TabControl(this, newIndex, name, tabPrefab, tabPanel);
        tab.Create();
        tabs.Add(tab);

        if (tabs.Count == 1)
          tabs[0].Open();

        var numTabs = tabs.Count;
        var tabBarRect = tabBar.GetComponent<RectTransform>();
        var size = tabBarRect.sizeDelta;
        size.y = numTabs * tabPrefab.GetComponent<RectTransform>().sizeDelta.y;
        tabBarRect.sizeDelta = size;
      }

      public void SelectTab(int index)
      {
        Plugin.Log.LogDebug("Select Tab: " + index);
        if (index != selectedTabIndex)
        {
          tabs[selectedTabIndex].Close();
          selectedTabIndex = index;
          tabs[selectedTabIndex].Open();
        }
      }
    }

    public override void _OnCreate()
    {
      titleText =
        gameObject
          .SelectDescendant("panel-bg", "title-text")
          ?.GetComponent<Text>()
          ;

      if (titleText != null)
        titleText.text = "Blackbox Manager";

      closeBtn =
        gameObject
          .SelectDescendant("panel-bg", "x")
          ?.GetComponent<Button>()
          ;

      closeBtn?.onClick.AddListener(new UnityAction(_Close));

      var tabBarGO =
        gameObject
          .SelectDescendant("panel-bg", "horizontal-tab")
          ;

      tabPrefab =
        tabBarGO
          .SelectChild("tab-btn-prefab")
          ;

      tabPanelPrefab =
        gameObject
          .SelectChild("content-bg-prefab")
          ;

      tabBar = new TabBar(tabBarGO, tabPrefab);
      tabBar.Create();

      var blackboxOverviewPanelGO = Object.Instantiate(tabPanelPrefab, tabPanelPrefab.transform.parent);
      blackboxOverviewPanelGO.name = "overview-bg";
      overviewPanel = blackboxOverviewPanelGO.GetOrCreateComponent<UIBlackboxOverviewPanel>();
      overviewPanel._Create();
      overviewPanel._Init(null);

      tabBar.AddTab("Overview", overviewPanel);

      var blackboxSettingsPanelGO = Object.Instantiate(tabPanelPrefab, tabPanelPrefab.transform.parent);
      blackboxSettingsPanelGO.name = "settings-bg";
      settingsPanel = blackboxSettingsPanelGO.GetOrCreateComponent<UIBlackboxSettingsPanel>();
      settingsPanel._Create();
      settingsPanel._Init(null);

      tabBar.AddTab("Settings", settingsPanel);
    }

    public override void _OnDestroy()
    {
      titleText = null;
      closeBtn = null;

      tabBar = null;
      tabPrefab = null;
      tabPanelPrefab = null;
    }

    public override bool _OnInit()
    {
      return true;
    }

    public override void _OnFree()
    {

    }

    public override void _OnUpdate()
    {
      overviewPanel?._Update();
      settingsPanel?._Update();
    }
  }

  public class ModdedUIBlackboxManagerWindow : IModdedUI<UIBlackboxManagerWindow>
  {
    const string optionWindowPath = "UI Root/Overlay Canvas/Top Windows/Option Window";
    const string uiWindowsPath = "UI Root/Overlay Canvas/In Game/Windows";
    const string uiStatisticsWindowName = "Statistics Window";
    const string uiStatisticsWindowPath = uiWindowsPath + "/" + uiStatisticsWindowName;
    const string uiAssemblerWindowName = "Assembler Window";
    const string uiAssemblerWindowPath = uiWindowsPath + "/" + uiAssemblerWindowName;
    const string uiBlueprintBrowserWindowName = "Blueprint Browser";
    const string uiBlueprintBrowserWindowPath = uiWindowsPath + "/" + uiBlueprintBrowserWindowName;
    const string uiBlackboxMangerWindowName = "Blackbox Manager Window";
    const string uiBlackboxManagerWindowPath = uiWindowsPath + "/" + uiBlackboxMangerWindowName;

    private GameObject gameObject;
    private UIBlackboxManagerWindow uiBlackboxManagerWindow;

    public UIBlackboxManagerWindow Component => uiBlackboxManagerWindow;
    public GameObject GameObject => gameObject;

    object IModdedUI.Component => uiBlackboxManagerWindow;

    public void CreateObjectsAndPrefabs()
    {
      var uiGame = UIRoot.instance.uiGame;
      var statisticsWindow = uiGame.statWindow.gameObject;
      gameObject = Object.Instantiate(statisticsWindow, statisticsWindow.transform.parent);
      gameObject.name = uiBlackboxMangerWindowName;

      var tabBar =
        gameObject
          .SelectDescendant("panel-bg", "horizontal-tab")
          ;

      var tabSize =
        (tabBar.transform.GetChild(0)?.transform as RectTransform)
        ?.sizeDelta.y
        ;

      var tabBarTransform = tabBar.transform as RectTransform;
      if (tabBarTransform != null && tabSize != null)
      {
        Plugin.Log.LogDebug("Tab bar size is " + tabSize);
        var newSizeDelta = tabBarTransform.sizeDelta;
        newSizeDelta.y = tabSize.Value;
        tabBarTransform.sizeDelta = newSizeDelta;
      }

      var tabBtnPrefab =
        gameObject
          .SelectDescendant("panel-bg", "horizontal-tab", "milestone-btn")
          ;

      tabBtnPrefab
        .SelectChild("text")
        .DestroyComponent<Localizer>()
        ;
      tabBtnPrefab.name = "tab-btn-prefab";
      tabBtnPrefab.SetActive(false);

      var contentBgPrefab =
        gameObject
          .SelectChild("product-bg")
          ;

      contentBgPrefab
          .SelectChild("top")
          .DestroyChildren(
            "favorite-filter-1", "favorite-filter-2", "favorite-filter-3", "favorite-text",
            "RankComboBox"
          )
          ;

      contentBgPrefab
        .SelectDescendant("top", "TimeComboBox")
        .name = "AstroComboBox"
        ;

      contentBgPrefab
        .SelectDescendant("top", "TargetComboBox")
        .name = "BlackboxComboBox"
        ;

      var blueprintBrowserWindow = uiGame.blueprintBrowser.gameObject;

      var saveChangesButton =
        blueprintBrowserWindow
        .SelectDescendant("inspector-group", "save-changes-button")
        ;

      var actionButtonPrefab = Object.Instantiate(saveChangesButton, contentBgPrefab.transform);
      actionButtonPrefab.name = "action-btn-prefab";
      actionButtonPrefab.SetActive(false);

      {
        var uiButton = actionButtonPrefab.GetComponent<UIButton>();
        uiButton.tip = null;
        uiButton.tips = default(UIButton.TipSettings);
      }

      var checkboxOption =
        UIRoot.instance.optionWindow.gameObject
          .SelectDescendant("details", "content-3", "list", "scroll-view", "viewport", "content", "milky-way-activation")
          ;

      var checkboxOptionPrefab = Object.Instantiate(checkboxOption, contentBgPrefab.transform);
      checkboxOptionPrefab.name = "checkbox-option-prefab";
      checkboxOptionPrefab.DestroyComponent<Localizer>();
      checkboxOptionPrefab.SetActive(false);

      var progressBar =
        gameObject
          .SelectDescendant("achievement-bg", "top", "overall-progress-group")
          ;

      var progressBarPrefab = Object.Instantiate(progressBar, contentBgPrefab.transform);
      progressBarPrefab.name = "progress-bar-prefab";
      progressBarPrefab.SelectChild("title").DestroyComponent<Localizer>();
      progressBarPrefab.SetActive(false);

      var blackboxEntryPrefab =
        contentBgPrefab
        .SelectDescendant("scroll-view", "viewport", "content")
        ?.transform.GetChild(0).gameObject
        ;
      blackboxEntryPrefab.name = "blackbox-entry-prefab";

      blackboxEntryPrefab
        .DestroyChildren("item", "favorite-btn-1", "favorite-btn-2", "favorite-btn-3", "raw-image")
        .DestroyComponent<UIProductEntry>()
        ;

      blackboxEntryPrefab
        .SelectChild("item-desc")
        .DestroyChildren(
          "product-rate-label", "consume-rate-label", "product-rate-text", "consume-rate-text",
          "unit-label-1", "unit-label-2", "sep-line"
        )
        ;

      var assemblerStateText =
        uiGame.assemblerWindow.gameObject
          .SelectDescendant("state", "state-text")
          ;

      var blackboxStatusLabel = Object.Instantiate(assemblerStateText, blackboxEntryPrefab.transform);
      blackboxStatusLabel.name = "status-label";

      {
        var rectTransform =
          blackboxStatusLabel
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(30, -40);

        var text =
          blackboxStatusLabel
            .GetComponent<Text>()
            ;

        text.alignment = TextAnchor.UpperLeft;
        text.text = "Status";
      }

      blackboxEntryPrefab
        .SelectDescendant("item-desc", "item-name")
        .GetComponent<Text>()
        .text = "Name"
        ;

      {
        var rectTransform =
          blackboxEntryPrefab
            .SelectChild("item-desc")
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchoredPosition = new Vector2(30, 0);
      }

      var blackboxProgressBar = Object.Instantiate(progressBarPrefab, blackboxEntryPrefab.transform);
      blackboxProgressBar.name = "progress-bar";
      blackboxProgressBar.SelectChild("title").DestroyComponent<Localizer>();
      blackboxProgressBar.SetActive(true);

      {
        var rectTransform =
          blackboxProgressBar
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
      }

      var blackboxPauseResumeButton = Object.Instantiate(actionButtonPrefab, blackboxEntryPrefab.transform);
      blackboxPauseResumeButton.name = "pause-resume-btn";
      blackboxPauseResumeButton.SetActive(true);

      blackboxPauseResumeButton
        .SelectChild("text")
        .DestroyComponent<Localizer>()
        .GetComponent<Text>()
        .text = "Pause / Resume"
        ;

      {
        var rectTransform =
          blackboxPauseResumeButton
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = new Vector2(30, 20);
        rectTransform.sizeDelta = new Vector2(90, 30);
      }

      var blackboxHighlightButton = Object.Instantiate(actionButtonPrefab, blackboxEntryPrefab.transform);
      blackboxHighlightButton.name = "highlight-btn";
      blackboxHighlightButton.SetActive(true);

      blackboxHighlightButton
        .SelectChild("text")
        .DestroyComponent<Localizer>()
        .GetComponent<Text>()
        .text = "Highlight"
        ;

      {
        var rectTransform =
          blackboxHighlightButton
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x * 2 - rectTransform.sizeDelta.x, rectTransform.anchoredPosition.y);
      }

      var blackboxDeleteButton = Object.Instantiate(actionButtonPrefab, blackboxEntryPrefab.transform);
      blackboxDeleteButton.name = "delete-btn";
      blackboxDeleteButton.SetActive(true);

      blackboxDeleteButton
        .SelectChild("text")
        .DestroyComponent<Localizer>()
        .GetComponent<Text>()
        .text = "Delete"
        ;

      {
        var deleteBtnColor =
          blueprintBrowserWindow
            .SelectDescendant("inspector-group", "delete-button")
            .GetComponent<Image>()
            .color
            ;
        blackboxDeleteButton
          .GetComponent<Image>()
          .color = deleteBtnColor;
      }

      {
        var contentTransform =
          contentBgPrefab
            .SelectDescendant("scroll-view", "viewport", "content")
            ?.transform
            ;

        Plugin.Log.LogDebug("child count: " + contentTransform.childCount);

        for (int i = 1; i < contentTransform.childCount; i++)
          Object.Destroy(contentTransform.GetChild(i).gameObject);
      }

      contentBgPrefab.name = "content-bg-prefab";
      contentBgPrefab.SetActive(false);

      tabBar
        .DestroyChildren(
          "production-btn", "power-btn", "research-btn",
          "dyson-btn", "performance-btn", "achievement-btn"
        )
        ;

      gameObject
        .DestroyChildren(
          "milestone-bg", "power-bg", "research-bg", "dyson-bg",
          "performance-bg", "achievement-bg", "content-trigger"
        )
        .DestroyComponent<UIStatisticsWindow>()
        ;

      gameObject
        .SelectChild("panel-bg")
        .DestroyChild("vertical-tab")
        ;

      gameObject
        .SelectDescendant("panel-bg", "title-text")
        .DestroyComponent<Localizer>()
        ;
    }

    public void CreateComponents()
    {
      uiBlackboxManagerWindow = gameObject.GetOrCreateComponent<UIBlackboxManagerWindow>();
      uiBlackboxManagerWindow._Create();
      uiBlackboxManagerWindow._Init(null);
    }

    public void Destroy()
    {
      if (uiBlackboxManagerWindow != null)
      {
        uiBlackboxManagerWindow._OnDestroy();
      }
      uiBlackboxManagerWindow = null;
      
      if (gameObject != null)
      {
        Object.Destroy(gameObject);
      }
      gameObject = null;
    }

    public void Free()
    {
      uiBlackboxManagerWindow?._OnFree();
    }

    public void Init()
    {
       
    }

    public void Update()
    {
      uiBlackboxManagerWindow?._OnUpdate();
    }
  }
}
