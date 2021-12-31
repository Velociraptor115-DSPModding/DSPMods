using System.IO;
using System.Collections;
using System.Collections.Generic;
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
          ? BlackboxManager.Instance.blackboxes.Find(x => x.Selection.stationIds.Contains(stationId))
          : BlackboxManager.Instance.blackboxes.Find(x => x.Id == blackboxId);

      if (blackbox != null)
      {
        Debug.Log($"Seems to be part of Blackbox#{blackbox.Id}");
        UIRoot.instance.uiGame.ShutAllFunctionWindow();
        var uiBlackboxInspectWindow = BlackboxUIGateway.BlackboxInspectWindow?.Component;
        if (uiBlackboxInspectWindow != null)
        {
          uiBlackboxInspectWindow._Init(blackbox);
          uiBlackboxInspectWindow._Open();
          uiBlackboxInspectWindow.transform.SetAsLastSibling();
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
      BlackboxUIGateway.Create();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnUpdate))]
    static void UIGame___OnUpdate()
    {
      BlackboxUIGateway.Update();

      var uiBlackboxWindowManager = BlackboxUIGateway.BlackboxManagerWindow?.Component;
      if (uiBlackboxWindowManager != null && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.M))
      {
        if (uiBlackboxWindowManager.active)
          uiBlackboxWindowManager._Close();
        else
          uiBlackboxWindowManager._Open();
      }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnFree))]
    static void UIGame___OnFree()
    {
      BlackboxUIGateway.Free();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnDestroy))]
    static void UIGame___OnDestroy()
    {
      BlackboxUIGateway.Destroy();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame.ShutAllFunctionWindow))]
    static void UIGame__ShutAllFunctionWindow()
    {
      BlackboxUIGateway.BlackboxInspectWindow.Component._Close();
      BlackboxUIGateway.BlackboxInspectWindow.Component._Free();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SignalProtoSet), nameof(SignalProtoSet.IconSprite))]
    static void SignalProtoSet__IconSprite(int signalId, ref Sprite __result)
    {
      if (signalId == BlackboxHighlight.blackboxSignalId)
        __result = BlackboxUIGateway.iconSprite;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WarningSystem), nameof(WarningSystem.WarningLogic))]
    static void WarningSystem__WarningLogic(ref WarningSystem __instance)
    {
      var highlight = BlackboxManager.Instance.highlight;
      if (highlight.blackboxId > 0)
      {
        var warningPool = __instance.warningPool;
        foreach (var warningId in highlight.warningIds)
        {
          warningPool[warningId].state = 1;
          warningPool[warningId].signalId = BlackboxHighlight.blackboxSignalId;

          __instance.warningCounts[BlackboxHighlight.blackboxSignalId]++;
        }
      }

      if (__instance.warningCounts[BlackboxHighlight.blackboxSignalId] > 0)
      {
        __instance.warningSignals[__instance.warningSignalCount] = BlackboxHighlight.blackboxSignalId;
        __instance.warningSignalCount++;
      }
    }
  }

  public class NextFrameInvokeHelper: MonoBehaviour
  {
    public void Start()
    {

    }
  }

  public static class BlackboxUIGateway
  {
    private static ModdedUIBlackboxInspectWindow blackboxInspectWindow;
    private static ModdedUIBlackboxManagerWindow blackboxManagerWindow;
    private static List<IModdedUI> moddedUIs;
    private static NextFrameInvokeHelper nextFrameInvokeHelper;
    public static Sprite iconSprite;

    public static ModdedUIBlackboxInspectWindow BlackboxInspectWindow => blackboxInspectWindow;
    public static ModdedUIBlackboxManagerWindow BlackboxManagerWindow => blackboxManagerWindow;

    static BlackboxUIGateway()
    {
      blackboxInspectWindow = new ModdedUIBlackboxInspectWindow();
      blackboxManagerWindow = new ModdedUIBlackboxManagerWindow();

      moddedUIs = new List<IModdedUI>
      {
          blackboxInspectWindow
        , blackboxManagerWindow
      };
    }

    public static void Create()
    {
      {
        var newTex = new Texture2D(1, 1);
        var data = System.IO.File.ReadAllBytes($@"{Path.GetDirectoryName(Plugin.Path)}\icon.png");
        newTex.LoadImage(data);
        iconSprite = Sprite.Create(newTex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
      }

      var thisRoot = new GameObject("bb-ui-gateway-root");
      thisRoot.SetActive(true);
      nextFrameInvokeHelper = thisRoot.GetOrCreateComponent<NextFrameInvokeHelper>();
      CreateObjectsAndPrefabs();
      nextFrameInvokeHelper.InvokeNextFrame(CreateComponents);
    }

    private static void CreateObjectsAndPrefabs()
    {
      foreach (var moddedUI in moddedUIs)
      {
        if (moddedUI.GameObject != null)
          throw new System.Exception("Blackbox UI mod encountered already created objects");
        moddedUI.CreateObjectsAndPrefabs();
      }
    }

    private static void CreateComponents()
    {
      foreach (var moddedUI in moddedUIs)
      {
        moddedUI.CreateComponents();
      }
    }

    public static void Destroy()
    {
      foreach (var moddedUI in moddedUIs)
      {
        moddedUI.Destroy();
      }
      Object.Destroy(nextFrameInvokeHelper);
    }

    public static void Free()
    {
      foreach (var moddedUI in moddedUIs)
      {
        moddedUI.Free();
      }
    }

    public static void Update()
    {
      foreach (var moddedUI in moddedUIs)
      {
        moddedUI.Update();
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

    public static GameObject DestroyComponent<T>(this GameObject gameObject) where T: Component
    {
      if (gameObject == null)
        return null;
      var component = gameObject.GetComponent<T>();
      if (component != null)
        Object.Destroy(component);
      return gameObject;
    }

    public static T GetOrCreateComponent<T>(this GameObject gameObject) where T : Component
    {
      if (gameObject == null)
        return null;
      var component = gameObject.GetComponent<T>();
      if (component == null)
        component = gameObject.AddComponent<T>();
      return component;
    }

    public static GameObject SelectChild(this GameObject gameObject, string name)
    {
      if (gameObject == null)
        return null;
      return gameObject.transform.Find(name)?.gameObject;
    }

    public static GameObject SelectDescendant(this GameObject gameObject, params string[] names)
    {
      var x = gameObject;
      foreach (var name in names)
        x = x.SelectChild(name);
      return x;
    }

    public static void InvokeNextFrame(this MonoBehaviour instance, System.Action executable)
    {
      try
      {
        instance.StartCoroutine(_InvokeNextFrame(executable));
      }
      catch
      {
        Plugin.Log.LogError("Couldn't invoke in next frame");
      }
    }

    private static IEnumerator _InvokeNextFrame(System.Action executable)
    {
      yield return null;
      executable();
    }
  }
}
