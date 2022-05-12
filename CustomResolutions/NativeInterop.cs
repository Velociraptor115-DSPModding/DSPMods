using System;
using System.Runtime.InteropServices;

namespace DysonSphereProgram.Modding.CustomResolutions;

[StructLayout(LayoutKind.Sequential)]  
internal struct RECT  
{  
  public int left;  
  public int top;  
  public int right;  
  public int bottom;  
}

class NativeInterop
{
  [DllImport("user32.dll")]
  internal static extern IntPtr GetActiveWindow();

  [DllImportAttribute("user32.dll", SetLastError=true)]  
  [return: MarshalAsAttribute(UnmanagedType.Bool)]  
  internal static extern bool GetWindowRect([InAttribute] IntPtr hWnd, [OutAttribute] out RECT lpRect);
  
  [DllImport("user32.dll", EntryPoint="GetWindowLongPtr")]
  private static extern IntPtr GetWindowLongPtr_Internal(IntPtr hWnd, int nIndex);
  
  [DllImport("user32.dll", EntryPoint="SetWindowLongPtr")]
  private static extern IntPtr SetWindowLongPtr_Internal(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
  
  [DllImport("user32.dll", EntryPoint="ShowWindow")]
  private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  
  [DllImport("user32.dll", SetLastError=true)]
  private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);
  
  [DllImport("user32.dll", EntryPoint="GetClientRect")]
  static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
  
  [DllImport("user32.dll", EntryPoint="AdjustWindowRect")]
  static extern bool AdjustWindowRect(ref RECT lpRect, uint dwStyle, bool bMenu);

  private const int WindowStylesIndex = -16;
  private const int WindowExtendedStylesIndex = -20;

  public static NativeWindowStyle GetWindowStyle(IntPtr hWnd)
    => (NativeWindowStyle)GetWindowLongPtr_Internal(hWnd, WindowStylesIndex).ToInt64();
  
  public static void SetWindowStyle(IntPtr hWnd, NativeWindowStyle style, bool retainClientSize = false)
  {
    if (!retainClientSize)
    {
      SetWindowLongPtr_Internal(hWnd, WindowStylesIndex, (IntPtr)style);
      SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SetWindowPosFlags.RefreshStyle);
    }
    else
    {
      if (!GetClientRect(hWnd, out var origClientRect))
        return;
      if (!GetWindowRect(hWnd, out var origWindowRect))
        return;
      var oldStyle = GetWindowStyle(hWnd);
      SetWindowLongPtr_Internal(hWnd, WindowStylesIndex, (IntPtr)style);
      // Plugin.Log.LogDebug("AdjustingRect");
      RECT requiredWindowSize = origClientRect;
      RECT prevWindowSize = origClientRect;
      AdjustWindowRect(ref prevWindowSize, (uint)oldStyle, false);
      AdjustWindowRect(ref requiredWindowSize, (uint)style, false);
      // LogRect(requiredWindowSize);
      var width = requiredWindowSize.right - requiredWindowSize.left;
      var height = requiredWindowSize.bottom - requiredWindowSize.top;
      var xAdj = prevWindowSize.left - requiredWindowSize.left;
      var yAdj = prevWindowSize.top - requiredWindowSize.top;
      SetWindowPos(hWnd, IntPtr.Zero, origWindowRect.left - xAdj, origWindowRect.top - yAdj, width, height, SetWindowPosFlags.RefreshSize);
    }
  }

  // private static void LogRect(RECT rect)
  // {
  //   Plugin.Log.LogDebug(rect.left + " " + rect.top + " " + (rect.right - rect.left) + " " + (rect.bottom - rect.top));
  // }

  public static bool SetWindowState(IntPtr hWnd, ShowCommand cmd)
    => ShowWindow(hWnd, (int)cmd);
}

[Flags]
public enum NativeWindowStyle : long
{
  Border = 0x00800000L,
  Caption = 0x00C00000L,
  Child = 0x40000000L,
  ChildWindow = 0x40000000L,
  ClipChildren = 0x02000000L,
  ClipSiblings = 0x04000000L,
  Disabled = 0x08000000L,
  DialogFrame = 0x00400000L,
  Group = 0x00020000L,
  HorizontalScroll = 0x00100000L,
  Iconic = 0x20000000L,
  Maximize = 0x01000000L,
  MaximizeBox = 0x00010000L,
  Minimize = 0x20000000L,
  MinimizeBox = 0x00020000L,
  Overlapped = 0x00000000L,
  OverlappedWindow = Overlapped | Caption | SysMenu | ThickFrame | MinimizeBox | MaximizeBox,
  Popup = 0x80000000L,
  PopupWindow = Popup | Border | SysMenu,
  SizeBox = 0x00040000L,
  SysMenu = 0x00080000L,
  TabStop = 0x00010000L,
  ThickFrame = 0x00040000L,
  Tiled = 0x00000000L,
  TiledWindow = Overlapped | Caption | SysMenu | ThickFrame | MinimizeBox | MaximizeBox,
  Visible = 0x10000000L,
  VerticalScroll = 0x00200000L
}

public enum ShowCommand
{
  Hide = 0,
  ShowNormal = 1,
  ShowMinimized = 2,
  ShowMaximized = 3,
  ShowNormalNoActivate = 4,
  Show = 5,
  Minimize = 6,
  ShowMinimizedNoActivate = 7,
  ShowNoActivate = 8,
  Restore = 9,
  ShowDefault = 10,
  ForceMinimize = 11
}

[Flags]
public enum SetWindowPosFlags : uint
{
  AsyncWindowPos = 0x4000,
  DeferErase = 0x2000,
  DrawFrame = 0x0020,
  FrameChanged = 0x0020,
  HideWindow = 0x0080,
  NoActivate = 0x0010,
  NoCopyBits = 0x0100,
  NoMove = 0x0002,
  NoOwnerZOrder = 0x0200,
  NoRedraw = 0x0008,
  NoReposition = 0x0200,
  NoSendChanging = 0x0400,
  NoSize = 0x0001,
  NoZOrder = 0x0004,
  ShowWindow = 0x0040,
  RefreshStyle = NoMove | NoSize | NoZOrder | FrameChanged,
  RefreshSize = NoZOrder | FrameChanged
}