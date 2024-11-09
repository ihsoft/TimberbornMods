using System;
using TimberApi.UIBuilderSystem;
using TimberApi.UIBuilderSystem.ElementBuilders;
using TimberApi.UIBuilderSystem.StyleSheetSystem;
using TimberApi.UIBuilderSystem.StyleSheetSystem.Extensions;
using TimberApi.UIBuilderSystem.StyleSheetSystem.PropertyEnums;
using UnityEngine;

namespace IgorZ.TimberDev.UI;

// TimberAPI preset for the fixed MinMaxSlider2 control.

public class MinMaxSliderBuilder2 : MinMaxSliderBuilder2<MinMaxSliderBuilder2, MinMaxSlider2>
{
    protected override MinMaxSliderBuilder2 BuilderInstance => this;

    public MinMaxSliderBuilder2 SetLabel(string text)
    {
        Root.label = text;
        return BuilderInstance;
    }
}

public abstract class MinMaxSliderBuilder2<TBuilder, TElement> : BaseElementBuilder<TBuilder, TElement>
        where TBuilder : BaseElementBuilder<TBuilder, TElement>
        where TElement : MinMaxSlider2, new()
{
    public TBuilder SetLowLimit(float value)
    {
        Root.lowLimit = value;
        return BuilderInstance;
    }

    public TBuilder SetHighLimit(float value)
    {
        Root.highLimit = value;
        return BuilderInstance;
    }

    public TBuilder SetValue(Vector2 value)
    {
        Root.value = value;
        return BuilderInstance;
    }

    protected override TElement InitializeRoot()
    {
        return new TElement();
    }
}

public class GameTextMinMaxSlider2 : GameTextMinMaxSliderBuilder2<GameTextMinMaxSlider2>
{
    protected override GameTextMinMaxSlider2 BuilderInstance => this;
}

public abstract class GameTextMinMaxSliderBuilder2<TBuilder> : BaseBuilder<TBuilder, MinMaxSlider2>
    where TBuilder : BaseBuilder<TBuilder, MinMaxSlider2>
{
    protected MinMaxSliderBuilder2 MinMaxSliderBuilder = null!;

    protected string SizeClass = "api__min-max-slider--normal";

    protected string DraggerClass = "api__min-max-slider--circle";

    public TBuilder SetLabel(string text)
    {
        MinMaxSliderBuilder.SetLabel(text);
        return BuilderInstance;
    }

    public TBuilder SetLowLimit(float value)
    {
        MinMaxSliderBuilder.SetLowLimit(value);
        return BuilderInstance;
    }

    public TBuilder SetHighLimit(float value)
    {
        MinMaxSliderBuilder.SetHighLimit(value);
        return BuilderInstance;
    }

    public TBuilder SetValue(Vector2 value)
    {
        MinMaxSliderBuilder.SetValue(value);
        return BuilderInstance;
    }
    
    public TBuilder Small()
    {
        SizeClass = "api__min-max-slider--small";

        return BuilderInstance;
    }
    
    public TBuilder Diamond()
    {
        DraggerClass = "api__min-max-slider--diamond";

        return BuilderInstance;
    }

    protected override MinMaxSlider2 InitializeRoot()
    {
        MinMaxSliderBuilder = UIBuilder.Create<MinMaxSliderBuilder2>()
            .AddClass("api__min-max-slider");

        return MinMaxSliderBuilder.Build();
    }

    protected override void InitializeStyleSheet(StyleSheetBuilder styleSheetBuilder)
    {
        styleSheetBuilder
            .AddClass("api__min-max-slider", builder => builder.Add(Property.FlexGrow, 1))
            .AddSelector(".api__min-max-slider > .unity-min-max-slider__label", builder => builder
                .Color(new Color(0.8f, 0.8f, 0.8f))
                .FontSize(13)
                .PaddingRight(13)
                .UnityTextAlign(UnityTextAlign.MiddleRight)
                .MinWidth(0)
            )
            .AddMultiSelector(new[]
                {
                    ".api__min-max-slider--circle > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__min-thumb",
                    ".api__min-max-slider--circle > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__max-thumb"
                },
                builder => builder
                    .BackgroundImage("UI/Images/Buttons/circle-off")
            )
            .AddMultiSelector(new[]
                {
                    ".api__min-max-slider--circle > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__min-thumb:hover:enabled",
                    ".api__min-max-slider--circle > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__max-thumb:hover:enabled",
                },
                builder => builder
                    .BackgroundImage("UI/Images/Buttons/circle-hover")
            )
            .AddMultiSelector(new[]
                {
                    ".api__min-max-slider--circle > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__min-thumb:hover:enabled:active",
                    ".api__min-max-slider--circle > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__max-thumb:hover:enabled:active",
                },
                builder => builder
                    .BackgroundImage("UI/Images/Buttons/circle-on")
            )
            .AddMultiSelector(new[]
                {
                    ".api__min-max-slider--diamond > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__min-thumb",
                    ".api__min-max-slider--diamond > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__max-thumb",
                },
                builder => builder
                    .BackgroundImage("UI/Images/Buttons/slider_holder")
            )
            .AddMultiSelector(new[]
                {
                    ".api__min-max-slider--diamond > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__min-thumb:hover:enabled",
                    ".api__min-max-slider--diamond > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__max-thumb:hover:enabled",
                },
                builder => builder
                    .BackgroundImage("UI/Images/Buttons/slider_holder_hover")
            )
            .AddSelector(".api__min-max-slider--normal > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__min-thumb", builder => builder
                .Height(25)
                .MinWidth(25)
                .MarginTop(-10)
                //.MarginLeft(-10)
            )
            .AddSelector(".api__min-max-slider--normal > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__max-thumb", builder => builder
                .Height(25)
                .MinWidth(25)
                .MarginTop(-10)
                //.MarginLeft(-5)
            )
            .AddSelector(".api__min-max-slider--small > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__min-thumb", builder => builder
                .MinHeight(16)
                .MinWidth(16)
                .MarginTop(-6)
                //.MarginLeft(-7)
            )
            .AddSelector(".api__min-max-slider--small > .unity-min-max-slider__input > .unity-min-max-slider__dragger > .unity-min-max-slider__max-thumb", builder => builder
                .MinHeight(16)
                .MinWidth(16)
                .MarginTop(-6)
                //.MarginLeft(-2)
            )
            .AddSelector(
                ".api__min-max-slider > .unity-min-max-slider__input > .unity-min-max-slider__dragger",
                builder => builder
                    .BackgroundImage("UI/Images/Backgrounds/bg-pixel-4")
                    .UnityBackgroundScaleMode(UnityBackgroundScaleMode.StretchToFill)
            )
            .AddSelector(
                ".api__min-max-slider > .unity-min-max-slider__input > .unity-min-max-slider__tracker",
                builder => builder
                    .BackgroundImage("UI/Images/Backgrounds/bg-pixel-2")
                    .UnityBackgroundScaleMode(UnityBackgroundScaleMode.StretchToFill)
            )
            .AddSelector(
                ".api__min-max-slider--normal > .unity-min-max-slider__input > .unity-min-max-slider__tracker",
                builder => builder
                    .Height(4)
            )
            .AddSelector(
                ".api__min-max-slider--small > .unity-min-max-slider__input > .unity-min-max-slider__tracker",
                builder => builder
                    .Height(2)
            );
    }

    public override MinMaxSlider2 Build()
    {
        return MinMaxSliderBuilder
            .AddClass(SizeClass)
            .AddClass(DraggerClass)
            .Build();
    }
    
    public TBuilder AddClass(string styleClass)
    {
        MinMaxSliderBuilder.AddClass(styleClass);
        return BuilderInstance;
    }

    public TBuilder ModifyRoot(Action<MinMaxSliderBuilder2> minMaxSliderBuilder)
    {
        minMaxSliderBuilder.Invoke(MinMaxSliderBuilder);
        return BuilderInstance;
    }
}