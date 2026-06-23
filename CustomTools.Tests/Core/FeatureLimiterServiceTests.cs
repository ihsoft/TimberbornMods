using System.Collections.Immutable;
using IgorZ.CustomTools.Core;
using Timberborn.BlueprintSystem;
using Timberborn.GameFactionSystem;

namespace CustomTools.Tests;

static class FeatureLimiterServiceTests {
  public static void AllowsToolsWithoutLimiter() {
    var service = CreateService("Folktails");
    var spec = new CustomToolSpec();

    Assert.True(service.IsAllowed(spec));
  }

  public static void AllowsEmptyFactionFilters() {
    var service = CreateService("Folktails");
    var spec = CreateSpecWithLimiter([]);

    Assert.True(service.IsAllowed(spec));
  }

  public static void AllowsCurrentFaction() {
    var service = CreateService("Folktails");
    var spec = CreateSpecWithLimiter(["IronTeeth", "Folktails"]);

    Assert.True(service.IsAllowed(spec));
  }

  public static void RejectsDifferentFaction() {
    var service = CreateService("Folktails");
    var spec = CreateSpecWithLimiter(["IronTeeth"]);

    Assert.False(service.IsAllowed(spec));
  }

  static FeatureLimiterService CreateService(string factionId) {
    return new FeatureLimiterService(new FactionService(factionId));
  }

  static CustomToolSpec CreateSpecWithLimiter(string[] allowedFactions) {
    var spec = new CustomToolSpec();
    spec.Blueprint.AddSpec(new CustomToolFeatureLimiterSpec {
        AllowedFactions = allowedFactions.ToImmutableArray(),
    });
    return spec;
  }
}
