using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

namespace Automation.Tests;

static class HookValueTests {
  /// <summary>Native editor definitions and conversions at configured CLR member boundaries.</summary>
  public static readonly (string Name, Action Test)[] Cases = [
      ("Hook values map booleans to native numeric state choices", BooleanValues),
      ("Hook values preserve CLR numeric types and fixed-point precision", NumericTypesAndPrecision),
      ("Hook values reject unrepresentable numbers and fractional integers", NumericErrors),
      ("Hook values validate string choices through the native definition", StringChoices),
      ("Hook values distinguish absent, zero and one-sided editor bounds", EditorBounds),
      ("Hook percentages use fractions and editor-only bounds", Percentages),
      ("Hook metadata rejects unsupported types and invalid choices before registration", InvalidMetadata),
  ];

  static void BooleanValues() {
    var definition = CreateValue(typeof(bool));

    Assert.Equal(100, HookValueConverter.ToScript(typeof(bool), true).AsRawNumber);
    Assert.Equal(0, HookValueConverter.ToScript(typeof(bool), false).AsRawNumber);
    Assert.Equal(ValueDef.NumericFormatEnum.Boolean, definition.DisplayNumericFormat);
    Assert.Equal("100", definition.Options[0].Value);
    Assert.Equal("Automation.State.On", definition.Options[0].Text);
    Assert.Equal("0", definition.Options[1].Value);
    Assert.Equal("Automation.State.Off", definition.Options[1].Text);
    Assert.Equal(false, (bool)HookValueConverter.FromScript(typeof(bool), ScriptValue.Of(0)));
    Assert.Equal(true, (bool)HookValueConverter.FromScript(typeof(bool), ScriptValue.Of(-1)));
    Assert.Throws<ScriptError.BadValue>(() => HookValueConverter.ToScript(typeof(bool), 1));
  }

  static void NumericTypesAndPrecision() {
    Assert.Equal(-1200, HookValueConverter.ToScript(typeof(int), -12).AsRawNumber);
    Assert.Equal(-12, (int)HookValueConverter.FromScript(typeof(int), ScriptValue.Of(-1200)));
    Assert.Equal(113, HookValueConverter.ToScript(typeof(float), 1.126f).AsRawNumber);
    Assert.Equal(1.13f, (float)HookValueConverter.FromScript(typeof(float), ScriptValue.Of(113)));
    Assert.Equal(112, HookValueConverter.ToScript(typeof(double), 1.125d).AsRawNumber);
    Assert.Equal(138, HookValueConverter.ToScript(typeof(double), 1.375d).AsRawNumber);
    Assert.Equal(0.29d, (double)HookValueConverter.FromScript(typeof(double), ScriptValue.Of(29)));
    Assert.Equal(12345678.91d, (double)HookValueConverter.FromScript(typeof(double), ScriptValue.Of(1234567891)));
    Assert.Equal(int.MinValue, HookValueConverter.ToScript(typeof(double), -21474836.48d).AsRawNumber);
    Assert.Equal(int.MaxValue, HookValueConverter.ToScript(typeof(double), 21474836.47d).AsRawNumber);
    Assert.Throws<ScriptError.BadValue>(() => HookValueConverter.ToScript(typeof(double), 1f));
    Assert.Throws<ScriptError.BadValue>(() => HookValueConverter.ToScript(typeof(double), null));
  }

  static void NumericErrors() {
    double[] invalid = [double.NaN, double.NegativeInfinity, double.PositiveInfinity, -21474836.49d, 21474836.48d];
    foreach (var number in invalid) {
      Assert.Throws<ScriptError.ValueOutOfRange>(() => HookValueConverter.ToScript(typeof(double), number));
    }
    Assert.Equal(2147483600, HookValueConverter.ToScript(typeof(int), 21474836).AsRawNumber);
    Assert.Throws<ScriptError.ValueOutOfRange>(() => HookValueConverter.ToScript(typeof(int), 21474837));
    Assert.Throws<ScriptError.ValueOutOfRange>(() => HookValueConverter.ToScript(typeof(int), -21474837));
    Assert.Throws<ScriptError.BadValue>(() => HookValueConverter.FromScript(typeof(int), ScriptValue.Of(101)));
    Assert.Throws<ScriptError.BadValue>(() => CreateValue(typeof(int)).RuntimeValueValidator(ScriptValue.Of(-101)));
  }

  static void StringChoices() {
    HookConfiguration.Option[] choices = [
        new HookConfiguration.Option { Value = "dry", NameLocKey = "DryKey" },
        new HookConfiguration.Option { Value = "wet", NameLocKey = "WetKey" },
    ];
    var definition = HookValueFactory.Create(typeof(string), new HookConfiguration.Value(), choices, new FakeLoc());

    Assert.Equal("dry", HookValueConverter.ToScript(typeof(string), "dry").AsString);
    Assert.Equal("wet", (string)HookValueConverter.FromScript(typeof(string), ScriptValue.FromString("wet")));
    Assert.Equal("WetKey", definition.Options[1].Text);
    definition.RuntimeValueValidator(ScriptValue.FromString("dry"));
    Assert.Throws<ScriptError.BadValue>(() => definition.RuntimeValueValidator(ScriptValue.FromString("Wet")));
    Assert.Throws<ScriptError.BadValue>(() => definition.RuntimeValueValidator(ScriptValue.FromString("unknown")));
    Assert.Throws<ScriptError.BadValue>(() => HookValueConverter.ToScript(typeof(string), null));
    Assert.Equal("", HookValueConverter.ToScript(typeof(string), "").AsString);
    CreateValue(typeof(string)).RuntimeValueValidator(ScriptValue.FromString("custom:value"));
  }

  static void EditorBounds() {
    var unbounded = CreateValue(typeof(float));
    var zero = CreateValue(typeof(float), min: 0, max: 0);
    var lower = CreateValue(typeof(float), min: 0);
    var upper = CreateValue(typeof(float), max: 10);

    Assert.False(unbounded.DisplayNumericFormatRange.HasValue);
    Assert.False(new ValueDef().DisplayNumericFormatRange.HasValue);
    Assert.Equal((0f, 0f), zero.DisplayNumericFormatRange.Value);
    Assert.Equal(0f, lower.DisplayNumericFormatRange.Value.min);
    Assert.True(float.IsNaN(lower.DisplayNumericFormatRange.Value.max));
    Assert.True(float.IsNaN(upper.DisplayNumericFormatRange.Value.min));
    Assert.Equal(10f, upper.DisplayNumericFormatRange.Value.max);
    Assert.Equal(500, HookValueConverter.ToScript(typeof(float), 5f).AsRawNumber);
    Assert.Equal(-5f, (float)HookValueConverter.FromScript(typeof(float), ScriptValue.Of(-500)));
    zero.RuntimeValueValidator(ScriptValue.Of(500));
  }

  static void Percentages() {
    Type[] types = [typeof(float), typeof(double)];
    foreach (var type in types) {
      var definition = CreateValue(type, min: 0, max: 1, percent: true);
      object fraction = type == typeof(float) ? (object)0.75f : 0.75d;

      Assert.Equal(75, HookValueConverter.ToScript(type, fraction).AsRawNumber);
      Assert.Equal(type, HookValueConverter.FromScript(type, ScriptValue.Of(75)).GetType());
      Assert.Equal(0.75d, Convert.ToDouble(HookValueConverter.FromScript(type, ScriptValue.Of(75))));
      Assert.Equal(ValueDef.NumericFormatEnum.Percent, definition.DisplayNumericFormat);
      Assert.Equal((0f, 100f), definition.DisplayNumericFormatRange.Value);
      Assert.Equal(21d, Convert.ToDouble(HookValueConverter.FromScript(type, ScriptValue.Of(2100))));
      definition.RuntimeValueValidator(ScriptValue.Of(2100));
    }
    Assert.False(CreateValue(typeof(double), percent: true).DisplayNumericFormatRange.HasValue);
  }

  static void InvalidMetadata() {
    Assert.Throws<ArgumentException>(() => CreateValue(typeof(DateTime)));
    Assert.Throws<ArgumentException>(() => CreateValue(typeof(int), percent: true));
    Assert.Throws<ArgumentException>(() => CreateValue(typeof(bool), min: 0));
    Assert.Throws<ArgumentException>(() => CreateValue(typeof(double), min: 2, max: 1));
    Assert.Throws<ArgumentException>(() => CreateValue(typeof(double), max: double.NaN));
    var choice = new HookConfiguration.Option { Value = "x", NameLocKey = "Name" };
    Assert.Throws<ArgumentException>(() => HookValueFactory.Validate(typeof(string), new(), []));
    Assert.Throws<ArgumentException>(() => HookValueFactory.Validate(typeof(string), new(), [choice, choice]));
    Assert.Throws<ArgumentException>(() => HookValueFactory.Validate(typeof(string), new(), [null]));
    Assert.Throws<ArgumentException>(() => HookValueFactory.Validate(typeof(int), new() { NameLocKey = " " }, null));
  }

  static ValueDef CreateValue(Type type, double? min = null, double? max = null, bool percent = false) {
    var metadata = new HookConfiguration.Value { Min = min, Max = max, Percent = percent };
    return HookValueFactory.Create(type, metadata, null, new FakeLoc());
  }
}
