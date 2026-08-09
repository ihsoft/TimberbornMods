// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace IgorZ.MapBrowser.Core;

sealed class WorkshopItemMetadata {
  [JsonProperty("published_file_id")]
  public string PublishedFileId { get; set; }

  [JsonProperty("title")]
  public string Title { get; set; }

  [JsonProperty("description_plain")]
  public string DescriptionPlain { get; set; }

  [JsonProperty("description_raw")]
  public string DescriptionRaw { get; set; }

  [JsonProperty("creator_steam_id")]
  public string CreatorSteamId { get; set; }

  [JsonProperty("created_at_utc")]
  public DateTime CreatedAtUtc { get; set; }

  [JsonProperty("updated_at_utc")]
  public DateTime UpdatedAtUtc { get; set; }

  [JsonProperty("preview_url")]
  public string PreviewUrl { get; set; }

  [JsonProperty("tags")]
  public List<string> Tags { get; set; } = [];

  [JsonProperty("votes_up")]
  public uint VotesUp { get; set; }

  [JsonProperty("votes_down")]
  public uint VotesDown { get; set; }

  [JsonProperty("score")]
  public float Score { get; set; }

  [JsonProperty("primary_category")]
  public string PrimaryCategory { get; set; }

  [JsonProperty("categories")]
  public List<WorkshopCategoryMatch> Categories { get; set; } = [];

  [JsonProperty("map_width")]
  public int MapWidth { get; set; }

  [JsonProperty("map_height")]
  public int MapHeight { get; set; }

  [JsonProperty("map_analysis_version")]
  public int? MapAnalysisVersion { get; set; }

  [JsonProperty("map_metadata_collection_state")]
  public string MapMetadataCollectionState { get; set; }

  [JsonProperty("map_classifications")]
  public MapClassifications MapClassifications { get; set; }
}

sealed class MapClassifications {
  [JsonProperty("forest_density")]
  public ForestDensityClassification ForestDensity { get; set; }

  [JsonProperty("water")]
  public WaterClassification Water { get; set; }

  [JsonProperty("settlement_space")]
  public SettlementSpaceClassification SettlementSpace { get; set; }

  /// <summary>
  /// Projected dry-land areas of useful islands. An empty list means none were found; <c>null</c> means unknown.
  /// </summary>
  [JsonProperty("islands")]
  public List<int> Islands { get; set; }
}

sealed class ForestDensityClassification {
  [JsonProperty("level")]
  public int? Level { get; set; }
}

sealed class WaterClassification {
  [JsonProperty("open_water_ratio")]
  public double? OpenWaterRatio { get; set; }

  [JsonProperty("broad_boundary_water_ratio")]
  public double? BroadBoundaryWaterRatio { get; set; }

  [JsonProperty("largest_water_body_ratio")]
  public double? LargestWaterBodyRatio { get; set; }

  [JsonProperty("water_form")]
  public string WaterForm { get; set; }
}

sealed class SettlementSpaceClassification {
  [JsonProperty("space_type")]
  public string SpaceType { get; set; }
}

sealed class WorkshopCategoryMatch {
  [JsonProperty("category")]
  public string Category { get; set; }

  [JsonProperty("score")]
  public int Score { get; set; }

  [JsonProperty("evidence")]
  public List<string> Evidence { get; set; } = [];
}
