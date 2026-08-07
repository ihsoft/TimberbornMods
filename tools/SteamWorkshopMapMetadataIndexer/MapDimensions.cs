// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json.Serialization;

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

sealed record MapDimensions(
    [property: JsonPropertyName("Width")] int Width, [property: JsonPropertyName("Height")] int Height);
