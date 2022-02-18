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
  public class InfiniteInventory
  {
    private readonly Player player;
    private int? sandRestore;
    StorageComponent infiniteInventoryRestore;
    StorageComponent infiniteInventory;

    public bool IsEnabled;
    public StorageComponent storage => infiniteInventory;

    public InfiniteInventory(Player player)
    {
      this.player = player;
    }

    public void Enable()
    {
      sandRestore = player.sandCount;
      infiniteInventoryRestore = player.package;
      UIRoot.instance?.uiGame.TogglePlayerInventory();
      // Force the UI to recalculate stuff
      // Because we change the storage component itself, the UI will not know the
      // underlying object has changed entirely and will continue to display
      // the old storage component till we close and reopen it. Hence the TogglePlayerInventory()
      infiniteInventory = Create(LDB.items.dataArray);
      player.package = infiniteInventory;
      UIRoot.instance?.uiGame.TogglePlayerInventory();
      UpdateUI();

      IsEnabled = true;
      Plugin.Log.LogDebug("Infinite Inventory Enabled");
    }

    public void Disable()
    {
      if (infiniteInventoryRestore != null)
      {
        UIRoot.instance?.uiGame.TogglePlayerInventory();
        player.package = infiniteInventoryRestore;
        infiniteInventoryRestore = null;
        UIRoot.instance?.uiGame.TogglePlayerInventory();
      }
      infiniteInventory = null;
      if (sandRestore.HasValue)
        player.sandCount = sandRestore.Value;
      sandRestore = null;
      UpdateUI();

      IsEnabled = false;
      Plugin.Log.LogDebug("Infinite Inventory Disabled");
    }

    public void Toggle()
    {
      if (!IsEnabled)
        Enable();
      else
        Disable();
    }

    private void UpdateUI()
    {
      var inventoryWindowTitle =
        GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Player Inventory/panel-bg/title-text");
      var title = inventoryWindowTitle?.GetComponent<UnityEngine.UI.Text>();

      if (title != null)
      {
        if (IsEnabled)
          title.text = title.text.Replace("(Infinite)", "").Trim() + " (Infinite)";
        else
          title.text = title.text.Replace("(Infinite)", "").Trim();
      }
    }

    public void GameTick()
    {
      if (!IsEnabled)
        return;

      var items = LDB.items.dataArray;
      var requiredSize = GetRequiredSize(items);
      if (this.player.package.size != requiredSize)
      {
        infiniteInventoryRestore.SetSize(this.player.package.size);
        this.player.package.SetSize(requiredSize);
      }
      var inventory = this.player.package.grids;
      for (int i = 0; i < items.Length; ++i)
      {
        var item = items[i];
        inventory[i].itemId = item.ID;
        inventory[i].filter = item.ID;
        inventory[i].stackSize = 30000;
        inventory[i].count = 9999;
      }

      player.sandCount = 999999;
    }

    private static int GetRequiredSize(ICollection<ItemProto> items)
    {
      var itemCount = items.Count;
      var colCount = UIRoot.instance.uiGame.inventory.colCount;
      // We need to set extra size, otherwise the UI bugs out when dropping items on the extra space
      // by throwing an IndexOutOfRange exception
      return itemCount + (itemCount % colCount > 0 ? colCount - (itemCount % colCount) : 0);
    }

    private static StorageComponent Create(IList<ItemProto> items)
    {
      var size = GetRequiredSize(items);
      var storage = new StorageComponent(size);
      storage.type = EStorageType.Filtered;
      for (int i = 0; i < items.Count; ++i)
      {
        var item = items[i];
        storage.grids[i].itemId = item.ID;
        storage.grids[i].filter = item.ID;
        storage.grids[i].stackSize = 30000;
        storage.grids[i].count = 9999;
      }
      for (int i = items.Count; i < size; i++)
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

  [HarmonyPatch]
  public static class InfiniteInventoryPatch
  {
    private static InfiniteInventory infiniteInventory;

    public static void Register(InfiniteInventory instance)
    {
      infiniteInventory = instance;
    }

    public static void Unregister(InfiniteInventory instance)
    {
      if (infiniteInventory == instance)
        infiniteInventory = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStorageGrid), nameof(UIStorageGrid.OnStorageContentChanged))]
    static void EnsureThatInfiniteInventoryHasProperTextColors(UIStorageGrid __instance)
    {
      if (infiniteInventory == null || __instance.storage != infiniteInventory.storage)
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
      if (infiniteInventory != null && infiniteInventory.IsEnabled)
      {
        __state = true;
        infiniteInventory.Disable();
      }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.SaveCurrentGame))]
    static void AfterSaveCurrentGame(ref bool __state)
    {
      if (__state && infiniteInventory != null)
      {
        infiniteInventory.Enable();
      }
    }
  }
}