using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DysonSphereProgram.Modding.Blackbox.UI
{
  public class CustomUIBuilder
  {
    const string uiWindowsPath = "UI Root/Overlay Canvas/In Game/Windows";
    const string uiTemplateWindowName = "Window Template";
    const string uiTemplateWindowPath = uiWindowsPath + "/" + uiTemplateWindowName;

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
}
