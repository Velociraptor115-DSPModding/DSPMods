using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public class InfiniteInventory
  {
    private readonly Player player;
    private readonly StorageComponent infiniteInventory;
    
    private int? sandRestore;
    StorageComponent infiniteInventoryRestore;

    private int prevInventorySize;
    private IList<int> cachedItemIds;
    private float cachedTime;
    private bool isEnabled;

    public bool IsEnabled
    {
      get => isEnabled;
      set
      {
        if (isEnabled == value)
          return;
        if (value) Enable(); else Disable();
      }
    }

    public bool IncludeLocked = true;
    public StorageComponent storage => infiniteInventory;

    public InfiniteInventory(Player player)
    {
      this.player = player;
      infiniteInventory = new StorageComponent(LDB.items.Length)
      {
        type = EStorageType.Filtered
      };
    }

    public void Enable()
    {
      sandRestore = player.sandCount;
      infiniteInventoryRestore = player.package;
      
      // Because we change the storage component itself, the UI will not know the
      // underlying object has changed entirely and will continue to display
      // the old storage component till we close and reopen it.
      using (InventoryWindowDeactivatedScope)
      {
        RefreshCachedItemIds();
        infiniteInventory.SetSize(GetRequiredSize(cachedItemIds.Count));
        player.package = infiniteInventory;
        prevInventorySize = infiniteInventory.size;
      }
      UpdateUI(true);

      isEnabled = true;
      Plugin.Log.LogDebug("Infinite Inventory Enabled");
    }

    public void Disable()
    {
      if (infiniteInventoryRestore != null)
      {
        using (InventoryWindowDeactivatedScope)
          player.package = infiniteInventoryRestore;
        infiniteInventoryRestore = null;
      }
      if (sandRestore.HasValue)
        player.sandCount = sandRestore.Value;
      sandRestore = null;
      UpdateUI(false);

      isEnabled = false;
      Plugin.Log.LogDebug("Infinite Inventory Disabled");
    }

    public void Toggle()
    {
      if (!isEnabled)
        Enable();
      else
        Disable();
    }

    private void UpdateUI(bool enabled)
    {
      var inventoryWindowTitle =
        GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Player Inventory/panel-bg/title-text");
      var title = inventoryWindowTitle?.GetComponent<UnityEngine.UI.Text>();

      if (title != null)
      {
        if (enabled)
          title.text = title.text.Replace("(Infinite)", "").Trim() + " (Infinite)";
        else
          title.text = title.text.Replace("(Infinite)", "").Trim();
      }
    }

    public void GameTick()
    {
      if (!isEnabled)
        return;

      if (Time.time - cachedTime > 1f)
        RefreshCachedItemIds();
      var requiredSize = GetRequiredSize(cachedItemIds.Count);
      if (player.package.size != prevInventorySize)
      {
        // Probably an inventory size upgrade, so we need to forward it to the actual inventory
        infiniteInventoryRestore.SetSize(player.package.size);
      }
      if (player.package.size != requiredSize)
        player.package.SetSize(requiredSize);
      prevInventorySize = player.package.size;
      RestockInfiniteInventory(cachedItemIds);
      player.sandCount = 999999;
    }

    private void RefreshCachedItemIds()
    {
      cachedItemIds = GetItemIdsFiltered();
      cachedTime = Time.time;
    }

    private void RestockInfiniteInventory(IList<int> itemIds)
    {
      var grids = infiniteInventory.grids;
      
      StorageComponent.GRID tmpGrid;

      static bool IsEqual(in StorageComponent.GRID g1, in StorageComponent.GRID g2) =>
        g1.itemId == g2.itemId
        && g1.filter == g2.filter
        && g1.stackSize == g2.stackSize
        && g1.count == g2.count
        && g1.inc == g2.inc;

      bool hasChanged = false;
      
      tmpGrid.stackSize = 30000;
      tmpGrid.count = 9999;
      tmpGrid.inc = 0;
      for (int i = 0; i < itemIds.Count; ++i)
      {
        tmpGrid.itemId = tmpGrid.filter = itemIds[i];
        if (!IsEqual(grids[i], tmpGrid))
        {
          hasChanged = true;
          grids[i] = tmpGrid;
        }
      }

      tmpGrid.itemId = 0;
      // We need to do this, because filter <= 0 is considered fair game by the UI
      tmpGrid.filter = int.MaxValue;
      tmpGrid.stackSize = tmpGrid.count = tmpGrid.inc = 0;
      for (int i = itemIds.Count; i < infiniteInventory.size; i++)
      {
        if (!IsEqual(grids[i], tmpGrid))
        {
          hasChanged = true;
          grids[i] = tmpGrid;
        }
      }

      if (hasChanged)
        infiniteInventory.NotifyStorageChange();
    }

    private IList<int> GetItemIdsFiltered() => LDB.items.dataArray.Where(itemFilter).Select(x => x.ID).ToList();
    private Func<ItemProto, bool> itemFilter => IncludeLocked ? AllItemsPredicate : OnlyUnlockedItemsPredicate; 
    private static bool OnlyUnlockedItemsPredicate(ItemProto item) => GameMain.history.ItemUnlocked(item.ID);
    private static bool AllItemsPredicate(ItemProto item) => true;

    private static int GetRequiredSize(int count)
    {
      var colCount = UIRoot.instance.uiGame.inventory.colCount;
      // We need to set extra size, otherwise the UI bugs out when dropping items on the extra space
      // by throwing an IndexOutOfRange exception
      return count + (count % colCount > 0 ? colCount - (count % colCount) : 0);
    }

    private static InventoryWindowDeactivationContext InventoryWindowDeactivatedScope =>
      new InventoryWindowDeactivationContext();

    private readonly ref struct InventoryWindowDeactivationContext
    {
      private readonly bool wasActive;
      private readonly UIGame uiGame;
      public InventoryWindowDeactivationContext()
      {
        uiGame = UIRoot.instance && UIRoot.instance.uiGame ? UIRoot.instance.uiGame : null;
        wasActive = uiGame && uiGame.inventory && uiGame.inventory.active;
        if (wasActive)
          uiGame.ShutPlayerInventory();
      }

      public void Dispose()
      {
        if (wasActive)
          uiGame.OpenPlayerInventory();
      }
    }
    
    public void PreserveVanillaSaveBefore()
    {
      if (!isEnabled)
        return;
      
      if (infiniteInventoryRestore != null)
        player.package = infiniteInventoryRestore;
      if (sandRestore.HasValue)
        player.sandCount = sandRestore.Value;
    }

    public void PreserveVanillaSaveAfter()
    {
      if (!isEnabled)
        return;

      player.package = infiniteInventory;
      player.sandCount = 999999;
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
      if (infiniteInventory is not { IsEnabled: true })
        return;
      
      __state = true;
      infiniteInventory.Disable();
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