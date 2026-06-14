using System;
using System.Collections.Generic;
using System.IO;
using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class FeatureControllerTests {
  public static void ReadFeatures() {
    var dir = CreateTempDir();
    File.WriteAllLines(Path.Combine(dir, FeatureController.FeaturesFilename), [
        "# comment",
        "",
        "EnabledFeature",
        "!DisabledFeature",
        "Size=42",
        "!DisabledValue=unused",
    ]);

    var seen = new List<(string Name, bool Enabled, string Value)>();
    var found = FeatureController.ReadFeatures(dir, (name, enabled, value) => {
      seen.Add((name, enabled, value));
      return true;
    });

    Assert.True(found);
    Assert.Equal(4, seen.Count);
    Assert.Equal(("EnabledFeature", true, null), seen[0]);
    Assert.Equal(("DisabledFeature", false, null), seen[1]);
    Assert.Equal(("Size", true, "42"), seen[2]);
    Assert.Equal(("DisabledValue", false, "unused"), seen[3]);
  }

  public static void RejectsInvalidNames() {
    var dir = CreateTempDir();
    File.WriteAllLines(Path.Combine(dir, FeatureController.FeaturesFilename), [
        "Good.Name",
        "Bad-Name",
    ]);

    var seen = new List<string>();
    FeatureController.ReadFeatures(dir, (name, enabled, value) => {
      seen.Add(name);
      return true;
    });

    Assert.Equal(1, seen.Count);
    Assert.Equal("Good.Name", seen[0]);
  }

  public static void ValidatesHelpers() {
    var flag = false;
    Assert.True(FeatureController.SetFlag(ref flag, "Feature", true, null));
    Assert.True(flag);
    Assert.Throws<InvalidOperationException>(() => FeatureController.SetFlag(ref flag, "Feature", true, "bad"));

    var value = 0;
    Assert.True(FeatureController.SetValue(ref value, "Number", true, "123"));
    Assert.Equal(123, value);
    Assert.True(FeatureController.SetValue(ref value, "Number", false, "456"));
    Assert.Equal(123, value);
    Assert.Throws<InvalidOperationException>(() => FeatureController.SetValue(ref value, "Number", true, null));
    Assert.Throws<InvalidOperationException>(() => FeatureController.SetValue(ref value, "Number", true, "not-int"));
  }

  static string CreateTempDir() {
    var dir = Path.Combine(Path.GetTempPath(), "TimberDev.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }
}
