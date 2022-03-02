# AutoQueueTech Mod
This mod automatically queues the next tech based on the configured setting, when the tech queue becomes empty

## How to use this mod
* Install the mod, and reboot the game.
* (Optional) Modify configs using the mod manager or directly in the BepInEx\config\dev.raptor.dsp.AutoQueueTech.cfg file
* The mod automatically kicks in when your tech queue becomes empty after successfully researching a tech

## Queue Modes

### Last Researched Tech (Default)

* Repeats the last researched tech if it is still not fully unlocked

### Least Hashes Required

* Queues the tech which can be researched with the least amount of hashes


## Changelog

### [v0.0.1](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.1/)

* Add queue mode "Last Researched Tech"
* Add queue mode "Least Hashes Required"