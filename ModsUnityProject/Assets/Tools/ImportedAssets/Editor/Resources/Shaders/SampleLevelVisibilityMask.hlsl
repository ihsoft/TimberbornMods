#ifndef SAMPLE_LEVEL_VISIBILITY_MASK_INCLUDED
#define LEVEL_VISIBILITY_THRESHOLD 0.001

void SampleLevelVisibilityMask_float(float4 inColor,
                                     UnityTexture2D mask,
                                     float3 worldPosition,
                                     float colorIntensity,
                                     float4 lowColor,
                                     float4 highColor,
                                     float opacity,
                                     out float4 outColor) {
  #if _USE_LEVEL_VISIBILITY && !SHADERGRAPH_PREVIEW
  const float levelDifference = _MaxVisibleLevel - worldPosition.y + 0.85;
  const float visibilityMask = levelDifference < LEVEL_VISIBILITY_THRESHOLD ? 1 : 0;
  const float4 maskPattern = SAMPLE_TEXTURE2D(mask, mask.samplerstate, worldPosition.xz);
  const float4 maskColor = lerp(lowColor, highColor, colorIntensity) * maskPattern;
  outColor = lerp(inColor, maskColor, visibilityMask * maskColor.a * opacity);
  #else
  outColor = inColor;
  #endif
}

#endif
