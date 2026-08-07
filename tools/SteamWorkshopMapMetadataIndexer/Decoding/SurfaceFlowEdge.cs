// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

readonly record struct SurfaceFlowEdge(int SourceCell, int TargetCell, float Flow);
