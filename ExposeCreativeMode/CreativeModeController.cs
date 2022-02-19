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

    bool veinsBury = false;

    private bool active = false;

    public bool Active
    {
      get => active;
      set => SetActive(value);
    }

    Player player;
    InfiniteInventory infiniteInventory;
    InfiniteStation infiniteStation;
    InfiniteReach infiniteReach;
    InfinitePower infinitePower;
    InstantResearch instantResearch;
    InstantBuild instantBuild;
    InstantReplicate instantReplicate;

    public void Free()
    {
      InstantReplicatePatch.Unregister(instantReplicate);
      InstantResearchPatch.Unregister(instantResearch);
      InfiniteReachPatch.Unregister(infiniteReach);
      InfinitePowerPatch.Unregister(infinitePower);
      InfiniteInventoryPatch.Unregister(infiniteInventory);
      Active = false;
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
      instantBuild = new InstantBuild(player);
      InstantReplicatePatch.Register(instantReplicate = new InstantReplicate());
    }

    public void OnInputUpdate()
    {
      if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleCreativeMode).keyValue)
      {
        Active = !Active;
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
          PlanetReform.SetPlanetModLevel(player.factory, veinsBury, modLevel);
        }
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleAllVeinsOnPlanet).keyValue && player.factory != null)
        {
          veinsBury = !veinsBury;
          PlanetReform.ModifyAllVeinsHeight(player.factory, veinsBury);
        }
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInstantResearch).keyValue)
          instantResearch.Toggle();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteInventory).keyValue)
          infiniteInventory.Toggle();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInfiniteStation).keyValue)
          infiniteStation.Toggle();
        if (CustomKeyBindSystem.GetKeyBind(KeyBinds.ToggleInstantBuild).keyValue)
          instantBuild.Toggle();
      }
    }

    public void SetActive(bool value)
    {
      if (value == active)
        return;
      active = value;
      
      // Auto-enable commonly used creative mode functions
      if (active)
      {
        // Disable achievements for the save
        player.controller.gameData.gameDesc.achievementEnable = false;

        if (CreativeModeConfig.autoEnableInfiniteInventory.Value)
          infiniteInventory.IsEnabled = true;
        if (CreativeModeConfig.autoEnableInstantBuild.Value)
          instantBuild.IsEnabled = true;
        if (CreativeModeConfig.autoEnableInstantResearch.Value)
          instantResearch.IsEnabled = true;
        if (CreativeModeConfig.autoEnableInfiniteReach.Value)
          infiniteReach.IsEnabled = true;
        if (CreativeModeConfig.autoEnableInfinitePower.Value)
          infinitePower.IsEnabled = true;
        if (CreativeModeConfig.autoEnableInstantReplicate.Value)
          instantReplicate.IsEnabled = true;
      }
      else
      {
        // Disable all creative mode functions when creative mode is disabled
        infiniteInventory.IsEnabled = false;
        infiniteStation.IsEnabled = false;
        instantBuild.IsEnabled = false;
        instantResearch.IsEnabled = false;
        infiniteReach.IsEnabled = false;
        infinitePower.IsEnabled = false;
        instantReplicate.IsEnabled = false;
      }
      OnActiveChange(active);
    }

    public void GameTick()
    {
      infiniteInventory.GameTick();
      infiniteStation.GameTick();
      instantResearch.GameTick();
      instantBuild.GameTick();
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