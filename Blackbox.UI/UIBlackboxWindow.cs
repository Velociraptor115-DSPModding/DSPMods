using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace DysonSphereProgram.Modding.Blackbox.UI
{
  public class CustomUIBuilder
  {
    const string uiWindowsPath = "UI Root/Overlay Canvas/In Game/Windows";
    const string uiTemplateWindowName = "Window Template";
    const string uiTemplateWindowPath = uiWindowsPath + "/" + uiTemplateWindowName;
    const string uiBlackboxWindowName = "Blackbox Window";
    const string uiBlackboxWindowPath = uiWindowsPath + "/" + uiBlackboxWindowName;

    GameObject gameObject;
    public CustomUIBuilder(GameObject gameObject)
    {
      this.gameObject = gameObject;
    }

    public CustomUIBuilder WithShadow()
    {
      var windowTemplate = GameObject.Find(uiTemplateWindowPath);
      var shadow = windowTemplate.transform.Find("shadow")?.gameObject;
      if (shadow == null)
        return this;
      
      Object.Instantiate(shadow, this.gameObject.transform);
      return this;
    }

    public CustomUIBuilder WithPanel()
    {
      var windowTemplate = GameObject.Find(uiTemplateWindowPath);
      var shadow = windowTemplate.transform.Find("panel-bg")?.gameObject;
      if (shadow == null)
        return this;

      Object.Instantiate(shadow, this.gameObject.transform);
      return this;
    }

    public GameObject Get()
    {
      return this.gameObject;
    }
  }

  public static class CustomUIBuilderExtensions
  {
    
  }


  public class UIBlackboxWindow: ManualBehaviour
  {
    private Blackbox blackbox;

    public override void _OnCreate()
    {
      gameObject
        .SelectChild("panel-bg")
        .SelectChild("btn-box")
        .SelectChild("close-btn")
        ?.GetComponent<Button>()
        ?.onClick.AddListener(_Close);
    }

    public override bool _OnInit()
    {
      blackbox = data as Blackbox;
      if (blackbox == null)
        return false;

      var titleText = gameObject
          .SelectChild("panel-bg")
          .SelectChild("title-text")
          ?.GetComponent<Text>()
          ;
      if (titleText != null)
        titleText.text = $"Blackbox #{blackbox.Id}";

      gameObject
        .SelectChild("produce")
        .SetActive(true)
        ;

      Debug.Log("Setting produce to Active");

      return true;
    }

    public override void _OnFree()
    {
      gameObject
          .SelectChild("produce")
          .SetActive(false);
      blackbox = null;
    }

    public override void _OnUpdate()
    {
      if (blackbox == null)
        return;

      var progressImg = gameObject
        .SelectChild("produce")
        .SelectChild("circle-back")
        .SelectChild("circle-fg")
        ?.GetComponent<Image>()
        ;

      progressImg.fillAmount = blackbox.CycleProgress;
    }
  }
}
