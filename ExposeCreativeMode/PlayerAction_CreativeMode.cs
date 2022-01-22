using System.Collections.Generic;
using UnityEngine;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public class PlayerAction_CreativeMode : PlayerAction, IInfiniteInventoryProvider, IInfinitePowerProvider, IInfiniteReachProvider, IInfiniteResearchProvider
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
    bool isInstantResearchActive = false;
    bool isInfiniteReachActive = false;
    bool veinsBury = false;
    StorageComponent infiniteInventoryRestore;
    StorageComponent infiniteInventory;
    int? sandRestore;
    float buildAreaRestore;

    bool active = false;

    StorageComponent IInfiniteInventoryProvider.Storage => infiniteInventory;
    bool IInfiniteInventoryProvider.IsEnabled => isInfiniteInventoryActive;

    bool IInfinitePowerProvider.IsEnabled => active;

    bool IInfiniteReachProvider.IsEnabled => isInfiniteReachActive;

    bool IInfiniteResearchProvider.IsEnabled => isInstantResearchActive;

    public override void Free()
    {
      if (isInfiniteInventoryActive)
        ToggleInfiniteInventory();
      if (isInfiniteStationActive)
        ToggleInfiniteStation();
      if (isInstantBuildActive)
        ToggleInstantBuild();
      if (isInstantResearchActive)
        ToggleInstantResearch();
      OnActiveChange(false);
      InputHandlerPatch.Update -= OnInputUpdate;
      InfiniteResearchPatch.Unregister(this);
      InfiniteReachPatch.Unregister(this);
      InfinitePowerPatch.Unregister(this);
      InfiniteInventoryPatch.Unregister(this);
      base.Free();
    }

    public override void Init(Player _player)
    {
      base.Init(_player);
      InfiniteInventoryPatch.Register(this);
      InfinitePowerPatch.Register(this);
      InfiniteReachPatch.Register(this);
      InfiniteResearchPatch.Register(this);
      InputHandlerPatch.Update += OnInputUpdate;
      InfiniteResearchHelper.EnsureInitialized();
    }

    private void OnInputUpdate()
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
          if (!isInstantResearchActive)
            ToggleInstantResearch();
          if (!isInfiniteReachActive)
            ToggleInfiniteReach();
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
          if (isInstantResearchActive)
            ToggleInstantResearch();
          if (isInfiniteReachActive)
            ToggleInfiniteReach();
        }
      }

      if (active)
      {
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.UnlockAllPublishedTech).keyValue)
          InfiniteResearchHelper.UnlockAllTech();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.FlattenPlanet).keyValue)
        {
          Plugin.Log.LogDebug("Flatten Keybind pressed");
          var ctrlHeld = (CombineKey.currModifier & 2) == 2;
          var shiftHeld = (CombineKey.currModifier & 1) == 1;
          if (ctrlHeld && shiftHeld)
          {
            Plugin.Log.LogDebug("Flatten M3");
            CreativeModeFunctions.FlattenPlanetM3(this);
          }
          else if (shiftHeld)
          {
            Plugin.Log.LogDebug("Flatten M2");
            CreativeModeFunctions.FlattenPlanetM2(this);
          }
          else if (ctrlHeld)
          {
            Plugin.Log.LogDebug("Flatten M1");
            CreativeModeFunctions.FlattenPlanetM1(this);
          }
          else
          {
            Plugin.Log.LogDebug("Flatten");
          CreativeModeFunctions.FlattenPlanet(this);
          }
          veinsBury = true;
        }
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleAllVeinsOnPlanet).keyValue)
        {
          veinsBury = !veinsBury;
          CreativeModeFunctions.ModifyAllVeinsHeight(this, veinsBury);
        }
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInstantResearch).keyValue)
          ToggleInstantResearch();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteInventory).keyValue)
          ToggleInfiniteInventory();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteStation).keyValue)
          ToggleInfiniteStation();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInstantBuild).keyValue)
          ToggleInstantBuild();
      }
    }

    public override void GameTick(long timei)
    {
      if (isInfiniteInventoryActive)
      {
        var items = LDB.items.dataArray;
        var requiredSize = InfiniteInventory.GetRequiredSize();
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

        player.SetSandCount(999999);
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
        if (player.factory != null && player.factory.prebuildCount > 0)
        {
          void BuildInstantly(int prebuildIdx)
          {
            ref var prebuild = ref player.factory.prebuildPool[prebuildIdx];
            if (prebuild.id != prebuildIdx)
              return;
            if (prebuild.itemRequired > 0)
            {
              int protoId = prebuild.protoId;
              int itemRequired = prebuild.itemRequired;
              player.package.TakeTailItems(ref protoId, ref itemRequired, out _, false);
              prebuild.itemRequired -= itemRequired;
              player.factory.AlterPrebuildModelState(prebuildIdx);
            }
            if (prebuild.itemRequired <= 0)
            {
              player.factory.BuildFinally(player, prebuild.id);
            }
          }

          if (player.factory.prebuildRecycleCursor > 0)
          {
            // This means that we can probably get away with just looking at the recycle instances
            for (int i = player.factory.prebuildRecycleCursor; i < player.factory.prebuildCursor; i++)
              BuildInstantly(player.factory.prebuildRecycle[i]);
          }
          else
          {
            // Highly probable that a prebuildPool resize took place this tick.
            // Better to go over the entire array

            // Don't ask me why the loop starts from 1. I'm merely following `MechaDroneLogic.UpdateTargets()`
            for (int i = 1; i < player.factory.prebuildCursor; i++)
              BuildInstantly(i);
          }
        }
      }

      if (isInstantResearchActive)
      {
        CreativeModeFunctions.ResearchCurrentTechInstantly(this);
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

    public void ToggleInstantResearch()
    {
      isInstantResearchActive = !isInstantResearchActive;
      if (isInstantResearchActive)
      {
        Debug.Log("Instant Research Enabled");
      }
      else
      {
        Debug.Log("Instant Research Disabled");
      }
    }

    public void ToggleInfiniteInventory()
    {
      isInfiniteInventoryActive = !isInfiniteInventoryActive;
      if (isInfiniteInventoryActive)
      {
        sandRestore = this.player.sandCount;
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
        if (sandRestore.HasValue)
          this.player.SetSandCount(sandRestore.Value);
        sandRestore = null;
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

    public void ToggleInfiniteReach()
    {
      isInfiniteReachActive = !isInfiniteReachActive;
      if (isInfiniteReachActive)
      {
        buildAreaRestore = this.player.mecha.buildArea;
        this.player.mecha.buildArea = 600;
        Debug.Log("Instant Reach Enabled");
      }
      else
      {
        this.player.mecha.buildArea = buildAreaRestore;
        Debug.Log("Instant Reach Disabled");
      }
    }

    void IInfiniteReachProvider.Enable()
    {
      if (!isInfiniteReachActive)
        ToggleInfiniteReach();
    }

    void IInfiniteReachProvider.Disable()
    {
      if (isInfiniteReachActive)
        ToggleInfiniteReach();
    }
  }
}