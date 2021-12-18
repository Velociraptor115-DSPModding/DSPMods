using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DysonSphereProgram.Modding.Blackbox.UI
{
  [HarmonyPatch]
  class BlackboxUIPatch
  {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame.OnPlayerInspecteeChange))]
    static void UIGame__OnPlayerInspecteeChange(EObjectType objType, int objId, ref bool __runOriginal)
    {
      if (!__runOriginal)
        return;
      var factory = GameMain.mainPlayer.factory;
      if (factory == null || objType != EObjectType.Entity || objId <= 0)
        return;

      var entity = factory.entityPool[objId];
      Debug.Log($"Trying to inspect Entity #{objId}");

      var blackboxId = 0;

      if (entity.assemblerId > 0 && factory.factorySystem.assemblerPool[entity.assemblerId].id < 0)
        blackboxId = -factory.factorySystem.assemblerPool[entity.assemblerId].id;
      if (entity.labId > 0 && factory.factorySystem.labPool[entity.labId].id < 0)
        blackboxId = -factory.factorySystem.labPool[entity.labId].id;
      if (entity.beltId > 0 && factory.cargoTraffic.beltPool[entity.beltId].id < 0)
        blackboxId = -factory.cargoTraffic.beltPool[entity.beltId].id;
      if (entity.splitterId > 0 && factory.cargoTraffic.splitterPool[entity.splitterId].id < 0)
        blackboxId = -factory.cargoTraffic.splitterPool[entity.splitterId].id;
      if (entity.inserterId > 0 && factory.factorySystem.inserterPool[entity.inserterId].id < 0)
        blackboxId = -factory.factorySystem.inserterPool[entity.inserterId].id;
      
      var stationId = entity.stationId;

      if (blackboxId <= 0 && stationId <= 0)
        return;
      
      var blackbox =
        stationId > 0
          ? BlackboxManager.Instance.blackboxes.Find(x => x.stationIds.Contains(stationId))
          : BlackboxManager.Instance.blackboxes.Find(x => x.Id == blackboxId);

      if (blackbox != null)
      {
        Debug.Log($"Seems to be part of Blackbox#{blackbox.Id}");
        UIRoot.instance.uiGame.ShutAllFunctionWindow();
        var uiBlackboxWindow = BlackboxUI.UIBlackboxWindow;
        if (uiBlackboxWindow != null)
        {
          uiBlackboxWindow._Init(blackbox);
          uiBlackboxWindow._Open();
          uiBlackboxWindow.transform.SetAsLastSibling();
        }
        if (stationId > 0)
        {
          UIRoot.instance.uiGame.stationWindow.stationId = stationId;
          UIRoot.instance.uiGame.OpenStationWindow();
          UIRoot.instance.uiGame.inspectStationId = stationId;
        }

        __runOriginal = false;
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnCreate))]
    static void UIGame___OnCreate()
    {
      BlackboxUI.CreateIfNotExists();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnUpdate))]
    static void UIGame___OnUpdate()
    {
      BlackboxUI.UIBlackboxWindow._Update();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnFree))]
    static void UIGame___OnFree()
    {
      BlackboxUI.UIBlackboxWindow._Close();
      BlackboxUI.UIBlackboxWindow._Free();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnDestroy))]
    static void UIGame___OnDestroy()
    {
      BlackboxUI.UIBlackboxWindow._Destroy();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame.ShutAllFunctionWindow))]
    static void UIGame__ShutAllFunctionWindow()
    {
      BlackboxUI.UIBlackboxWindow._Close();
      BlackboxUI.UIBlackboxWindow._Free();
    }
  }

  public static class BlackboxUI
  {
    const string uiWindowsPath = "UI Root/Overlay Canvas/In Game/Windows";
    const string uiTemplateWindowName = "Window Template";
    const string uiTemplateWindowPath = uiWindowsPath + "/" + uiTemplateWindowName;
    const string uiAssemblerWindowName = "Assembler Window";
    const string uiAssemblerWindowPath = uiWindowsPath + "/" + uiAssemblerWindowName;
    const string uiBlackboxWindowName = "Blackbox Window";
    const string uiBlackboxWindowPath = uiWindowsPath + "/" + uiBlackboxWindowName;

    private static GameObject windowGO;
    private static UIBlackboxWindow uiBlackboxWindow;
    public static GameObject WindowGO
    {
      get
      {
        CreateIfNotExists();
        return windowGO;
      }
    }

    public static UIBlackboxWindow UIBlackboxWindow
    {
      get
      {
        CreateIfNotExists();
        return uiBlackboxWindow;
      }
    }

    public static void CreateIfNotExists()
    {
      if (windowGO != null)
        return;
      var assemblerWindow = GameObject.Find(uiAssemblerWindowPath);
      windowGO = Object.Instantiate(assemblerWindow, assemblerWindow.transform.parent);
      windowGO.name = uiBlackboxWindowName;

      windowGO
        .DestroyChild("player-storage")
        .DestroyChild("state")
        .DestroyChild("offwork")
        .DestroyComponent<UIAssemblerWindow>()
        ;

      windowGO
        .SelectChild("panel-bg")
        .DestroyChild("deco")
        .DestroyChild("deco (1)")
        .DestroyChild("deco (2)")
        ;

      windowGO
        .SelectChild("produce")
        .DestroyChild("speed")
        .DestroyChild("serving-box")
        ;

      windowGO
        .SelectChild("produce")
        .SelectChild("circle-back")
        .DestroyChild("product-icon")
        .DestroyChild("cnt-text")
        .DestroyChild("circle-fg-1")
        .DestroyChild("product-icon-1")
        .DestroyChild("cnt-text-1")
        .DestroyChild("stop-btn")
        ;

      if (windowGO.GetComponent<UIBlackboxWindow>() == null)
        windowGO.AddComponent<UIBlackboxWindow>();
      uiBlackboxWindow = windowGO.GetComponent<UIBlackboxWindow>();
      uiBlackboxWindow._Create();
    }

    public static void DestroyAll()
    {
      if (windowGO != null)
      {
        Object.Destroy(windowGO);
        uiBlackboxWindow = null;
      }
    }
  }

  public static class GameObjectExtensions
  {
    public static GameObject DestroyChildren(this GameObject gameObject, params string[] names)
    {
      var x = gameObject;
      foreach (var name in names)
        x = x.DestroyChild(name);
      return x;
    }
    public static GameObject DestroyChild(this GameObject gameObject, string name)
    {
      if (gameObject == null)
        return null;
      var child = gameObject.transform.Find(name)?.gameObject;
      if (child != null)
        Object.Destroy(child);
      return gameObject;
    }

    public static GameObject DestroyComponent<T>(this GameObject gameObject) where T: Object
    {
      if (gameObject == null)
        return null;
      var component = gameObject.GetComponent<T>();
      if (component != null)
        Object.Destroy(component);
      return gameObject;
    }

    public static GameObject SelectChild(this GameObject gameObject, string name)
    {
      if (gameObject == null)
        return null;
      return gameObject.transform.Find(name)?.gameObject;
    }
  }

  //public class BlackboxUI
  //{
  //  public static readonly BlackboxUI Instance = new BlackboxUI();

  //  public UIBlackboxWindow window;
  //}
}
