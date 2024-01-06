# AutoQueueTech Mod
This mod automatically queues the next tech based on the configured setting, when the tech queue becomes empty

## How to use this mod
* Install the mod, and reboot the game.
* (Optional) Modify configs using the mod manager or directly in the BepInEx\config\dev.raptor.dsp.AutoQueueTech.cfg file
* The mod automatically kicks in when your tech queue becomes empty after successfully researching a tech

## Queue Modes

### Least Hashes Required - Tech Level Aware (Default)

* Queues the tech which can be researched with the lowest tier of matrices, with the least amount of hashes
(Contributed by [Jiesi Luo](https://github.com/luojiesi))

### Least Hashes Required

* Queues the tech which can be researched with the least amount of hashes

### Last Researched Tech

* Repeats the last researched tech if it is still not fully unlocked


## Changelog

### [v0.0.3](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.3/)

* Add queue mode "Least Hashes Required - Tech Level Aware", courtesy of [Jiesi Luo](https://github.com/luojiesi)

### [v0.0.2](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.2/)

* Fix a null reference exception that occurs on starting a new game

### [v0.0.1](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.1/)

* Add queue mode "Last Researched Tech"
* Add queue mode "Least Hashes Required"