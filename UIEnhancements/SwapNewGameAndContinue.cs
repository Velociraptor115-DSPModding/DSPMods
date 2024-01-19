using BepInEx.Configuration;
using HarmonyLib;

namespace DysonSphereProgram.Modding.UIEnhancements;

public class SwapNewGameAndContinue: EnhancementBase
{
    private float newGameOrigPosY;
    private float continueGameOrigPosY;
    
    protected override void UseConfig(ConfigFile configFile)
    {
        
    }

    protected override void Patch(Harmony _harmony)
    {
        
    }

    protected override void Unpatch()
    {
        
    }

    protected override void CreateUI()
    {
        var mainMenu = UIRoot.instance.uiMainMenu;
        newGameOrigPosY = mainMenu.newGameRtPosY;
        continueGameOrigPosY = mainMenu.continueRtPosY;
        mainMenu.newGameRtPosY = continueGameOrigPosY;
        mainMenu.continueRtPosY = newGameOrigPosY;
    }

    protected override void DestroyUI()
    {
        var mainMenu = UIRoot.instance.uiMainMenu;
        mainMenu.newGameRtPosY = newGameOrigPosY;
        mainMenu.continueRtPosY = continueGameOrigPosY;
    }

    protected override string Name => "Swap New Game and Continue";
}