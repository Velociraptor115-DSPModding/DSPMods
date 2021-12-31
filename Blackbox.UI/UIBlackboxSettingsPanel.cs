using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace DysonSphereProgram.Modding.Blackbox.UI
{
  public class UIBlackboxSettingsPanel: ManualBehaviour
  {
    UIToggle autoblackbox;
    Text autoblackboxText;
    UIToggle forceNoStacking;
    Text forceNoStackingText;
    UIToggle logProfiledData;
    Text logProfiledDataText;
    UIToggle continuousLogging;
    Text continuousLoggingText;
    UIToggle analyseInBackground;
    Text analyseInBackgroundText;

    public override void _OnCreate()
    {
      gameObject
        .DestroyChild("top")
        ;

      {
        var rectTransform =
          gameObject
            .SelectChild("scroll-view")
            .GetComponent<RectTransform>()
            ;

        rectTransform.pivot = new Vector2(rectTransform.pivot.x, 1);
        rectTransform.sizeDelta = Vector2.zero;
      }

      var checkboxOptionPrefab =
        gameObject
          .SelectChild("checkbox-option-prefab")
          ;

      var scrollViewContent =
        gameObject
          .SelectDescendant("scroll-view", "viewport", "content")
          ;

      var autoblackboxGO = Object.Instantiate(checkboxOptionPrefab, scrollViewContent.transform);
      autoblackboxGO.name = "cb-autoblackbox";
      autoblackboxText = autoblackboxGO.GetComponent<Text>();
      autoblackbox = autoblackboxGO.SelectChild("CheckBox").GetComponent<UIToggle>();
      autoblackboxText.text = "Auto-blackbox";
      autoblackbox.isOn = false;
      autoblackboxGO.SetActive(true);

      {
        var rectTransform = autoblackboxGO.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -30);
      }

      var forceNoStackingGO = Object.Instantiate(checkboxOptionPrefab, scrollViewContent.transform);
      forceNoStackingGO.name = "cb-force-no-stacking";
      forceNoStackingText = forceNoStackingGO.GetComponent<Text>();
      forceNoStacking = forceNoStackingGO.SelectChild("CheckBox").GetComponent<UIToggle>();
      forceNoStackingText.text = "Force No Stacking";
      forceNoStacking.isOn = false;
      forceNoStackingGO.SetActive(true);

      {
        var rectTransform = forceNoStackingGO.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -80);
      }

      var logProfiledDataGO = Object.Instantiate(checkboxOptionPrefab, scrollViewContent.transform);
      logProfiledDataGO.name = "cb-log-profiled-data";
      logProfiledDataText = logProfiledDataGO.GetComponent<Text>();
      logProfiledData = logProfiledDataGO.SelectChild("CheckBox").GetComponent<UIToggle>();
      logProfiledDataText.text = "Log Profiled Data";
      logProfiledData.isOn = false;
      logProfiledDataGO.SetActive(true);

      {
        var rectTransform = logProfiledDataGO.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -230);
      }

      var continuousLoggingGO = Object.Instantiate(checkboxOptionPrefab, scrollViewContent.transform);
      continuousLoggingGO.name = "cb-continuous-logging";
      continuousLoggingText = continuousLoggingGO.GetComponent<Text>();
      continuousLogging = continuousLoggingGO.SelectChild("CheckBox").GetComponent<UIToggle>();
      continuousLoggingText.text = "Continuous Logging";
      continuousLogging.isOn = false;
      continuousLoggingGO.SetActive(true);

      {
        var rectTransform = continuousLoggingGO.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -280);
      }

      var analyseInBackgroundGO = Object.Instantiate(checkboxOptionPrefab, scrollViewContent.transform);
      analyseInBackgroundGO.name = "cb-analyse-in-background";
      analyseInBackgroundText = analyseInBackgroundGO.GetComponent<Text>();
      analyseInBackground = analyseInBackgroundGO.SelectChild("CheckBox").GetComponent<UIToggle>();
      analyseInBackgroundText.text = "Analyse in background thread";
      analyseInBackground.isOn = false;
      analyseInBackgroundGO.SetActive(true);

      {
        var rectTransform = analyseInBackgroundGO.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -330);
      }
    }

    public override void _OnDestroy()
    {
      
    }

    public override bool _OnInit()
    {
      autoblackbox.isOn = BlackboxManager.Instance.autoBlackbox.isActive;
      forceNoStacking.isOn = BlackboxBenchmark.forceNoStackingConfig;
      logProfiledData.isOn = BlackboxBenchmark.logProfiledData;
      continuousLogging.isOn = BlackboxBenchmark.continuousLogging;
      analyseInBackground.isOn = Blackbox.analyseInBackgroundConfig;
      return true;
    }

    public override void _OnFree()
    {

    }

    public override void _OnUpdate()
    {
      BlackboxManager.Instance.autoBlackbox.isActive = autoblackbox.isOn;
      BlackboxBenchmark.forceNoStackingConfig = forceNoStacking.isOn;
      BlackboxBenchmark.logProfiledData = logProfiledData.isOn;
      BlackboxBenchmark.continuousLogging = continuousLogging.isOn;
      Blackbox.analyseInBackgroundConfig = analyseInBackground.isOn;
    }
  }
}
