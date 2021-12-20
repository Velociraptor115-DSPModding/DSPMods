using System.Collections.Generic;
using UnityEngine;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public class PlayerAction_CreativeMode : PlayerAction, IInfiniteInventoryProvider
  {
    const string uiCreativeModeContainerPath = "UI Root/Overlay Canvas/In Game";
    const string uiCreativeModeTextName = "creative-mode-text";
    const string uiCreativeModeTextPath = uiCreativeModeContainerPath + "/" + uiCreativeModeTextName;
    const string uiVersionTextName = "version-text";
    const string uiVersionTextPath = uiCreativeModeContainerPath + "/" + uiVersionTextName;
    const float uiCreativeModeTextOffset = 0.55f;

    bool isInfiniteInventoryActive = false;
    bool isInfiniteStationActive = false;
    bool isInstantBuildActive = false;
    StorageComponent infiniteInventoryRestore;
    StorageComponent infiniteInventory;

    bool active = false;

    StorageComponent IInfiniteInventoryProvider.Storage => infiniteInventory;
    bool IInfiniteInventoryProvider.IsEnabled => isInfiniteInventoryActive;

    public override void Free()
    {
      if (isInfiniteInventoryActive)
        ToggleInfiniteInventory();
      if (isInfiniteStationActive)
        ToggleInfiniteStation();
      if (isInstantBuildActive)
        ToggleInstantBuild();
      OnActiveChange(false);
      InfiniteInventoryPatch.Unregister(this);
      base.Free();
    }

    public override void Init(Player _player)
    {
      base.Init(_player);
      InfiniteInventoryPatch.Register(this);
    }

    public override void GameTick(long timei)
    {

      if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleCreativeMode).keyValue)
      {
        active = !active;
        OnActiveChange(active);

        // Auto-enable commonly used creative mode functions
        if (active)
        {
          if (!isInfiniteInventoryActive)
            ToggleInfiniteInventory();
          if (!isInstantBuildActive)
            ToggleInstantBuild();
        }
        else
        {
          // Disable all creative mode functions when creative mode is disabled
          if (isInfiniteInventoryActive)
            ToggleInfiniteInventory();
          if (isInfiniteStationActive)
            ToggleInfiniteStation();
          if (isInstantBuildActive)
            ToggleInstantBuild();
        }
      }

      if (active)
      {
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.UnlockAllPublishedTech).keyValue)
          CreativeModeFunctions.UnlockAllPublishedTech(this);
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.CoverPlanetInFoundation).keyValue)
          CreativeModeFunctions.CoverPlanetInFoundation(this);
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ResearchCurrentTechInstantly).keyValue)
          CreativeModeFunctions.ResearchCurrentTechInstantly(this);
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteInventory).keyValue)
          ToggleInfiniteInventory();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteStation).keyValue)
          ToggleInfiniteStation();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInstantBuild).keyValue)
          ToggleInstantBuild();
      }

      if (isInfiniteInventoryActive)
      {
        var inventory = this.player.package.grids;

        var items = LDB.items.dataArray;
        for (int i = 0; i < items.Length; ++i)
        {
          var item = items[i];
          inventory[i].itemId = item.ID;
          inventory[i].filter = item.ID;
          inventory[i].stackSize = 30000;
          inventory[i].count = 9999;
        }
      }

      if (isInfiniteStationActive)
      {
        for (int factoryIdx = 0; factoryIdx < GameMain.data.factoryCount; factoryIdx++)
        {
          PlanetTransport transport = GameMain.data.factories[factoryIdx].transport;
          if (transport != null)
          {
            for (int stationIdx = 1; stationIdx < transport.stationCursor; stationIdx++)
            {
              if (transport.stationPool[stationIdx] != null && transport.stationPool[stationIdx].id == stationIdx)
              {
                var ss = transport.stationPool[stationIdx].storage;
                for (int i = 0; i < ss.Length; i++)
                {
                  if (ss[i].itemId > 0)
                  {
                    var logic = ss[i].remoteLogic == ELogisticStorage.None ? ss[i].localLogic : ss[i].remoteLogic;
                    if (logic == ELogisticStorage.Supply && ss[i].count > ss[i].max / 2)
                    {
                      ss[i].count = ss[i].max / 2;
                    }
                    else if (logic == ELogisticStorage.Demand && ss[i].count < ss[i].max / 2)
                    {
                      ss[i].count = ss[i].max / 2;
                    }
                  }
                }
              }
            }
          }
        }
      }

      if (isInstantBuildActive)
      {
        if (player.factory != null)
        {
          for (int i = 0; i < player.factory.prebuildPool.Length; i++)
          {
            ref var prebuild = ref player.factory.prebuildPool[i];
            if (prebuild.itemRequired > 0)
            {
              int protoId = prebuild.protoId;
              int itemRequired = prebuild.itemRequired;
              player.package.TakeTailItems(ref protoId, ref itemRequired, false);
              prebuild.itemRequired -= itemRequired;
              player.factory.AlterPrebuildModelState(i);
            }
            if (prebuild.itemRequired <= 0)
            {
              player.factory.BuildFinally(player, prebuild.id);
            }
          }
        }
      }
    }

    public void ToggleInfiniteStation()
    {
      isInfiniteStationActive = !isInfiniteStationActive;
      if (isInfiniteStationActive)
      {
        Debug.Log("Infinite Station Enabled");
      }
      else
      {
        Debug.Log("Infinite Station Disabled");
      }
    }

    public void ToggleInstantBuild()
    {
      isInstantBuildActive = !isInstantBuildActive;
      if (isInstantBuildActive)
      {
        Debug.Log("Instant Build Enabled");
      }
      else
      {
        Debug.Log("Instant Build Disabled");
      }
    }

    public void ToggleInfiniteInventory()
    {
      isInfiniteInventoryActive = !isInfiniteInventoryActive;
      if (isInfiniteInventoryActive)
      {
        infiniteInventoryRestore = this.player.package;
        UIRoot.instance?.uiGame.TogglePlayerInventory();
        // Force the UI to recalculate stuff
        // Because we change the storage component itself, the UI will not know the
        // underlying object has changed entirely and will continue to display
        // the old storage component till we close and reopen it. Hence the TogglePlayerInventory()
        this.infiniteInventory = InfiniteInventory.Create();
        this.player.package = infiniteInventory;
        UIRoot.instance?.uiGame.TogglePlayerInventory();

        Debug.Log("Infinite Inventory Enabled");
      }
      else
      {
        if (infiniteInventoryRestore != null)
        {
          UIRoot.instance?.uiGame.TogglePlayerInventory();
          this.player.package = infiniteInventoryRestore;
          infiniteInventoryRestore = null;
          UIRoot.instance?.uiGame.TogglePlayerInventory();
        }
        this.infiniteInventory = null;
        Debug.Log("Infinite Inventory Disabled");
      }

      var inventoryWindowTitle = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Player Inventory/panel-bg/title-text");
      var title = inventoryWindowTitle?.GetComponent<UnityEngine.UI.Text>();

      if (title != null)
      {
        if (isInfiniteInventoryActive)
          title.text = title.text.Replace("(Infinite)", "").Trim() + " (Infinite)";
        else
          title.text = title.text.Replace("(Infinite)", "").Trim();
      }
    }

    void OnActiveChange(bool active)
    {
      var creativeModeText = GameObject.Find(uiCreativeModeTextPath);
      if (creativeModeText == null)
        creativeModeText = MakeCreativeModeTextUI();
      if (creativeModeText == null)
        return;

      var uiTextComponent = creativeModeText.GetComponent<UnityEngine.UI.Text>();
      uiTextComponent.text = active ? "Creative Mode" : "";
    }

    static GameObject MakeCreativeModeTextUI()
    {
      var versionText = GameObject.Find(uiVersionTextPath);
      if (versionText == null)
        return null;
      var creativeModeText = Object.Instantiate(versionText, versionText.transform.parent);
      creativeModeText.name = uiCreativeModeTextName;
      var componentToRemove = creativeModeText.GetComponent<UIVersionText>();
      if (componentToRemove != null)
        Object.Destroy(componentToRemove);
      creativeModeText.transform.Translate(Vector2.down * uiCreativeModeTextOffset);
      // By default this gets added as last child, so it shows up in the Esc menu, blueprint menu, etc.
      // So we set it's index next to the version-text
      creativeModeText.transform.SetSiblingIndex(versionText.transform.GetSiblingIndex() + 1);
      return creativeModeText;
    }

    void IInfiniteInventoryProvider.Enable()
    {
      if (!isInfiniteInventoryActive)
        ToggleInfiniteInventory();
    }

    void IInfiniteInventoryProvider.Disable()
    {
      if (isInfiniteInventoryActive)
        ToggleInfiniteInventory();
    }
  }
}