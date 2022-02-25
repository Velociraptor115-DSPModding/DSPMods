using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using DysonSphereProgram.Modding.UIEnhancements.UI.Builder;
using UnityEngine.UI;

namespace DysonSphereProgram.Modding.UIEnhancements;

public class EditableStationStorageMax: EnhancementBase
{
  public static List<Text> patchedTexts = new List<Text>();
  public static List<InputField> patchedInputFields = new List<InputField>();

  public static ConfigEntry<int> sliderStep;
  public static ConfigEntry<int> sliderStepPercent;

  [HarmonyPostfix]
  [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnCreate))]
  public static void PatchStationWindow(ref UIStationWindow __instance)
  {
    for (int i = 0; i < __instance.storageUIs.Length; i++)
    {
      ref var storageUI = ref __instance.storageUIs[i];
      var inputField = PatchMaxValue(ref storageUI);
      patchedTexts.Add(storageUI.maxValueText);
      patchedInputFields.Add(inputField);
    }
  }
  
  private static InputField PatchMaxValue(ref UIStationStorage __instance)
  {
    var component = __instance;
    var binding = new DelegateDataBindSource<int, string>(
      () =>
      {
        if (!component)
          return 0;
        if (component.station == null)
          return 0;
        if (component.index >= component.station.storage.Length)
          return 0;

        return component.station.storage[component.index].max;
      },
      newMax =>
      {
        if (!component)
          return;
        if (component.station == null)
          return;
        if (component.index >= component.station.storage.Length)
          return;
        
        if (newMax < 0)
          return;
        component.station.storage[component.index].max = newMax;
      },
      DataBindTransform.From<int, string>(x => x.ToString(), x => int.TryParse(x, out int res) ? res : -1)
    );
    
    TextBoxToInputField.CreateInputFieldFromText(__instance.maxValueText, out var inputFieldCtx, context =>
    {
      context
        .WithAnchor(Anchor.StretchRight)
        .OfSize(35, -6)
        .Bind(binding);
    });
    
    __instance.maxValueText.gameObject.SetActive(false);

    return inputFieldCtx.inputField;
  }
  
  [HarmonyPostfix]
  [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
  public static void PatchStationStorageRefreshValues(ref UIStationStorage __instance)
  {
    if (__instance.station == null)
      return;
    if (__instance.index >= __instance.station.storage.Length)
      return;

    var proto = LDB.items.Select(__instance.stationWindow.factory.entityPool[__instance.station.entityId].protoId);
    __instance.maxSlider.maxValue = proto.prefabDesc.stationMaxItemCount + __instance.GetAdditionStorage();
    __instance.maxSlider.value = __instance.station.storage[__instance.index].max;
  }
  
  [HarmonyPrefix]
  [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnMaxSliderValueChange))]
  public static void InterceptSliderValueChange(ref UIStationStorage __instance, ref float val)
  {
    if (__instance.station == null)
      return;
    if (__instance.index >= __instance.station.storage.Length)
      return;

    if ((int)val == __instance.station.storage[__instance.index].max) // This is being set from the input field 
      return;
    
    if ((int)val == (int)__instance.maxSlider.maxValue)
      return;

    var step = sliderStep.Value;
    if (sliderStepPercent.Value != 0)
      step = (int)__instance.maxSlider.maxValue * sliderStepPercent.Value / 100;

    val -= (int)val % step;
  }
  
  [HarmonyTranspiler]
  [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
  public static IEnumerable<CodeInstruction> PatchRefreshValues(IEnumerable<CodeInstruction> code, ILGenerator generator)
  {
    var matcher = new CodeMatcher(code, generator);

    matcher.SearchForward(ci => ci.Calls(AccessTools.PropertySetter(typeof(Slider), nameof(Slider.value))));
    var valueSetEndPos = matcher.Pos;
    matcher.MatchBack(false,
      new CodeMatch(ci => ci.IsLdarg(0)),
      new CodeMatch(ci => ci.LoadsField(AccessTools.Field(typeof(UIStationStorage), nameof(UIStationStorage.maxSlider))))
    );
    var valueSetStartPos = matcher.Pos;

    matcher.RemoveInstructionsInRange(valueSetStartPos, valueSetEndPos);

    matcher.Start();
    matcher.SearchForward(ci => ci.Calls(AccessTools.PropertySetter(typeof(Slider), nameof(Slider.maxValue))));
    var maxValueSetEndPos = matcher.Pos;
    matcher.MatchBack(false,
      new CodeMatch(ci => ci.IsLdarg(0)),
      new CodeMatch(ci => ci.LoadsField(AccessTools.Field(typeof(UIStationStorage), nameof(UIStationStorage.maxSlider))))
    );
    var maxValueSetStartPos = matcher.Pos;
    
    matcher.RemoveInstructionsInRange(maxValueSetStartPos, maxValueSetEndPos);
    

    return matcher.InstructionEnumeration();
  }
  
  [HarmonyTranspiler]
  [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnMaxSliderValueChange))]
  public static IEnumerable<CodeInstruction> PatchMaxSliderValueChange(IEnumerable<CodeInstruction> code, ILGenerator generator)
  {
    var matcher = new CodeMatcher(code, generator);

    matcher.MatchForward(false,
      new CodeMatch(OpCodes.Ldc_R4, 100f),
      new CodeMatch(OpCodes.Mul)
    );
    
    if (matcher.IsValid) 
      matcher.SetOperandAndAdvance(1f);

    return matcher.InstructionEnumeration();
  }

  public static void ApplyPatch(Harmony harmony)
  {
    harmony.PatchAll(typeof(EditableStationStorageMax));
    if (!(UIRoot.instance && UIRoot.instance.uiGame && UIRoot.instance.uiGame.created))
      return;
    
    var window = UIRoot.instance.uiGame.stationWindow;
    PatchStationWindow(ref window);
  }

  private static void RemovePatch()
  {
    foreach (var text in patchedTexts)
      text.gameObject.SetActive(true);
    foreach (var inputField in patchedInputFields)
      Object.Destroy(inputField.gameObject);
    
    patchedTexts.Clear();
    patchedInputFields.Clear();
  }
  protected override void UseConfig(ConfigFile configFile)
  {
    sliderStep = configFile.Bind(ConfigSection, "Slider Step", 100);
    sliderStepPercent =
      configFile.Bind(ConfigSection, "Slider Step %", 0, new ConfigDescription(
        "If this is non-zero, the slider step will be a percentage of the maximum"
        , new AcceptableValueRange<int>(0, 100)
      ));
  }
  protected override void Patch(Harmony _harmony)
  {
    ApplyPatch(_harmony);
  }
  protected override void Unpatch()
  {
    RemovePatch();
  }
  protected override void CreateUI()
  {
    
  }
  protected override void DestroyUI()
  {
    
  }
  protected override string Name => "Editable Station Storage Max";
}