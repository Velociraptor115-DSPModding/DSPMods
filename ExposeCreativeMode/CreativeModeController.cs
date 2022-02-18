using System.Collections.Generic;
using UnityEngine;
using CommonAPI.Systems;

namespace DysonSphereProgram.Modding.ExposeCreativeMode
{
  public class CreativeModeController
  {
    const string uiCreativeModeContainerPath = "UI Root/Overlay Canvas/In Game";
    const string uiCreativeModeTextName = "creative-mode-text";
    const string uiCreativeModeTextPath = uiCreativeModeContainerPath + "/" + uiCreativeModeTextName;
    const string uiVersionTextName = "version-text";
    const string uiVersionTextPath = uiCreativeModeContainerPath + "/" + uiVersionTextName;
    const float uiCreativeModeTextOffset = 0.55f;

    bool isInstantBuildActive = false;
    bool veinsBury = false;

    bool active = false;

    Player player;
    InfiniteInventory infiniteInventory;
    InfiniteStation infiniteStation;
    InfiniteReach infiniteReach;
    InfinitePower infinitePower;
    InstantResearch instantResearch;

    public void Free()
    {
      if (infiniteInventory.IsEnabled)
        infiniteInventory.Disable();
      if (infiniteStation.IsEnabled)
        infiniteStation.Disable();
      if (isInstantBuildActive)
        ToggleInstantBuild();
      if (instantResearch.IsEnabled)
        instantResearch.Disable();
      if (infiniteReach.IsEnabled)
        infiniteReach.Disable();
      OnActiveChange(false);
      InstantResearchPatch.Unregister(instantResearch);
      InfiniteReachPatch.Unregister(infiniteReach);
      InfinitePowerPatch.Unregister(infinitePower);
      InfiniteInventoryPatch.Unregister(infiniteInventory);
      player = null;
    }

    public void Init(Player _player)
    {
      player = _player;
      InfiniteInventoryPatch.Register(infiniteInventory = new InfiniteInventory(player));
      InfinitePowerPatch.Register(infinitePower = new InfinitePower());
      InfiniteReachPatch.Register(infiniteReach = new InfiniteReach(player));
      InstantResearchPatch.Register(instantResearch = new InstantResearch());
      InfiniteResearchHelper.Reinitialize();
      infiniteStation = new InfiniteStation();
    }

    public void OnInputUpdate()
    {
      if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleCreativeMode).keyValue)
      {
        active = !active;
        OnActiveChange(active);

        // Auto-enable commonly used creative mode functions
        if (active)
        {
          // Disable achievements for the save
          player.controller.gameData.gameDesc.achievementEnable = false;

          if (!infiniteInventory.IsEnabled)
            infiniteInventory.Enable();
          if (!isInstantBuildActive)
            ToggleInstantBuild();
          if (!instantResearch.IsEnabled)
            instantResearch.Enable();
          if (!infiniteReach.IsEnabled)
            infiniteReach.Enable();
          if (!infinitePower.IsEnabled)
            infinitePower.Enable();
        }
        else
        {
          // Disable all creative mode functions when creative mode is disabled
          if (infiniteInventory.IsEnabled)
            infiniteInventory.Disable();
          if (infiniteStation.IsEnabled)
            infiniteStation.Disable();
          if (isInstantBuildActive)
            ToggleInstantBuild();
          if (instantResearch.IsEnabled)
            instantResearch.Disable();
          if (infiniteReach.IsEnabled)
            infiniteReach.Disable();
          if (infinitePower.IsEnabled)
            infinitePower.Disable();
        }
      }

      if (active)
      {
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.UnlockAllPublishedTech).keyValue)
          InfiniteResearchHelper.UnlockAllTech();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.FlattenPlanet).keyValue && player.factory != null)
        {
          Plugin.Log.LogDebug("Flatten Keybind pressed");
          var ctrlHeld = (CombineKey.currModifier & 2) == 2;
          var shiftHeld = (CombineKey.currModifier & 1) == 1;
          var modLevel = (shiftHeld, ctrlHeld) switch
          {
            (true, true) => 0,
            (true, false) => 1,
            (false, true) => 2,
            (false, false) => 3
          };
          CreativeModeFunctions.FlattenPlanet(player.factory, veinsBury, modLevel);
        }
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleAllVeinsOnPlanet).keyValue && player.factory != null)
        {
          veinsBury = !veinsBury;
          CreativeModeFunctions.ModifyAllVeinsHeight(player.factory, veinsBury);
        }
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInstantResearch).keyValue)
          instantResearch.Toggle();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteInventory).keyValue)
          infiniteInventory.Toggle();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteStation).keyValue)
          infiniteStation.Toggle();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInstantBuild).keyValue)
          ToggleInstantBuild();
      }
    }

    public void GameTick()
    {
      infiniteInventory.GameTick();
      infiniteStation.GameTick();

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

      instantResearch.GameTick();
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
  }
}