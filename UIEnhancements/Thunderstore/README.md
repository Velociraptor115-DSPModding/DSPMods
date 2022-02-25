# UIEnhancements Mod
This mod aims to enhance DSP's UI for ease-of-use

## How to use this mod
* First install the mod, and reboot the game.

## What's new in this update
* Changed the names of some of the config items. If you are updating from a previous version and want to keep your config file clean without obsolete options, you might want to delete the config file in the mod manager.  
* Add option to hide the real-time clock
* Add option to hide the game-time clock
* Add option to move windows partially off-screen

## Enhancements

* Each enhancement can be enabled / disabled individually. When they are disabled, they do not modify the game in any way.  
* **All enhancements are disabled by default**. You will have to enable the ones you want after installing the mod and starting the game for the first time. That generates the config file for the mod. Then, you can configure it using the mod manager's config settings like  
![Mod Config Page](https://github.com/Velociraptor115/DSPMods/blob/main/UIEnhancements/Docs/BepInConfig.png?raw=true)  

### Editable Station Storage Max

* Allows to edit the "max" field of the stations' storage  
![Editable Station Storage Max](https://github.com/Velociraptor115/DSPMods/blob/main/UIEnhancements/Docs/EditableStationStorageMax.png?raw=true)  

### Unrestricted UI Scaler

* Replaces the "UI layout reference height" option with a much more flexible variant  
* This is the settings page without the mod
![Unrestricted UI Scaler Part 1](https://github.com/Velociraptor115/DSPMods/blob/main/UIEnhancements/Docs/UnrestrictedUIScaler_P1.png?raw=true)  
* With the mod, this now becomes  
![Unrestricted UI Scaler Part 2](https://github.com/Velociraptor115/DSPMods/blob/main/UIEnhancements/Docs/UnrestrictedUIScaler_P2.png?raw=true)  
* If you click on "Enable Live Preview", you will get a top-level slider that will show you the scaling preview  
![Unrestricted UI Scaler Part 3](https://github.com/Velociraptor115/DSPMods/blob/main/UIEnhancements/Docs/UnrestrictedUIScaler_P3.png?raw=true)  
* You can exit the settings screen and try out the UI scaling you want in-game. Then you can click Apply or Cancel to either keep the change or revert it and dismiss the live preview   
![Unrestricted UI Scaler Part 4](https://github.com/Velociraptor115/DSPMods/blob/main/UIEnhancements/Docs/UnrestrictedUIScaler_P4.png?raw=true)  

### Partial Off-screen Windows

* Allows the in-game windows to be moved partially off-screen. Originally, it jumps back into the display area even if any one corner is slightly outside the display. Now, the restriction only applies to the most extreme corner of the window.  
* The threshold width and height that needs to be kept inside the display area at all times is configurable, but it is recommended not to set it to a very low value or negative value, to avoid placing your windows outside the display area and not being able to drag it back in. (However, even if you do that, the window positions will be reset when you restart the game)  

### Hide Real Time Display

* Hides the real-time clock in the bottom right

### Hide Game Time Display

* Hides the game-time clock in the bottom right

## Contact / Feedback / Bug Reports
You can either find me on the DSP Discord's #modding channel  
Or you can create an issue on [GitHub](https://github.com/Velociraptor115/DSPMods)  
\- Raptor#4825

## Changelog

### [v0.0.2](https://dsp.thunderstore.io/package/Raptor/UIEnhancements/0.0.2/)

* Add "Partial Off-screen Windows"
* Add "Hide Real Time Display"
* Add "Hide Game Time Display"
* Fix issue with the station's input field capturing the UI selection even after submitting or escaping

### [v0.0.1](https://dsp.thunderstore.io/package/Raptor/UIEnhancements/0.0.1/)

* Add unrestricted UI scaler
* Make station storage max editable