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

using static UIBuilderDSL;

public static class TextBoxToInputField
{
  public static void CreateInputFieldFromText(Text text, System.Action<InputFieldContext> initializer = null) =>
    CreateInputFieldFromText(text, out _, initializer);
  public static void CreateInputFieldFromText(Text text, out InputFieldContext inputFieldContext, System.Action<InputFieldContext> initializer = null)
  {
    var parent = text.transform.parent;
    var rectTransform = text.GetComponent<RectTransform>();

    var inputHighlightColorBlock = ColorBlock.defaultColorBlock with
    {
      normalColor = Color.clear,
      highlightedColor = Color.white.AlphaMultiplied(0.1f),
      pressedColor = Color.clear,
      disabledColor = Color.clear
    };

    inputFieldContext =
      Create.InputField("input-field")
        .ChildOf(parent)
        .WithPivot(rectTransform.pivot)
        .WithMinMaxAnchor(rectTransform.anchorMin, rectTransform.anchorMax)
        .At(rectTransform.anchoredPosition)
        .OfSize(rectTransform.sizeDelta)
        .WithVisuals((IProperties<Image>)new ImageProperties())
        .WithTransition(colors: inputHighlightColorBlock)
        ;

    Select.Text(inputFieldContext.text)
      .WithMaterial(text.material)
      .WithColor(text.color)
      .WithFont(text.font)
      .WithFontSize(
        text.fontSize
        , text.resizeTextForBestFit ? text.resizeTextMinSize : null
        , text.resizeTextForBestFit ? text.resizeTextMaxSize : null
      )
      .WithAlignment(text.alignment)
      .WithOverflow(text.horizontalOverflow, text.verticalOverflow)
      ;

    if (initializer != null)
      initializer(inputFieldContext);
  }
}