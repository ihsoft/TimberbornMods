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

  [JsonProperty("gallery_urls")]
  public List<string> GalleryUrls { get; set; } = [];

  [JsonProperty("gallery_checked_at_utc")]
  public DateTime? GalleryCheckedAtUtc { get; set; }

  [JsonProperty("gallery_collection_state")]
  public string GalleryCollectionState { get; set; }

  [JsonProperty("visual_scores")]
  public Dictionary<string, float> VisualScores { get; set; } = [];

  [JsonProperty("visual_percentiles")]
  public Dictionary<string, float> VisualPercentiles { get; set; } = [];

  [JsonProperty("visual_levels")]
  public Dictionary<string, int> VisualLevels { get; set; } = [];

  [JsonProperty("visual_labels")]
  public List<string> VisualLabels { get; set; } = [];

  [JsonProperty("visual_image_count")]
  public int VisualImageCount { get; set; }

  [JsonProperty("visual_gallery_image_count")]
  public int VisualGalleryImageCount { get; set; }

  [JsonProperty("visual_missing_image_count")]
  public int VisualMissingImageCount { get; set; }

  [JsonProperty("visual_model")]
  public string VisualModel { get; set; }

  [JsonProperty("visual_classifier_version")]
  public string VisualClassifierVersion { get; set; }

  [JsonProperty("visual_stale")]
  public bool VisualStale { get; set; }
}

sealed class WorkshopCategoryMatch {
  [JsonProperty("category")]
  public string Category { get; set; }

  [JsonProperty("score")]
  public int Score { get; set; }

  [JsonProperty("evidence")]
  public List<string> Evidence { get; set; } = [];
}
