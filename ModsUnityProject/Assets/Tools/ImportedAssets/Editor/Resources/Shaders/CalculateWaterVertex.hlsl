#ifndef CALCULATE_WATER_VERTEX_INCLUDED
#include "WaterUtils.cginc"
#include "WaterSettings.cginc"
#include "WaterVertexParameters.cginc"

SamplerState Point_Clamp_Sampler;

inline OutData GetOutData(UnityTexture2DArray waterData, float2 uv, float layer) {
  float3 data = SAMPLE_TEXTURE2D_ARRAY_LOD(waterData, Point_Clamp_Sampler, uv, layer, 0).xyz;
  return CreateOutData(data.x, data.y, data.z);
}

inline float4 GetProperty(UnityTexture2DArray propertyArray, float2 uv, float layer) {
  return SAMPLE_TEXTURE2D_ARRAY_LOD(propertyArray, Point_Clamp_Sampler, uv, layer, 0);
}

inline float GetHeight(UnityTexture2DArray heights, float2 uv, float layer) {
  return SAMPLE_TEXTURE2D_ARRAY_LOD(heights, Point_Clamp_Sampler, uv, layer, 0).r;
}

inline float2 GetOutflow(Texture2DArray outflows, float2 uv, const int layer) {
  return SAMPLE_TEXTURE2D_ARRAY_LOD(outflows, Point_Clamp_Sampler, uv, layer, 0).rg;
}

inline float2 GetTileUv(float2 baseUv, int2 offset) {
  return float2(baseUv.x + offset.x + 0.5, baseUv.y + offset.y + 0.5) / _MapSize;
}

inline float2 GetVertexUv(float2 baseUv, int2 offset) {
  const float2 vertexMapSize = _MapSize * 4;
  return float2(4 * baseUv.x + offset.x + 0.5, 4 * baseUv.y + offset.y + 0.5) / vertexMapSize;
}

inline bool CheckSkirtsMask(const int mask, bool4 skirts) {
  return
    IsMaskBitSet(mask, LEFT_SKIRT_BIT) && skirts.x ||
    IsMaskBitSet(mask, RIGHT_SKIRT_BIT) && skirts.y ||
    IsMaskBitSet(mask, TOP_SKIRT_BIT) && skirts.z ||
    IsMaskBitSet(mask, BOTTOM_SKIRT_BIT) && skirts.w;
}

inline bool IsOldOrNewSideSkirt(const int mask, int4 oldSkirts, int4 newSkirts) {
  return CheckSkirtsMask(mask, bool4(oldSkirts.y > ZERO_EPS || newSkirts.y > ZERO_EPS,
                                     oldSkirts.w > ZERO_EPS || newSkirts.w > ZERO_EPS,
                                     oldSkirts.x > ZERO_EPS || newSkirts.x > ZERO_EPS,
                                     oldSkirts.z > ZERO_EPS || newSkirts.z > ZERO_EPS));
}

inline bool IsOldAndNewSideSkirt(const int mask, int4 oldSkirts, int4 newSkirts) {
  return CheckSkirtsMask(mask, bool4(oldSkirts.y > ZERO_EPS && newSkirts.y > ZERO_EPS,
                                     oldSkirts.w > ZERO_EPS && newSkirts.w > ZERO_EPS,
                                     oldSkirts.x > ZERO_EPS && newSkirts.x > ZERO_EPS,
                                     oldSkirts.z > ZERO_EPS && newSkirts.z > ZERO_EPS));
}

void CalculateWaterVertex_float(UnityTexture2DArray oldWaterData,
                                UnityTexture2DArray newWaterData,
                                UnityTexture2DArray oldEdgeLinksArray,
                                UnityTexture2DArray newEdgeLinksArray,
                                UnityTexture2DArray newCornerLinksArray,
                                UnityTexture2DArray oldBaseCornerLinksArray,
                                UnityTexture2DArray newBaseCornerLinksArray,
                                UnityTexture2DArray oldSkirtsArray,
                                UnityTexture2DArray newSkirtsArray,
                                UnityTexture2DArray oldHeights,
                                UnityTexture2DArray newHeights,
                                float4 uv0,
                                float3 basePosition,
                                out float3 worldPosition,
                                out float3 columnData,
                                out float4 edgeLinks,
                                out float4 cornerLinks,
                                out float2 tileCoordinates,
                                out float2 outflow) {
  const int columnIndex = (int)basePosition.y;
  const int vertexId = (int)uv0.z;
  const int mask = (int)uv0.w;
  tileCoordinates = GetTileUv(uv0.xy, 0);

  OutData oldSelfData = GetOutData(oldWaterData, tileCoordinates, columnIndex);
  OutData newSelfData = GetOutData(newWaterData, tileCoordinates, columnIndex);
  int4 oldEdgeLinks = GetProperty(oldEdgeLinksArray, tileCoordinates, columnIndex);
  int4 newEdgeLinks = GetProperty(newEdgeLinksArray, tileCoordinates, columnIndex);
  int4 newCornerLinks = GetProperty(newCornerLinksArray, tileCoordinates, columnIndex);

  // Tile data to be passed to the fragment shader.
  columnData = float3(columnIndex,
                      lerp(oldSelfData.Floor, newSelfData.Floor, _TickProgress),
                      lerp(oldSelfData.Height, newSelfData.Height, _TickProgress));
  edgeLinks = newEdgeLinks;
  cornerLinks = newCornerLinks;

  // All calculations are done for a tile that either had water in the previous tick or has it in
  // the current tick. Tiles that have no water in both ticks are not processed and all their vertex
  // positions are set to 0.
  worldPosition = float3(0, 0, 0);
  [branch]
  if (oldSelfData.Depth > 0 || newSelfData.Depth > 0) {
    const int4 vertexNeighbours = VertexNeighbours[vertexId];
    const int2 vertexOffset = vertexNeighbours.xy;
    const float2 vertexUv = GetVertexUv(uv0.xy, vertexOffset);
    const bool isEdge = IsMaskBitSet(mask, EDGE_VERTEX_BIT);

    // Initial vertex height is calculated is just a linear interpolation between the old and new.
    const float oldHeight = GetHeight(oldHeights, vertexUv, columnIndex);
    const float newHeight = GetHeight(newHeights, vertexUv, columnIndex);
    float height = lerp(oldHeight, newHeight, _TickProgress);

    // Skip 0.5 influence values, as they are used not used in vertex shader.
    int vertexIndex = vertexOffset.x + 4 * vertexOffset.y;
    float4 directions = InfluenceDirections[vertexIndex];
    directions.x = abs(directions.x) < 1 ? 0 : directions.x;
    directions.y = abs(directions.y) < 1 ? 0 : directions.y;
    directions.z = abs(directions.z) < 1 ? 0 : directions.z;
    directions.w = abs(directions.w) < 1 ? 0 : directions.w;

    int2 horizontalNeighbor = directions.xy;
    int2 horizontalLink = -1;
    if (horizontalNeighbor.x > 0) {
      horizontalLink = int2(oldEdgeLinks.w, newEdgeLinks.w);
    } else if (horizontalNeighbor.x < 0) {
      horizontalLink = int2(oldEdgeLinks.y, newEdgeLinks.y);
    }

    int2 verticalNeighbor = directions.zw;
    int2 verticalLink = -1;
    if (verticalNeighbor.y > 0) {
      verticalLink = int2(oldEdgeLinks.x, newEdgeLinks.x);
    } else if (verticalNeighbor.y < 0) {
      verticalLink = int2(oldEdgeLinks.z, newEdgeLinks.z);
    }

    int4 oldBaseCornerLinks = GetProperty(oldBaseCornerLinksArray, tileCoordinates, columnIndex);
    int4 newBaseCornerLinks = GetProperty(newBaseCornerLinksArray, tileCoordinates, columnIndex);
    int2 diagonalNeighbor = directions.xw;
    int2 diagonalLink = -1;
    if (diagonalNeighbor.x > 0 && diagonalNeighbor.y > 0) {
      diagonalLink = float2(oldBaseCornerLinks.y, newBaseCornerLinks.y);
    } else if (diagonalNeighbor.x < 0 && diagonalNeighbor.y > 0) {
      diagonalLink = float2(oldBaseCornerLinks.x, newBaseCornerLinks.x);
    } else if (diagonalNeighbor.x > 0 && diagonalNeighbor.y < 0) {
      diagonalLink = float2(oldBaseCornerLinks.w, newBaseCornerLinks.w);
    } else if (diagonalNeighbor.x < 0 && diagonalNeighbor.y < 0) {
      diagonalLink = float2(oldBaseCornerLinks.z, newBaseCornerLinks.z);
    }

    // Calculate per-vertex outflows. Edge vertices should use average outflow of all connected
    // vertices, while non-edge vertices should use their own outflow.
    outflow = 0;
    #if _CALCULATE_OUTFLOWS && !SHADERGRAPH_PREVIEW

    float2 oldSelfOutflow = GetOutflow(_OldOutflows, tileCoordinates, columnIndex);
    float2 newSelfOutflow = GetOutflow(_NewOutflows, tileCoordinates, columnIndex);

    if (isEdge) {
      float2 oldOutflowSum = oldSelfOutflow;
      float2 newOutflowSum = newSelfOutflow;
      int2 outflowDivisor = 1;

      const float2 horizontalTileUv = GetTileUv(uv0.xy, horizontalNeighbor);
      if (horizontalLink.x >= 0) {
        oldOutflowSum += GetOutflow(_OldOutflows, horizontalTileUv, horizontalLink.x);
        outflowDivisor.x += 1;
      }
      if (horizontalLink.y >= 0) {
        newOutflowSum += GetOutflow(_NewOutflows, horizontalTileUv, horizontalLink.y);
        outflowDivisor.y += 1;
      }

      const float2 verticalTileUv = GetTileUv(uv0.xy, verticalNeighbor);
      if (verticalLink.x >= 0) {
        oldOutflowSum += GetOutflow(_OldOutflows, verticalTileUv, verticalLink.x);
        outflowDivisor.x += 1;
      }
      if (verticalLink.y >= 0) {
        newOutflowSum += GetOutflow(_NewOutflows, verticalTileUv, verticalLink.y);
        outflowDivisor.y += 1;
      }
      
      const float2 diagonalTileUv = GetTileUv(uv0.xy, diagonalNeighbor);
      if (diagonalLink.x >= 0) {
        oldOutflowSum += GetOutflow(_OldOutflows, diagonalTileUv, diagonalLink.x);
        outflowDivisor.x += 1;
      }
      if (diagonalLink.y >= 0) {
        newOutflowSum += GetOutflow(_OldOutflows, diagonalTileUv, diagonalLink.y);
        outflowDivisor.y += 1;
      }

      float2 outflowOld = oldOutflowSum / outflowDivisor.x;
      float2 outflowNew = newOutflowSum / outflowDivisor.y;
      outflow = lerp(outflowOld, outflowNew, _TickProgress);
    } else {
      outflow = lerp(oldSelfOutflow, newSelfOutflow, _TickProgress);
    }

    #endif

    // Add a vertical spilling offset to vertices that are just about to be covered by water.
    // This is used to make water appear from below the terrain, which makes it look more smoothly.
    float spillingOffset = 0;
    const bool waterJustApproached = oldSelfData.Depth == 0 && newSelfData.Depth > 0;
    if (waterJustApproached) {
      bool canAddSpillingOffset = false;
      const bool hadLinkToNeighbor = horizontalLink.x >= 0 || verticalLink.x >= 0;
      const bool hasLinkToNeighbor = horizontalLink.y >= 0 || verticalLink.y >= 0;
      if (!hasLinkToNeighbor) {
        if (hadLinkToNeighbor) {
          bool isHorizontal = horizontalLink.x >= 0;
          const int neighborLink = isHorizontal ? horizontalLink.x : verticalLink.x;
          const int2 neighborDirection = isHorizontal ? directions.xy : directions.zw;
          const float2 neighborTileUv = GetTileUv(uv0.xy, neighborDirection);
          OutData neighborData = GetOutData(newWaterData, neighborTileUv, neighborLink);
          if (neighborData.Depth > 0) {
            canAddSpillingOffset = true;
          }
        } else
          canAddSpillingOffset = true;
      } else if (hasLinkToNeighbor && !hadLinkToNeighbor) {
        canAddSpillingOffset = true;
      }

      if (canAddSpillingOffset) {
        float maxSpillingOffset = isEdge ? MAX_SPILLING_OFFSET_EDGE : MAX_SPILLING_OFFSET_NON_EDGE;
        spillingOffset = lerp(maxSpillingOffset, 0, pow(_TickProgress, SPILLING_OFFSET_EXPONENT));
      }
    }

    // Add an edge offset to vertices that are on the edge of the water. This is used to make water
    // appear more naturally and without sharp corners. This is applied only to shallow water.
    // The offset is applied only to vertices that are not connected to any other water vertices.
    float2 edgeOffset = 0;
    [branch]
    if (isEdge) {
      bool canApplyEdgeOffset = false;
      bool isCorner = IsMaskBitSet(mask, CORNER_VERTEX_BIT);
      if (isCorner) {
        bool hadNoMainLink = horizontalLink.x == -1 && verticalLink.x == -1;
        bool hasNoMainLink = horizontalLink.y == -1 && verticalLink.y == -1;
        bool hadNoCornerLink = diagonalLink.x == -1;
        bool hasNoCornerLink = diagonalLink.y == -1;
        bool hadBothSideLinks = (horizontalLink.x >= 0 && verticalLink.x >= 0);
        bool hasBothSideLinks = (horizontalLink.y >= 0 && verticalLink.y >= 0);
        canApplyEdgeOffset =
          ((hasNoMainLink && hadNoMainLink) || (hadNoCornerLink && hasNoCornerLink))
          && !hadBothSideLinks && !hasBothSideLinks;
      } else {
        bool isHorizontal = directions.x != 0 && directions.w == 0;
        const bool hadConnectionToNeighbor =
          (isHorizontal && horizontalLink.x >= 0) || (!isHorizontal && verticalLink.x >= 0);
        const bool hasConnectionToNeighbor =
          (isHorizontal && horizontalLink.y >= 0) || (!isHorizontal && verticalLink.y >= 0);
        if (!hasConnectionToNeighbor) {
          if (hadConnectionToNeighbor) {
            const int neighborConnection = isHorizontal ? horizontalLink.x : verticalLink.x;
            const int2 neighborDirection = isHorizontal ? directions.xy : directions.zw;
            const float2 neighborUv = GetTileUv(uv0.xy, neighborDirection);
            OutData neighborData = GetOutData(newWaterData, neighborUv, neighborConnection);
            if (neighborData.Depth > 0) {
              canApplyEdgeOffset = true;
            }
          } else
            canApplyEdgeOffset = true;
        }
      }

      if (canApplyEdgeOffset) {
        float2 offsetDirection = -normalize(directions.xw);
        if (isCorner) {
          bool wasLinkedOnlyHorizontally = horizontalLink.x >= 0 && verticalLink.x < 0;
          bool isLinkedOnlyHorizontally = horizontalLink.y >= 0 && verticalLink.y < 0;
          if (wasLinkedOnlyHorizontally || isLinkedOnlyHorizontally) {
            offsetDirection = normalize(float2(0, -directions.w));
          } else {
            bool wasLinkedOnlyVertically = horizontalLink.x < 0 && verticalLink.x >= 0;
            bool isLinkedOnlyVertically = horizontalLink.y < 0 && verticalLink.y >= 0;
            if (wasLinkedOnlyVertically || isLinkedOnlyVertically) {
              offsetDirection = normalize(float2(-directions.x, 0));
            } else {
              offsetDirection *= 2;
            }
          }
        }

        float outflowLength = saturate(length(outflow));
        float oldVertexDepth = saturate(oldHeight - oldSelfData.Floor);
        float newVertexDepth = saturate(newHeight - newSelfData.Floor);
        float vertexDepth = lerp(oldVertexDepth, newVertexDepth, _TickProgress);
        float depthInfluence =
          saturate((vertexDepth - EDGE_OFFSET_MAX_DEPTH) / (EDGE_OFFSET_DEPTH_SPAN));
        float outflowInfluence =
          saturate((EDGE_OFFSET_MAX_OUTFLOW - outflowLength) / EDGE_OFFSET_MAX_OUTFLOW);

        edgeOffset = MAX_EDGE_OFFSET * lerp(0, offsetDirection, depthInfluence * outflowInfluence);
      }
    }


    // Set height for skirts, that should cover all 4 sides of the tile (if needed).
    const bool isSkirt = IsMaskBitSet(mask, SKIRT_BIT);
    const bool isFloorSkirt = IsMaskBitSet(mask, FLOOR_SKIRT_BIT);
    [branch]
    if (isSkirt) {
      const int2 neighborVertexOffset = vertexNeighbours.zw;
      int2 neighborVertexLink = -1;
      if (neighborVertexOffset.x > 0) {
        neighborVertexLink = int2(oldEdgeLinks.w, newEdgeLinks.w);
      } else if (neighborVertexOffset.x < 0) {
        neighborVertexLink = int2(oldEdgeLinks.y, newEdgeLinks.y);
      } else if (neighborVertexOffset.y > 0) {
        neighborVertexLink = int2(oldEdgeLinks.x, newEdgeLinks.x);
      } else if (neighborVertexOffset.y < 0) {
        neighborVertexLink = int2(oldEdgeLinks.z, newEdgeLinks.z);
      }
      int4 oldSkirts = GetProperty(oldSkirtsArray, tileCoordinates, columnIndex);
      int4 newSkirts = GetProperty(newSkirtsArray, tileCoordinates, columnIndex);
      bool oldOrNewSkirts = IsOldOrNewSideSkirt(mask, oldSkirts, newSkirts);
      bool oldAndNewSkirts = IsOldAndNewSideSkirt(mask, oldSkirts, newSkirts);

      if (isFloorSkirt) {
        if (neighborVertexLink.x == -1 || neighborVertexLink.y == -1 || oldOrNewSkirts) {
          bool isMapBorder = basePosition.x < MAP_BORDER_TOLERANCE
             || basePosition.z < MAP_BORDER_TOLERANCE
             || basePosition.x > _MapSize.x - MAP_BORDER_TOLERANCE
             || basePosition.z > _MapSize.y - MAP_BORDER_TOLERANCE;
          bool isNotLinkedToNeighbor = neighborVertexLink.x == -1 && neighborVertexLink.y == -1;
          bool isAnyColumnAbove = GetOutData(newWaterData, tileCoordinates, columnIndex + 1).Floor > 0;
          height = isMapBorder || (isNotLinkedToNeighbor && !isAnyColumnAbove)
                     ? lerp(oldSelfData.Floor, newSelfData.Floor, _TickProgress)
                     : lerp(oldSelfData.Height, newSelfData.Height, _TickProgress);
        } else {
          const float2 neighborVertexUv = GetVertexUv(uv0.xy, vertexOffset + neighborVertexOffset);
          const float oldNeighborHeight = GetHeight(oldHeights, neighborVertexUv,
                                                    neighborVertexLink.x);
          const float newNeighborHeight = GetHeight(newHeights, neighborVertexUv,
                                                    neighborVertexLink.y);
          const float neighborHeight = lerp(oldNeighborHeight, newNeighborHeight, _TickProgress);
          height = max(min(neighborHeight, height), oldSelfData.Floor);
        }
      } else if (oldAndNewSkirts) {
        if (neighborVertexLink.x >= 0 && neighborVertexLink.y >= 0) {
          const float2 neighborTileUv = GetTileUv(uv0.xy, neighborVertexOffset);
          float oldTarget = GetOutData(oldWaterData, neighborTileUv, neighborVertexLink.x).Floor;
          float newTarget = GetOutData(newWaterData, neighborTileUv, neighborVertexLink.y).Floor;
          float targetHeight = lerp(oldTarget, newTarget, _TickProgress);
          if (targetHeight > max(oldSelfData.Height, newSelfData.Height)) {
            height = targetHeight;
          }
        }
      }
    }

    height += spillingOffset;
    #if _USE_LEVEL_VISIBILITY
    height = min(_MaxVisibleLevel + 0.85, height);
    #endif
    worldPosition = float3(basePosition.x, height, basePosition.z)
      + float3(edgeOffset.x, 0, edgeOffset.y)
      + WATER_VERTICAL_OFFSET;

    #if _USE_LEVEL_VISIBILITY
    if (_MaxVisibleLevel < max(oldSelfData.Floor, newSelfData.Floor)) {
      worldPosition = 0;
    }
    #endif
  }
}


#endif
