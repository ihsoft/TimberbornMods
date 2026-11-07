using System;
using Bindito.Core;
using IgorZ.Automation.Api;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;
using Timberborn.BaseComponentSystem;
using UnityEngine;

namespace Automation.Tests;

static class ExternalApiTests {
  /// <summary>Value and optional-consumer regressions; scheduling and UI still need game validation.</summary>
  public static readonly (string Name, Action Test)[] Cases = [
      ("External API maps booleans to numeric state choices", BooleanValues),
      ("External API preserves declared numeric types and fixed-point precision", NumericTypesAndPrecision),
      ("External API rejects unrepresentable numbers and fractional integers", NumericErrors),
      ("External API validates string choices in both directions", StringChoices),
      ("External API distinguishes absent, zero and one-sided editor bounds", EditorBounds),
      ("External API percentages use fractions and editor-only bounds", Percentages),
      ("External API works without Automation and logs once per helper", OptionalEndpoint),
      ("External API rejects malformed declarations even without Automation", InvalidDeclarations),
  ];

  static void BooleanValues() {
    var value = CreateValue(typeof(bool));

    Assert.Equal(100, value.ToScript(true).AsRawNumber);
    Assert.Equal(0, value.ToScript(false).AsRawNumber);
    Assert.Equal(ValueDef.NumericFormatEnum.Boolean, value.Definition.DisplayNumericFormat);
    Assert.Equal("100", value.Definition.Options[0].Value);
    Assert.Equal("Automation.State.On", value.Definition.Options[0].Text);
    Assert.Equal("0", value.Definition.Options[1].Value);
    Assert.Equal("Automation.State.Off", value.Definition.Options[1].Text);
    Assert.Equal(false, (bool)value.FromScript(ScriptValue.Of(0)));
    Assert.Equal(true, (bool)value.FromScript(ScriptValue.Of(-1)));
    Assert.Throws<ScriptError.BadValue>(() => value.ToScript(1));
  }

  static void NumericTypesAndPrecision() {
    var integer = CreateValue(typeof(int));
    var single = CreateValue(typeof(float));
    var real = CreateValue(typeof(double));

    Assert.Equal(-1200, integer.ToScript(-12).AsRawNumber);
    Assert.Equal(-12, (int)integer.FromScript(ScriptValue.Of(-1200)));
    Assert.Equal(113, single.ToScript(1.126f).AsRawNumber);
    Assert.Equal(1.13f, (float)single.FromScript(ScriptValue.Of(113)));
    Assert.Equal(112, real.ToScript(1.125d).AsRawNumber);
    Assert.Equal(138, real.ToScript(1.375d).AsRawNumber);
    Assert.Equal(0.29d, (double)real.FromScript(ScriptValue.Of(29)));
    Assert.Equal(12345678.91d, (double)real.FromScript(ScriptValue.Of(1234567891)));
    Assert.Equal(int.MinValue, real.ToScript(-21474836.48d).AsRawNumber);
    Assert.Equal(int.MaxValue, real.ToScript(21474836.47d).AsRawNumber);
    Assert.Throws<ScriptError.BadValue>(() => real.ToScript(1f));
    Assert.Throws<ScriptError.BadValue>(() => real.ToScript(null));
  }

  static void NumericErrors() {
    var real = CreateValue(typeof(double));
    double[] invalid = [double.NaN, double.NegativeInfinity, double.PositiveInfinity, -21474836.49d, 21474836.48d];
    foreach (var number in invalid) {
      Assert.Throws<ScriptError.ValueOutOfRange>(() => real.ToScript(number));
    }
    var integer = CreateValue(typeof(int));
    Assert.Equal(2147483600, integer.ToScript(21474836).AsRawNumber);
    Assert.Throws<ScriptError.ValueOutOfRange>(() => integer.ToScript(21474837));
    Assert.Throws<ScriptError.ValueOutOfRange>(() => integer.ToScript(-21474837));
    Assert.Throws<ScriptError.BadValue>(() => integer.FromScript(ScriptValue.Of(101)));
    Assert.Throws<ScriptError.BadValue>(() => integer.Definition.RuntimeValueValidator(ScriptValue.Of(-101)));
  }

  static void StringChoices() {
    var choice = new ExternalValueDefinition(typeof(string), new FakeLoc(), ["dry", "wet"], ["DryKey", "WetKey"]);

    Assert.Equal("dry", choice.ToScript("dry").AsString);
    Assert.Equal("wet", (string)choice.FromScript(ScriptValue.FromString("wet")));
    Assert.Equal("WetKey", choice.Definition.Options[1].Text);
    Assert.Throws<ScriptError.BadValue>(() => choice.ToScript("Wet"));
    Assert.Throws<ScriptError.BadValue>(() => choice.FromScript(ScriptValue.FromString("unknown")));
    Assert.Throws<ScriptError.BadValue>(() => choice.ToScript(null));
    var freeText = CreateValue(typeof(string));
    Assert.Equal("", freeText.ToScript("").AsString);
    Assert.Equal("custom:value", (string)freeText.FromScript(ScriptValue.FromString("custom:value")));
  }

  static void EditorBounds() {
    var unbounded = CreateValue(typeof(float));
    var zero = CreateValue(typeof(float), min: 0, max: 0);
    var lower = CreateValue(typeof(float), min: 0);
    var upper = CreateValue(typeof(float), max: 10);

    Assert.False(unbounded.Definition.DisplayNumericFormatRange.HasValue);
    Assert.False(new ValueDef().DisplayNumericFormatRange.HasValue);
    Assert.Equal((0f, 0f), zero.Definition.DisplayNumericFormatRange.Value);
    Assert.Equal(0f, lower.Definition.DisplayNumericFormatRange.Value.min);
    Assert.True(float.IsNaN(lower.Definition.DisplayNumericFormatRange.Value.max));
    Assert.True(float.IsNaN(upper.Definition.DisplayNumericFormatRange.Value.min));
    Assert.Equal(10f, upper.Definition.DisplayNumericFormatRange.Value.max);
    Assert.Equal(500, zero.ToScript(5f).AsRawNumber);
    Assert.Equal(-5f, (float)zero.FromScript(ScriptValue.Of(-500)));
    zero.Definition.RuntimeValueValidator(ScriptValue.Of(500));
  }

  static void Percentages() {
    Type[] types = [typeof(float), typeof(double)];
    foreach (var type in types) {
      var value = CreateValue(type, min: 0, max: 1, percent: true);
      object fraction = type == typeof(float) ? (object)0.75f : 0.75d;

      Assert.Equal(75, value.ToScript(fraction).AsRawNumber);
      Assert.Equal(type, value.FromScript(ScriptValue.Of(75)).GetType());
      Assert.Equal(0.75d, Convert.ToDouble(value.FromScript(ScriptValue.Of(75))));
      Assert.Equal(ValueDef.NumericFormatEnum.Percent, value.Definition.DisplayNumericFormat);
      Assert.Equal((0f, 100f), value.Definition.DisplayNumericFormatRange.Value);
      Assert.Equal(21d, Convert.ToDouble(value.FromScript(ScriptValue.Of(2100))));
      value.Definition.RuntimeValueValidator(ScriptValue.Of(2100));
    }
    Assert.False(CreateValue(typeof(double), percent: true).Definition.DisplayNumericFormatRange.HasValue);
  }

  static void OptionalEndpoint() {
    // This test assembly deliberately contains no AutomationApiEndpoint type.
    Debug.Messages.Clear();
    var api = new AutomationApi(new TestContainer(), "ExampleMod", "ExampleMod.Name");
    Func<bool> read = () => throw new InvalidOperationException("Unexpected synchronous getter.");
    Action execute = () => throw new InvalidOperationException("Unexpected action execution.");
    api.AddSignal("Ready", "ReadyKey", read);
    api.AddAction("Reset", "ResetKey", execute);
    api.AddSignal("Ready", "DuplicateKey", read);
    api.ForBuildings<BaseComponent>().AddSignal("BuildingReady", "ReadyKey", _ => read());
    for (var i = 0; i < 10; i++) {
      api.UpdateSignal("Ready");
      api.UpdateSignal("BuildingReady", new BaseComponent());
    }
    Assert.Equal(1, Debug.Messages.Count);
    Assert.Equal("[Automation API] Mod 'ExampleMod' supports Automation.ConnectV1, but the endpoint was not found.",
        Debug.Messages[0]);

    var nextGame = new AutomationApi(new TestContainer(), "ExampleMod", "ExampleMod.Name");
    nextGame.UpdateSignal("Ready");
    Assert.Equal(2, Debug.Messages.Count);
  }

  static void InvalidDeclarations() {
    string[] invalidIds = ["", "1Mod", "My.Mod", "My_Mod", "Mod:Id", "Möd", "Mod\n"];
    foreach (var id in invalidIds) {
      Assert.Throws<ArgumentException>(() => new AutomationApi(new TestContainer(), id, "NameKey"));
    }
    var api = new AutomationApi(new TestContainer(), "ExampleMod", "NameKey");
    Assert.Throws<ArgumentException>(() => api.AddSignal("Bad.Id", "NameKey", () => 1));
    Assert.Throws<ArgumentNullException>(() => api.AddSignal("NullRead", "NameKey", (Func<bool>)null));
    Assert.Throws<ArgumentNullException>(() => api.AddAction("NullAction", "NameKey", (Action)null));
    Assert.Throws<ArgumentException>(() => api.AddSignal("Bounds", "NameKey", () => 1d, min: 2, max: 1));
    Assert.Throws<ArgumentException>(() => api.AddPercentAction("Bounds", "NameKey", (float _) => {}, max: float.NaN));
    Assert.Throws<ArgumentException>(() => api.AddAction("Choices", "NameKey", (string _) => {}, options: []));
    Assert.Throws<ArgumentException>(() => api.AddSignal("Choices", "NameKey", () => "x",
        options: [("x", "FirstKey"), ("x", "SecondKey")]));
  }

  static ExternalValueDefinition CreateValue(Type type, double? min = null, double? max = null, bool percent = false) {
    return new ExternalValueDefinition(type, new FakeLoc(), null, null, min, max, percent);
  }
}
