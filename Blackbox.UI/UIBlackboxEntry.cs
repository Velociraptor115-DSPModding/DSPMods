using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace DysonSphereProgram.Modding.Blackbox.UI
{ 
  public class UIProgressBar: MonoBehaviour
  {
    public Text title { get; private set; }
    public Text progressText { get; private set; }

    public RectTransform progressPointRect { get; private set; }
    public Image progressImage { get; private set; }

    private float _progress;
    public float progress
    {
      get => _progress;
      set
      {
        if (_progress != value)
        {
          _progress = value;
          progressImage.fillAmount = value;
          progressPointRect.anchoredPosition = new Vector2(progressImage.rectTransform.rect.width * value, 0f);
        }
      }
    }

    public void Awake()
    {
      title =
        gameObject
          .SelectChild("title")
          .GetComponent<Text>()
          ;

      progressText =
        gameObject
          .SelectChild("progress-text")
          .GetComponent<Text>()
          ;

      progressPointRect =
        gameObject
          .SelectDescendant("bar-group", "bar-fg", "point")
          .GetComponent<RectTransform>()
          ;

      progressImage =
        gameObject
          .SelectDescendant("bar-group", "bar-fg")
          .GetComponent<Image>()
          ;
    }
  }

  public class UIBlackboxEntry: ManualBehaviour
  {
    public RectTransform rectTransform;
    public Text nameText;
    public Text statusText;
    public Text pauseResumeBtnText;
    public Text highlightBtnText;

    public Image highlightBtnImage;

    public UIButton pauseResumeBtn;
    public UIButton highlightBtn;
    public UIButton deleteBtn;

    public UIProgressBar progressBar;

    public int index;

    public Blackbox entryData;

    private static Color errorColor = new Color(1f, 0.27f, 0.1934f, 0.7333f);
    private static Color warningColor = new Color(0.9906f, 0.5897f, 0.3691f, 0.7059f);
    private static Color okColor = new Color(0.3821f, 0.8455f, 1f, 0.7059f);
    private static Color idleColor = new Color(0.5882f, 0.5882f, 0.5882f, 0.8196f);

    private static Color highlightColor = new Color(0.2972f, 0.6886f, 1f, 0.8471f);
    private static Color stopHighlightColor = new Color(1f, 0.298f, 0.3697f, 0.8471f);

    public override void _OnCreate()
    {
      rectTransform = gameObject.GetComponent<RectTransform>();

      nameText =
        gameObject
          .SelectDescendant("item-desc", "item-name")
          .GetComponent<Text>()
          ;

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

      highlightBtn =
        gameObject
          .SelectDescendant("highlight-btn")
          .GetComponent<UIButton>()
          ;

      highlightBtnText =
        highlightBtn
          .gameObject
          .SelectChild("text")
          .GetComponent<Text>()
          ;

      highlightBtnImage =
        highlightBtn
          .GetComponent<Image>();

      deleteBtn =
        gameObject
          .SelectDescendant("delete-btn")
          .GetComponent<UIButton>()
          ;

      progressBar =
        gameObject
          .SelectDescendant("progress-bar")
          .GetOrCreateComponent<UIProgressBar>()
          ;
    }

    public override void _OnDestroy()
    {
      
    }

    public override bool _OnInit()
    {
      pauseResumeBtn.onClick += OnPauseResumeBtnClick;
      highlightBtn.onClick += OnHighlightBtnClick;
      deleteBtn.onClick += OnDeleteBtnClick;
      return true;
    }

    public override void _OnFree()
    {
      pauseResumeBtn.onClick -= OnPauseResumeBtnClick;
      highlightBtn.onClick -= OnHighlightBtnClick;
      deleteBtn.onClick -= OnDeleteBtnClick;
    }

    public override void _OnUpdate()
    {
      if (entryData == null)
        return;

      nameText.text = entryData.Name;

      switch (entryData.Status)
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
          if (entryData.Simulation != null)
          {
            statusText.text = entryData.Simulation.isBlackboxSimulating ? "Simulating" : "Simulation Paused";
            statusText.color = entryData.Simulation.isBlackboxSimulating ? okColor : warningColor;
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
          statusText.text = entryData.Status.ToString();
          statusText.color = idleColor;
          break;
      }

      progressBar.gameObject.SetActive(false);
      pauseResumeBtn.gameObject.SetActive(false);

      if (entryData.Simulation != null)
      {
        pauseResumeBtn.gameObject.SetActive(true);
        if (entryData.Simulation.isBlackboxSimulating)
          pauseResumeBtnText.text = "Pause";
        else
          pauseResumeBtnText.text = "Resume";

        progressBar.gameObject.SetActive(true);
        progressBar.title.text = "";
        progressBar.progress = entryData.Simulation.CycleProgress;
        progressBar.progressText.text = entryData.Simulation.CycleProgressText;
      }

      if (entryData.Analysis != null)
      {
        progressBar.gameObject.SetActive(true);
        progressBar.title.text = "";
        progressBar.progress = entryData.Analysis.Progress;
        progressBar.progressText.text = entryData.Analysis.ProgressText;
      }

      if (entryData.Id == BlackboxManager.Instance.highlight.blackboxId)
      {
        highlightBtnText.text = "Stop Highlight";
        highlightBtnImage.color = stopHighlightColor;
      }
      else
      {
        highlightBtnText.text = "Highlight";
        highlightBtnImage.color = highlightColor;
      }
    }

    public void SetTrans()
    {
      rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -index * rectTransform.rect.height);
    }

    private void OnBtnClick()
    {
      Plugin.Log.LogDebug($"Button clicked from {nameof(UIBlackboxEntry)} {nameText.text}");
    }

    private void OnPauseResumeBtnClick(int _)
    {
      OnBtnClick();
      if (entryData == null)
        return;

      if (entryData.Status != BlackboxStatus.Blackboxed || entryData.Simulation == null)
        return;

      if (entryData.Simulation.isBlackboxSimulating)
        entryData.Simulation.PauseBlackboxing();
      else
        entryData.Simulation.ResumeBlackboxing();
    }

    private void OnHighlightBtnClick(int _)
    {
      OnBtnClick();
      if (entryData == null)
        return;

      //var stationIds = string.Join(", ", entryData.Selection.stationIds);
      //Plugin.Log.LogDebug("Need to draw nav lines to " + stationIds);
      var highlight = BlackboxManager.Instance.highlight;

      if (highlight.blackboxId == entryData.Id)
        highlight.ClearHighlight();
      else
        highlight.RequestHighlight(entryData);
    }

    private void OnDeleteBtnClick(int _)
    {
      OnBtnClick();
      if (entryData == null)
        return;

      BlackboxManager.Instance.MarkBlackboxForRemoval(entryData);
    }
  }
}
