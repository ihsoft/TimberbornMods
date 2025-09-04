// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Provides methods to extend weather-related functionality.</summary>
/// <remarks>
/// The other mods can call this interface via reflections. It is guaranteed that it will not have mod-specific types in
/// the method signatures.
/// </remarks>
public interface IWeatherExtension : IAutomationExtension {
  /// <summary>Adds a new weather ID with a localization key for its name.</summary>
  /// <remarks>Duplicate IDs will be skipped.</remarks>
  public void AddWeatherId(string weatherId, string nameLocKey);

  /// <summary>Adds a provider function that returns the current weather ID for temperate weather.</summary>
  /// <remarks>
  /// Multiple providers can be added; the checking order is not determined. If the provider can't give a relevant ID,
  /// it should return NULl. The first non-null ID returned by any provider will be used as the current temperate
  /// weather ID. If none of the providers return a non-null ID, the temperate weather ID will be considered
  /// "TemperateWeather".
  /// </remarks>
  public void AddTemperateWeatherIdProvider(Func<string> getCurrentWeatherIdFunc);

  /// <summary>Triggers a check of the current season.</summary>
  /// <remarks>
  /// This method can be called multiple times per a tick. The actual check for the season will be done at the end of
  /// the frame, and the signal will only be triggered if the new value is different from the recorded last time.
  /// </remarks>
  /// <seealso cref="AddTemperateWeatherIdProvider"/>
  public void TriggerSeasonCheck();
}
