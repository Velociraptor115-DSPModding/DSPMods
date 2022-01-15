using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using CommonAPI;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public static class InfiniteInventory
  {
    public static int GetRequiredSize()
    {
      var items = LDB.items.dataArray;
      var itemCount = items.Length;
      var colCount = UIRoot.instance.uiGame.inventory.colCount;
      // We need to set extra size, otherwise the UI bugs out when dropping items on the extra space
      // by throwing an IndexOutOfRange exception
      return itemCount + (itemCount % colCount > 0 ? colCount - (itemCount % colCount) : 0);
    }

    public static StorageComponent Create()
    {
      var items = LDB.items.dataArray;
      var size = GetRequiredSize();
      var storage = new StorageComponent(size);
      storage.type = EStorageType.Filtered;
      for (int i = 0; i < items.Length; ++i)
      {
        var item = items[i];
        storage.grids[i].itemId = item.ID;
        storage.grids[i].filter = item.ID;
        storage.grids[i].stackSize = 30000;
        storage.grids[i].count = 9999;
      }
      for (int i = items.Length; i < size; i++)
      {
        storage.grids[i].itemId = 0;
        // We need to do this, because filter <= 0 is considered fair game by the UI
        storage.grids[i].filter = int.MaxValue;
        storage.grids[i].stackSize = 0;
        storage.grids[i].count = 0;
      }

      return storage;
    }
  }

  public interface IInfiniteInventoryProvider
  {
    StorageComponent Storage { get; }
    bool IsEnabled { get; }

    void Enable();
    void Disable();
  }

  [HarmonyPatch]
  public static class InfiniteInventoryPatch
  {
    private static IInfiniteInventoryProvider provider;

    public static void Register(IInfiniteInventoryProvider p)
    {
      provider = p;
    }

    public static void Unregister(IInfiniteInventoryProvider p)
    {
      if (provider == p)
        provider = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStorageGrid), nameof(UIStorageGrid.OnStorageContentChanged))]
    static void EnsureThatInfiniteInventoryHasProperTextColors(UIStorageGrid __instance)
    {
      var infiniteInventory = provider?.Storage;
      if (infiniteInventory == null || __instance.storage != infiniteInventory)
        return;

      for (int i = 0; i < __instance.numTexts.Length; i++)
      {
        if (__instance.numTexts[i] != null)
          __instance.numTexts[i].color = __instance.prefabNumText.color;
      }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    static void BeforeSaveCurrentGame(ref bool __state)
    {
      __state = false;
      if (provider != null && provider.IsEnabled)
      {
        __state = true;
        provider.Disable();
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    static void AfterSaveCurrentGame(ref bool __state)
    {
      if (__state && provider != null)
      {
        provider.Enable();
      }
    }
  }
}