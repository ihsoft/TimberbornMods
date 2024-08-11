// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using TimberApi.UIBuilderSystem;
using TimberApi.UIBuilderSystem.ElementBuilders;
using TimberApi.UIBuilderSystem.StyleSheetSystem;
using TimberApi.UIBuilderSystem.StylingElements;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI {

/// <summary>Fragment for the UI panel.</summary>
public class PanelFragment : PanelFragmentBuilder<PanelFragment> {
  /// <inheritdoc />
  protected override PanelFragment BuilderInstance => this;
}

/// <summary>Builder class for a panel fragment.</summary>
/// <typeparam name="TBuilder">the type to build.</typeparam>
public abstract class PanelFragmentBuilder<TBuilder> : BaseBuilder<TBuilder, NineSliceVisualElement>
    where TBuilder : BaseBuilder<TBuilder, NineSliceVisualElement> {
  const string BackgroundClass = nameof(PanelFragment);

  VisualElementBuilder _visualElementBuilder;

  /// <inheritdoc />
  protected override NineSliceVisualElement InitializeRoot() {
    _visualElementBuilder = UIBuilder.Create<VisualElementBuilder>();
    _visualElementBuilder.AddClass(BackgroundClass);
    _visualElementBuilder.SetPadding(new Padding(new Length(12f, LengthUnit.Pixel), new Length(8f, LengthUnit.Pixel)));
    return _visualElementBuilder.Build();
  }

  /// <summary>Adds a component to the panel.</summary>
  public TBuilder AddComponent(VisualElement visualElement) {
    Root.Add(visualElement);
    return BuilderInstance;
  }

  /// <summary>Sets the flex direction of the panel.</summary>
  public TBuilder SetFlexDirection(FlexDirection direction) {
    Root.style.flexDirection = direction;
    return BuilderInstance;
  }

  /// <summary>Sets the height of the panel.</summary>
  public TBuilder SetWidth(Length width) {
    Root.style.width = width;
    return BuilderInstance;
  }

  /// <summary>Sets the height of the panel.</summary>
  public TBuilder SetJustifyContent(Justify justify) {
    Root.style.justifyContent = justify;
    return BuilderInstance;
  }

  /// <inheritdoc />
  protected override void InitializeStyleSheet(StyleSheetBuilder styleSheetBuilder) {
    styleSheetBuilder.AddNineSlicedBackgroundClass(BackgroundClass, "ui/images/backgrounds/bg-3", 9f, 0.5f);
  }
}

}
