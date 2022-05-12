using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DysonSphereProgram.Modding.CustomResolutions;

public enum WindowMode
{
  Fullscreen,
  FullscreenExclusive,
  FullscreenBorderless,
  Windowed,
  WindowedBorderless
}

public class CustomResolutionsController
{
  public ConfigEntry<bool> isFirstRun;
  
  public ConfigEntry<bool> isCustomResolution;
  public ConfigEntry<int> customResolutionWidth;
  public ConfigEntry<int> customResolutionHeight;
  public ConfigEntry<int> customResolutionRefreshRate;
  
  public ConfigEntry<WindowMode> windowMode;

  private const string AutoConfigSection = "Auto Config";
  private const string CustomResolutionSection = "Custom Resolution";
  private const string WindowModeSection = "Window Mode";

  private const string IsFirstRunKey = "Is First Run";
  private const string IsCustomResolutionKey = "Is Custom Resolution";
  private const string CustomResolutionWidthKey = "Width";
  private const string CustomResolutionHeightKey = "Height";
  private const string CustomResolutionRefreshRateKey = "Refresh Rate";
  private const string WindowModeKey = "Window Mode";

  internal CustomResolutionsController(ConfigFile config)
  {

    isFirstRun = config.Bind(AutoConfigSection, IsFirstRunKey, true);

    isCustomResolution = config.Bind(CustomResolutionSection, IsCustomResolutionKey, false);
    customResolutionWidth = config.Bind(CustomResolutionSection, CustomResolutionWidthKey, 0);
    customResolutionHeight = config.Bind(CustomResolutionSection, CustomResolutionHeightKey, 0);
    customResolutionRefreshRate = config.Bind(CustomResolutionSection, CustomResolutionRefreshRateKey, 0);

    windowMode = config.Bind(WindowModeSection, WindowModeKey, WindowMode.Fullscreen);
  }

  public void HandleFirstRun()
  {
    if (!isFirstRun.Value)
      return;

    windowMode.Value = DSPGame.globalOption.fullscreen ? WindowMode.Fullscreen : WindowMode.Windowed;
    isFirstRun.Value = false;
  }
  
  public const NativeWindowStyle RegularWindowStyle =
      NativeWindowStyle.Overlapped
      | NativeWindowStyle.MinimizeBox
      | NativeWindowStyle.SysMenu
      | NativeWindowStyle.Caption
      | NativeWindowStyle.ClipSiblings
      | NativeWindowStyle.Visible
    ;
  
  public const NativeWindowStyle ResizableWindowStyle =
      NativeWindowStyle.Overlapped
      | NativeWindowStyle.TiledWindow
      | NativeWindowStyle.ClipSiblings
      | NativeWindowStyle.Visible
    ;
  
  public const NativeWindowStyle BorderlessWindowStyle =
      NativeWindowStyle.Overlapped
      | NativeWindowStyle.Popup
      | NativeWindowStyle.ClipSiblings
      | NativeWindowStyle.Visible
    ;

  public void ApplySettings(WindowMode mode, Resolution resolution, bool isResizable)
  {
    var hWnd = NativeInterop.GetActiveWindow();

    switch (mode)
    {
      case WindowMode.Fullscreen:
        Screen.SetResolution(
          resolution.width, resolution.height, FullScreenMode.FullScreenWindow,
          resolution.refreshRate
        );
        break;
      case WindowMode.FullscreenExclusive:
        Screen.SetResolution(
          resolution.width, resolution.height, FullScreenMode.ExclusiveFullScreen,
          resolution.refreshRate
        );
        break;
      case WindowMode.FullscreenBorderless:
        Screen.SetResolution(
          resolution.width, resolution.height, FullScreenMode.Windowed,
          resolution.refreshRate
        );
        UIRoot.instance.StartCoroutine(MakeBorderlessFullscreen(hWnd));
        break;
      case WindowMode.Windowed:
        Screen.SetResolution(
          resolution.width, resolution.height, FullScreenMode.Windowed,
          resolution.refreshRate
        );
        var style = isResizable ? ResizableWindowStyle : RegularWindowStyle;
        UIRoot.instance.StartCoroutine(SetWindowStyleAsync(hWnd, style, true));
        break;
      case WindowMode.WindowedBorderless:
        Screen.SetResolution(
          resolution.width, resolution.height, FullScreenMode.Windowed,
          resolution.refreshRate
        );
        UIRoot.instance.StartCoroutine(SetWindowStyleAsync(hWnd, BorderlessWindowStyle, true));
        break;
    }
  }

  public void ApplySettings(in GameOption option, bool isResizable = false)
  {
    ApplySettings(windowMode.Value, GetEffectiveResolution(option.resolution), isResizable);
  }

  public Resolution GetEffectiveResolution(Resolution requestedResolution)
  {
    if (Plugin.Controller.isCustomResolution.Value)
    {
      requestedResolution.width = Plugin.Controller.customResolutionWidth.Value;
      requestedResolution.height = Plugin.Controller.customResolutionHeight.Value;
      requestedResolution.refreshRate = Plugin.Controller.customResolutionRefreshRate.Value;
    }
    else
    {
      var resolutions = GetValidResolutions();
      if (resolutions != null && resolutions.Length > 0)
      {
        if (!resolutions.Contains(requestedResolution))
        {
          Plugin.Log.LogDebug("Not custom resolution and unsupported by monitor, resetting to a valid one");
          requestedResolution = resolutions[(resolutions.Length / 2) - 1];
        }
      }
      else
      {
        requestedResolution.width = 1280;
        requestedResolution.height = 720;
        requestedResolution.refreshRate = 60;
      }
    }
    return requestedResolution;
  }
  
  private IEnumerator MakeBorderlessFullscreen(IntPtr hWnd)
  {
    yield return new WaitForEndOfFrame();
    yield return new WaitForEndOfFrame();
    NativeInterop.SetWindowStyle(hWnd, ResizableWindowStyle);
    NativeInterop.SetWindowState(hWnd, ShowCommand.ShowMaximized);
    yield return new WaitForEndOfFrame();
    NativeInterop.SetWindowStyle(hWnd, BorderlessWindowStyle);
  }

  private IEnumerator SetWindowStyleAsync(IntPtr hWnd, NativeWindowStyle style, bool retainClientSize)
  {
    yield return new WaitForEndOfFrame();
    yield return new WaitForEndOfFrame();
    NativeInterop.SetWindowStyle(hWnd, style, retainClientSize);
  }

  private IEnumerator Combine(params IEnumerator[] enumerators)
  {
    foreach (var enumerator in enumerators)
      while (enumerator.MoveNext())
        yield return enumerator.Current;
  }
  
  public static Resolution[] GetValidResolutions()
  {
    var resolutions = Screen.resolutions;
    var rotatedResolutions =
      resolutions.Select(res =>
      {
        (res.width, res.height) = (res.height, res.width);
        return res;
      });
    return resolutions.Concat(rotatedResolutions).ToArray();
  }
}


public static class Patch
{
  [HarmonyPostfix]
  [HarmonyPatch(typeof(GameOption), nameof(GameOption.Apply))]
  public static void ResetToPortraitResolutionIfDetected(ref GameOption __instance)
  {
    Plugin.Controller.ApplySettings(in __instance);
  }

  [HarmonyPostfix]
  [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.CollectResolutions))]
  public static void AddRotatedResolutions(UIOptionWindow __instance)
  {
    //var rotatedResolutions = GetRotatedResolutions();
    var existingResolutions = __instance.availableResolutions.ToArray();
    foreach (var resolution in existingResolutions)
    {
      var res = resolution;
      (res.width, res.height) = (res.height, res.width);
      __instance.availableResolutions.Add(res);
      __instance.resolutionComp.Items.Add(string.Concat(new string[]
      {
        res.width.ToString(),
        " x ",
        res.height.ToString(),
        "   ",
        res.refreshRate.ToString(),
        "Hz"
      }));
    }
  }
}