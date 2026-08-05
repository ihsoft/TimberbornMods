# Timberborn Map Water Decoder

Research CLI for validating exact water-map decoding before a content-derived water classifier is added to the public
Workshop index. It reads a local `.timber` archive and writes:

- a JSON summary with exact open-water coverage, maximum surface depth, serialized water levels, and excluded
  underground water-column count;
- a BMP diagnostic map where green shows terrain and blue shows decoded open-water depth.
- a feature BMP where yellow marks irregular deep lake cores, orange marks conservative shallow-lake cores, white marks
  ambiguous broad shallows, cyan marks river candidates, dark blue marks lake shores, and magenta marks deeper water
  that does not yet satisfy the lake-core rule.

Run:

```powershell
dotnet run --project tools/TimberbornMapWaterDecoder/TimberbornMapWaterDecoder.csproj -- `
  MAP.timber OUTPUT_DIRECTORY
```

The decoder supports both legacy `TerrainMap.Heights` and current `TerrainMap.Voxels`. It interprets
`WaterMapNew.WaterColumns` using the game's level-major packed-list order. A water column is considered open surface
water only when its serialized floor equals the highest terrain surface at that horizontal cell. Non-empty columns
below that surface are counted for diagnostics and excluded.

Lake detection is independent of depth. Broad water is first split into local surface-level regions so that narrow
channels and waterfalls do not merge a cascade of ponds into one object. Each region is then evaluated using boundary
throughput relative to volume, saved-flow coherence, surface-height spread, aspect ratio, compactness, and the share of
its area that remains after deeper shore-distance erosion. The inner-core rule also permits large irregular lakes with
islands even when their outer contour has low compactness. Strongly sloped, elongated, or through-flowing regions are
river candidates; regions without enough evidence for either class remain explicitly ambiguous.

Depth is retained only as a diagnostic attribute and controls whether a detected lake core is drawn yellow or orange.
It is not evidence by itself that a water body is a lake. The tool is not part of the production workflow; its output
is intended for visual verification before these rules become classifier inputs.
