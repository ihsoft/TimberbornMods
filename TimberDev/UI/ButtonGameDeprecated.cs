// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using TimberApi.UIBuilderSystem;
using TimberApi.UIBuilderSystem.CustomElements;
using TimberApi.UIBuilderSystem.ElementBuilders;
using TimberApi.UIBuilderSystem.StyleSheetSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberDev.UI;

/// <summary>This is a temporary solution until TAPI is updated from 0.7.6.0.</summary>
public abstract class ButtonGameDeprecated<TBuilder> : BaseBuilder<TBuilder, LocalizableButton> where TBuilder : BaseBuilder<TBuilder, LocalizableButton>
{
    protected LocalizableButtonBuilder ButtonBuilder = null;

    protected string ImageClass = "api__button__game-button--normal";

    public TBuilder District()
    {
        ImageClass = "api__button__game-button--district";
        return BuilderInstance;
    }

    public TBuilder Highlight()
    {
        ImageClass = "api__button__game-button--highlight";
        return BuilderInstance;
    }

    public TBuilder Destructive()
    {
        ImageClass = "api__button__game-button--destructive";
        return BuilderInstance;
    }

    public TBuilder Active()
    {
        ButtonBuilder.AddClass("api__button__game-button--active");
        return BuilderInstance;
    }

    public TBuilder SetLocKey(string locKey)
    {
        ButtonBuilder.SetLocKey(locKey);
        return BuilderInstance;
    }

    public TBuilder SetWidth(Length width)
    {
        ButtonBuilder.SetWidth(width);
        return BuilderInstance;
    }

    public TBuilder SetHeight(Length height)
    {
        ButtonBuilder.SetHeight(height);
        return BuilderInstance;
    }

    public TBuilder SetFontSize(Length size)
    {
        ButtonBuilder.SetFontSize(size);
        return BuilderInstance;
    }

    public TBuilder SetFontStyle(FontStyle style)
    {
        //IL_0007: Unknown result type (might be due to invalid IL or missing references)
        ButtonBuilder.SetFontStyle(style);
        return BuilderInstance;
    }

    public TBuilder SetColor(StyleColor color)
    {
        ButtonBuilder.SetColor(color);
        return BuilderInstance;
    }

    protected override LocalizableButton InitializeRoot()
    {
        ButtonBuilder = UIBuilder.Create<LocalizableButtonBuilder>();
        return ButtonBuilder.AddClass("api__button__game-button").Build();
    }

    protected override void InitializeStyleSheet(StyleSheetBuilder styleSheetBuilder)
    {
        styleSheetBuilder.AddClass("api__button__game-button", delegate(PropertyBuilder builder)
        {
            builder.Add(Property.ClickSound, "UI.Click", StyleValueType.String).Add(Property.Color, "white", StyleValueType.Enum);
        }).AddNineSlicedBackgroundClass("api__button__game-button", "ui/images/buttons/button-game-hover", 24f, 0.5f, default(PseudoClass)).AddNineSlicedBackgroundClass("api__button__game-button--normal", "ui/images/buttons/button-game", 24f, 0.5f)
            .AddNineSlicedBackgroundClass("api__button__game-button--highlight", "ui/images/buttons/button-game-highlight", 24f, 0.5f)
            .AddNineSlicedBackgroundClass("api__button__game-button--district", "ui/images/buttons/button-game-district", 24f, 0.5f)
            .AddNineSlicedBackgroundClass("api__button__game-button--destructive", "ui/images/buttons/button-game-disabled", 24f, 0.5f)
            .AddNineSlicedBackgroundClass("api__button__game-button--active", "ui/images/buttons/button-game-active", 24f, 0.5f, PseudoClass.Hover, PseudoClass.Active);
    }

    public TBuilder ModifyRoot(Action<LocalizableButtonBuilder> buttonBuilder)
    {
        buttonBuilder(ButtonBuilder);
        return BuilderInstance;
    }

    public override LocalizableButton Build()
    {
        return ButtonBuilder.AddClass(ImageClass).Build();
    }
}
