using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DysonSphereProgram.Modding.DroneControl;

public static class UIManager
{
  public static bool Patched { get; private set; } = false;

  private static Button DroneControlBtn;
  private static Image DroneControlBtnImage;
  private static Color DroneControlBtnColorOriginal;
  private static Color DroneControlBtnColorEnabled;
  private static Color DroneControlBtnColorDisabled;
  private static Material originalMat;
  private static Material widget5xMat;
  
  public static void CreateUI()
  {
    if (Patched)
      return;

    var mechaWindow = UIRoot.instance.uiGame.mechaWindow;
    if (!mechaWindow)
      return;
    var circle = mechaWindow.droneCountText.transform.parent.Find("circle")?.gameObject;
    if (!circle)
      return;

    DroneControlBtn = circle.GetComponent<Button>();
    if (!DroneControlBtn)
      DroneControlBtn = circle.AddComponent<Button>();

    DroneControlBtnImage = circle.GetComponent<Image>();

    DroneControlBtn.onClick.AddListener(OnDroneControlBtnClickListener);
    
    DroneControlBtnColorOriginal = DroneControlBtnImage.color;
    DroneControlBtnColorDisabled = DroneControlBtnColorOriginal.RGBMultiplied(new Color(1f, 0.25f, 0.25f));
    DroneControlBtnColorEnabled = DroneControlBtnColorOriginal.RGBMultiplied(new Color(0.25f, 1f, 0.25f));
    originalMat = DroneControlBtnImage.material;
    widget5xMat = Resources.Load<Material>("ui/materials/widget-alpha-5x");
    DroneControlBtn.colors = new ColorBlock
    {
      normalColor = Color.white.AlphaMultiplied(0.4f),
      highlightedColor = Color.white.AlphaMultiplied(0.5f),
      pressedColor = Color.white.AlphaMultiplied(0.3f),
      disabledColor = Color.white.AlphaMultiplied(0.3f),
      colorMultiplier = 1,
      fadeDuration = 0.1f
    };
    DroneControlBtnImage.material = widget5xMat;
    DroneControlBtnImage.color = PatchController.DisableDrones ? DroneControlBtnColorDisabled : DroneControlBtnColorEnabled;
    Patched = true;
  }

  private static void OnDroneControlBtnClickListener()
  {
    PatchController.SetDisableDrones(!PatchController.DisableDrones);
    EventSystem.current.SetSelectedGameObject(null);
  }

  public static void RefreshUI()
  {
    if (!Patched)
      return;
    DroneControlBtnImage.color = PatchController.DisableDrones ? DroneControlBtnColorDisabled : DroneControlBtnColorEnabled;
  }

  public static void DestroyUI()
  {
    if (DroneControlBtn)
      Object.Destroy(DroneControlBtn);
    DroneControlBtn = null;

    if (DroneControlBtnImage)
    {
      DroneControlBtnImage.material = originalMat;
      DroneControlBtnImage.color = DroneControlBtnColorOriginal;
    }
    DroneControlBtnImage = null;

    originalMat = null;
    widget5xMat = null;

    Patched = false;
  }
}