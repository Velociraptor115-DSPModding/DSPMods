using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace DysonSphereProgram.Modding.Blackbox.UI
{
  public class UIBlackboxInspectWindow: ManualBehaviour
  {
    private Blackbox blackbox;

    private Button closeButton;
    private Text titleText;
    private Text statusText;
    private Text pauseResumeBtnText;

    private UIButton pauseResumeBtn;
    private Image progressImg;
    private GameObject progressIndicator;

    private static Color errorColor = new Color(1f, 0.27f, 0.1934f, 0.7333f);
    private static Color warningColor = new Color(0.9906f, 0.5897f, 0.3691f, 0.7059f);
    private static Color okColor = new Color(0.3821f, 0.8455f, 1f, 0.7059f);
    private static Color idleColor = new Color(0.5882f, 0.5882f, 0.5882f, 0.8196f);

    public override void _OnCreate()
    {
      closeButton =
        gameObject
          .SelectDescendant("panel-bg", "btn-box", "close-btn")
          ?.GetComponent<Button>()
          ;

      closeButton?.onClick.AddListener(_Close);

      titleText =
        gameObject
          .SelectDescendant("panel-bg", "title-text")
          ?.GetComponent<Text>()
          ;

      if (titleText != null)
        titleText.text = "<Select Blackbox to Inspect>";

      progressIndicator =
        gameObject
          .SelectChild("produce")
          ;

      progressIndicator.SetActive(false);

      progressImg =
        progressIndicator
          .SelectDescendant("circle-back", "circle-fg")
          ?.GetComponent<Image>()
          ;

      if (progressImg != null)
        progressImg.fillAmount = 0;

      statusText =
        gameObject
          .SelectDescendant("status-label")
          .GetComponent<Text>()
          ;

      pauseResumeBtn =
        gameObject
          .SelectDescendant("pause-resume-btn")
          .GetComponent<UIButton>()
          ;

      pauseResumeBtnText =
        pauseResumeBtn
          .gameObject
          .SelectChild("text")
          .GetComponent<Text>()
          ;
    }

    public override void _OnDestroy()
    {
      closeButton = null;
      titleText = null;
      progressImg = null;
      progressIndicator = null;
      statusText = null;
      pauseResumeBtnText = null;
      pauseResumeBtn = null;
    }

    public override bool _OnInit()
    {
      blackbox = data as Blackbox;
      if (blackbox == null)
        return false;

      if (titleText != null)
        titleText.text = blackbox.Name;

      progressIndicator.SetActive(true);

      pauseResumeBtn.onClick += OnPauseResumeBtnClick;

      Debug.Log("Setting produce to Active");

      return true;
    }

    public override void _OnFree()
    {
      pauseResumeBtn.onClick -= OnPauseResumeBtnClick;
      progressIndicator.SetActive(false);
      blackbox = null;
      titleText.text = "<Select Blackbox to Inspect>";
    }

    public override void _OnUpdate()
    {
      if (blackbox == null)
        return;

      switch (blackbox.Status)
      {
        case BlackboxStatus.InAnalysis:
          statusText.text = "Analysing";
          statusText.color = idleColor;
          break;
        case BlackboxStatus.AnalysisFailed:
          statusText.text = "Analysis Failed";
          statusText.color = errorColor;
          break;
        case BlackboxStatus.Blackboxed:
          if (blackbox.Simulation != null)
          {
            statusText.text = blackbox.Simulation.isBlackboxSimulating ? "Simulating" : "Simulation Paused";
            statusText.color = blackbox.Simulation.isBlackboxSimulating ? okColor : warningColor;
            break;
          }
          statusText.text = "Blackboxed";
          statusText.color = idleColor;
          break;
        case BlackboxStatus.Invalid:
          statusText.text = "Invalid";
          statusText.color = errorColor;
          break;
        default:
          statusText.text = blackbox.Status.ToString();
          statusText.color = idleColor;
          break;
      }


      if (blackbox.Simulation == null)
      {
        pauseResumeBtn.gameObject.SetActive(false);
      }
      else
      {
        pauseResumeBtn.gameObject.SetActive(true);
        if (blackbox.Simulation.isBlackboxSimulating)
          pauseResumeBtnText.text = "Pause";
        else
          pauseResumeBtnText.text = "Resume";

        progressImg.fillAmount = blackbox.Simulation.CycleProgress;
      }
    }

    private void OnPauseResumeBtnClick(int _)
    {
      if (blackbox == null)
        return;

      if (blackbox.Status != BlackboxStatus.Blackboxed || blackbox.Simulation == null)
        return;

      if (blackbox.Simulation.isBlackboxSimulating)
        blackbox.Simulation.PauseBlackboxing();
      else
        blackbox.Simulation.ResumeBlackboxing();
    }
  }

  public class ModdedUIBlackboxInspectWindow : IModdedUI<UIBlackboxInspectWindow>
  {
    const string uiWindowsPath = "UI Root/Overlay Canvas/In Game/Windows";
    const string uiAssemblerWindowName = "Assembler Window";
    const string uiAssemblerWindowPath = uiWindowsPath + "/" + uiAssemblerWindowName;
    const string uiBlueprintBrowserWindowName = "Blueprint Browser";
    const string uiBlueprintBrowserWindowPath = uiWindowsPath + "/" + uiBlueprintBrowserWindowName;
    const string uiBlackboxInspectWindowName = "Blackbox Inspect Window";
    const string uiBlackboxInspectWindowPath = uiWindowsPath + "/" + uiBlackboxInspectWindowName;

    private GameObject gameObject;
    private UIBlackboxInspectWindow uiBlackboxInspectWindow;

    public UIBlackboxInspectWindow Component => uiBlackboxInspectWindow;
    public GameObject GameObject => gameObject;

    object IModdedUI.Component => uiBlackboxInspectWindow;

    public void CreateObjectsAndPrefabs()
    {
      var uiGame = UIRoot.instance.uiGame;
      var assemblerWindow = uiGame.assemblerWindow.gameObject;
      gameObject = Object.Instantiate(assemblerWindow, assemblerWindow.transform.parent);
      gameObject.name = uiBlackboxInspectWindowName;


      var assemblerStateText =
        assemblerWindow
          .SelectDescendant("state", "state-text")
          ;

      var blackboxStatusLabel = Object.Instantiate(assemblerStateText, gameObject.transform);
      blackboxStatusLabel.name = "status-label";

      {
        var rectTransform =
          blackboxStatusLabel
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(40, -50);

        var text =
          blackboxStatusLabel
            .GetComponent<Text>()
            ;

        text.alignment = TextAnchor.UpperLeft;
        text.text = "Status";
      }

      gameObject
        .DestroyChild("player-storage")
        .DestroyChild("state")
        .DestroyChild("offwork")
        .DestroyComponent<UIAssemblerWindow>()
        ;

      gameObject
        .SelectChild("panel-bg")
        .DestroyChild("deco")
        .DestroyChild("deco (1)")
        .DestroyChild("deco (2)")
        ;

      gameObject
        .SelectChild("produce")
        .DestroyChild("speed")
        .DestroyChild("serving-box")
        ;

      gameObject
        .SelectChild("produce")
        .SelectChild("circle-back")
        .DestroyChild("product-icon")
        .DestroyChild("cnt-text")
        .DestroyChild("circle-fg-1")
        .DestroyChild("product-icon-1")
        .DestroyChild("cnt-text-1")
        .DestroyChild("stop-btn")
        ;

      {
        var rectTransform =
          gameObject
            .SelectChild("produce")
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchorMin = new Vector2(0, 0.5f);
        rectTransform.anchorMax = new Vector2(0, 0.5f);
        rectTransform.pivot = new Vector2(0, 0.5f);
        rectTransform.anchoredPosition = new Vector2(20, 0);
      }

      var blueprintBrowserWindow = uiGame.blueprintBrowser.gameObject;

      var saveChangesButton =
        blueprintBrowserWindow
        .SelectDescendant("inspector-group", "save-changes-button")
        ;

      var pauseResumeBtn = Object.Instantiate(saveChangesButton, gameObject.transform);
      pauseResumeBtn.name = "pause-resume-btn";

      pauseResumeBtn
        .SelectChild("text")
        .DestroyComponent<Localizer>()
        .GetComponent<Text>()
        .text = "Pause / Resume"
        ;

      {
        var uiButton = pauseResumeBtn.GetComponent<UIButton>();
        uiButton.tip = null;
        uiButton.tips = default(UIButton.TipSettings);

        var rectTransform =
          pauseResumeBtn
            .GetComponent<RectTransform>()
            ;

        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = new Vector2(40, 40);
      }

      //var newGO = new GameObject("test-texture-load", typeof(RectTransform), typeof(Image));
      //newGO.transform.parent = gameObject.transform;
      //{
      //  var rectTransform = newGO.GetComponent<RectTransform>();
      //  rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
      //  rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
      //  rectTransform.pivot = new Vector2(0.5f, 0.5f);
      //  rectTransform.anchoredPosition3D = Vector3.zero;
      //  rectTransform.sizeDelta = Vector2.one;
      //}
      //{
      //  var image = newGO.GetComponent<Image>();
      //  var newTex = new Texture2D(1, 1);
      //  var data = System.IO.File.ReadAllBytes(@"D:\Raptor\Downloads\testbb2.png");
      //  newTex.LoadImage(data);
      //  image.material.mainTexture = newTex;
      //}
    }

    public void CreateComponents()
    {
      uiBlackboxInspectWindow = gameObject.GetOrCreateComponent<UIBlackboxInspectWindow>();
      uiBlackboxInspectWindow._Create();
    }

    public void Destroy()
    {
      if (uiBlackboxInspectWindow != null)
      {
        uiBlackboxInspectWindow._OnDestroy();
      }
      uiBlackboxInspectWindow = null;
      
      if (gameObject != null)
      {
        Object.Destroy(gameObject);
      }
      gameObject = null;
    }

    public void Free()
    {
      uiBlackboxInspectWindow?._OnFree();
    }

    public void Init()
    {
      
    }

    public void Update()
    {
      uiBlackboxInspectWindow?._OnUpdate();
    }
  }
}
