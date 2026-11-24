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

### Useful Upgrades Only

* Same as "Least Hashes Required - Tech Level Aware", but upgrade techs (mining, research speed, logistics, mecha, combat upgrades ...) are only queued when they are on the `Useful Upgrades` list. Main-tree techs are not limited, and the techs a listed upgrade still needs first are queued too.
* `Useful Upgrades`: comma separated tech names as the game shows them (any language) or tech ids. Add `:N` to stop after level N, e.g. `Research Speed:140`. The default list is the late-game white matrix set: Vein Utilization, Research Speed, Drone Engine, Logistics Carrier Engine, plus the finite logistics / stacking upgrades. It is written with the game's internal (Chinese) tech names, which match in every language.
* Useful when the queue runs dry overnight: without it the cheapest upgrade is queued, which in late game keeps pulling every infinite upgrade (Ray Transmission Efficiency, Mecha Core, Drive Engine ...) to the same cost.

### Least Hashes Required

* Queues the tech which can be researched with the least amount of hashes

### Last Researched Tech

* Repeats the last researched tech if it is still not fully unlocked


## Changelog

### [v0.0.6](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.6/)

* Ignore obsolete tech in LeastHashesRequired mode as well

### [v0.0.5](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.5/)

* Ignore obsolete tech while queueing. Fixes [pile sorter issues](https://github.com/Velociraptor115-DSPModding/DSPMods/issues/26)

### [v0.0.4](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.4/)

* Fix least hashes logic

### [v0.0.3](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.3/)

* Add queue mode "Least Hashes Required - Tech Level Aware", courtesy of [Jiesi Luo](https://github.com/luojiesi)

### [v0.0.2](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.2/)

* Fix a null reference exception that occurs on starting a new game

### [v0.0.1](https://dsp.thunderstore.io/package/Raptor/AutoQueueTech/0.0.1/)

* Add queue mode "Last Researched Tech"
* Add queue mode "Least Hashes Required"