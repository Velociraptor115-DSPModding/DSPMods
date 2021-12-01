# ExposeCreativeMode Mod
This mod merely exposes the "test mode" left in the game by the devs as "creative mode" and adds some text in the UI below the version and player info to indicate when creative mode is enabled.

## How to use this mod
* First install the mod, and reboot the game.
* Press Shift+F4 to toggle creative mode
* When in creative mode
  * Ctrl + T : Unlocks all techs
  * Ctrl + A : Resets all achievements
  * Numpad 1 : Places one stack of most items in the game to the inventory.
  * Numpad 2 : Increases Mecha mining, walking, replicating and reactor speed to a fixed high value. Increases research speed to a fixed high value
  * Numpad 3 : Covers the current planet in foundation entirely
  * Numpad 4 : Increases construction drone count by 1
  * Numpad 5 : Sets drone speed to a fixed high value
  * Numpad 6 : Immediately researches the current queued tech
  * Numpad 7 : Unlocks sail mode
  * Numpad 8 : Sets energy to 20 GJ. Also unlocks sail mode
  * Numpad 9 : Unlocks warp
  * Numpad 0 : Sets all station storage to 100000000
  * Numpad + : Adds more of the currently selected building to the inventory

  My additional functions
  * Ctrl + Numpad 1 : Toggles infinite inventory mode  
  (**WARNING**: Saving while in infinite inventory mode, will permanently change the player inventory for the particular save - meaning you won't be able to toggle back after relaunching DSP)
  * Ctrl + Numpad 0 : Toggles infinite station mode  
  (**WARNING**: This probably does not do what you think it does. Read the description carefully before trying it out)

### Infinite Inventory mode

* Makes the player inventory have all items present in the game
* All items are reset to 9999, with the stack size being 30000 every frame
* Because the stack is greater, items which go into the inventory are effectively "deleted"

### Infinite Station mode

* This mode is primarily for testing blueprints, by making stations infinitely output to belt and consume from belt without needing any drones/vessels
* Makes all stations set to supply have items less than half of max
* Makes all stations set to demand have items more than half of max
* In the case of ILS, only remote supply/demand is checked. If remote is set to storage, then local is taken

## Contact / Feedback / Bug Reports
You can either find me on the DSP Discord's #modding channel  
Or you can create an issue on [GitHub](https://github.com/Velociraptor115/DSPMods)  
\- Raptor#4825

## Changelog

### v0.0.3

* Added contact information

### v0.0.2

* Uploaded source code to GitHub  
* Added a couple of functions for testing blueprints and mods

### v0.0.1

* Initial version that just exposes already existing mode