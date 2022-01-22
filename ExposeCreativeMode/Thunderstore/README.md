# ExposeCreativeMode Mod
This mod allows you to enable "Creative Mode" and play without limitations (Well, atleast without a few limitations)  
Note that this most of the functionality in this mod will tip off abnormality detection in-game, so do not use it on a save which you want
to upload to the Milky Way.

## How to use this mod
* First install the mod, and reboot the game.
* Press Shift + F4 to toggle creative mode  
  (**Note**: Enabling creative mode will auto-enable Infinite Inventory, Instant Build and Instant Research)
* You need to be in creative mode to use the rest of the functions below

## Functions

All controls are rebindable. Detailed explanation of functions are given further below.  

| Function                          | Default Keybind                | Description |
| --------------------------------- | ------------------------------ | ----------- |
| Infinite&nbsp;Inventory           | Ctrl&nbsp;+&nbsp;Numpad&nbsp;1 | Toggles Infinite Inventory mode |
| Instant&nbsp;Build                | Ctrl&nbsp;+&nbsp;Numpad&nbsp;2 | Toggles Instant Build mode |
| Instant&nbsp;Research             | Ctrl&nbsp;+&nbsp;Numpad&nbsp;6 | Toggles Instant Research mode |
| Lock&nbsp;Research                | L                              | While Instant Research mode is active, hold this keybind to toggle locking tech |
| Infinite&nbsp;Power               | -                              | Auto-enabled when in creative mode  |
| Infinite&nbsp;Reach               | -                              | Auto-enabled when in creative mode |
| Unlock&nbsp;all&nbsp;tech         | Ctrl&nbsp;+&nbsp;T             | Press keybind to unlock all tech<br>5 levels of each infinite tech will be unlocked |
| Flatten&nbsp;Terrain              | Numpad&nbsp;3                  | Foundations entire planet with the "no decoration" option |
| Restore&nbsp;Terrain              | -                              | Hold a combination of the Ctrl and Shift keys while pressing the "Flatten Terrain" keybind to restore terrain<br><br>Ctrl - Restore shallow oceans<br>Shift - Restore mid-level oceans<br>Ctrl + Shift - Restore deep oceans |
| Bury&nbsp;/&nbsp;Raise&nbsp;Veins | Numpad&nbsp;4                  | Toggles bury/raise all veins on the planet |
| Infinite&nbsp;Station             | Ctrl&nbsp;+&nbsp;Numpad&nbsp;0 | Toggles Infinite Station mode |

<a name="infinite-inventory-mode"></a>
### Infinite Inventory mode

* Makes the player inventory have all items present in the game
* All items are reset to 9999, with the stack size being 30000 every frame
* Because the stack is greater, items which go into the inventory are effectively "deleted"  

(**Note**: Infinite Inventory is skipped when saving game data, so you will have your previous inventory when you load it the next time)

### Instant Research mode

* You can lock / unlock specific tech in the tech tree screen and also increase / decrease the research level of infinite research.  
* Hold the "Lock Research" keybind to lock / decrease research.  
* You can also use the Ctrl and Shift keys to modify the number of levels to increase / decrease.  
  | Modifier     | Lv.  |
  | ------------ | ---- |
  | None         |    1 |
  | Ctrl         |   10 |
  | Shift        |  100 |
  | Ctrl + Shift | 1000 |  

### Infinite Station mode

* This mode is primarily for testing blueprints, by making stations infinitely output to belt and consume from belt without needing any drones/vessels
* Makes all stations set to supply have items less than half of max
* Makes all stations set to demand have items more than half of max
* In the case of ILS, only remote supply/demand is checked. If remote is set to storage, then local is taken

### Infinite Power
* All buildings connected to a power network will work at 100% regardless of the amount of power supply
* Buildings not connected to a power network will still **NOT** work

### Infinite Reach
* You can now inspect, interact with, build and delete items across the entire planet. Inspect works even in Planet View mode

## Contact / Feedback / Bug Reports
You can either find me on the DSP Discord's #modding channel  
Or you can create an issue on [GitHub](https://github.com/Velociraptor115/DSPMods)  
\- Raptor#4825

## Changelog

### [v0.0.12](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.12/)
* Fix compatibility issue between BetterMachines and UnlockAllTech / InfiniteResearch

### [v0.0.11](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.11/)
* Fix InfinitePower for Spray Coaters, Pilers and Traffic Monitors
* Fix InfiniteResearch for the new tech from the recent update. The extra storage space for logistics stations should get properly applied now
* InfiniteInventory now gives infinite soil as well

### [v0.0.10](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.10/)

* Updated code for game version 0.9.24.11182
* Added ability to set the foundation "level" of the planet

### [v0.0.9](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.9/)

* Added support for lock / unlock of tech while "Instant Research" is active
* Handled the scenario where inventory capacity research may be completed while "Infinite Inventory" is active

### [v0.0.8](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.8/)

* Added "Infinite Power" and "Infinite Reach"
* Modified the foundation color used for "Flatten Planet"
* Added a toggle to bury/raise all veins on the planet

### [v0.0.7](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.7/)

* Fixed an [issue](https://github.com/Velociraptor115/DSPMods/issues/4) with Creative Mode not detecting inputs properly
* Fixed (hopefully) an [issue](https://github.com/Velociraptor115/DSPMods/issues/5) with Instant Build using up too much CPU after building big blueprints
* Changed "Instant Research" into a toggle

### [v0.0.6](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.6/)

* Fixed an issue with "Instant Build" throwing an exception when not on a planet

### [v0.0.5](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.5/)

* Added "Instant Build" functionality
* Auto-enable "Infinite Inventory" and "Instant Build" when enabling "Creative Mode". They can be individually toggled off later if you do not want to use them.
* Fixed various UI issues with "Infinite Inventory"
* "Infinite Inventory" is no longer saved as part of game data, so you can load the save with your previous inventory
* Fixed "Creative Mode" UI text still being visible on quitting to main menu or loading another game

### [v0.0.4](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.4/)

* Removed some of the redundant options and consolidated them
* Infinite Inventory now shows "(Infinite)" on the inventory UI Window
* Added support for rebinding controls

### [v0.0.3](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.3/)

* Added contact information

### [v0.0.2](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.2/)

* Uploaded source code to GitHub  
* Added a couple of functions for testing blueprints and mods

### [v0.0.1](https://dsp.thunderstore.io/package/Raptor/ExposeCreativeMode/0.0.1/)

* Initial version that just exposes already existing mode