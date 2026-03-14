void GetSamplingParameters_float(const float2 inputPosition,
                                 const float2 offset,
                                 const float selfIndex,
                                 float4 edgeLinks,
                                 float4 cornerLinks,
                                 out float2 samplingPosition,
                                 out float samplingIndex,
                                 out float weight) {
  const float directionalLinks[9] = {
    cornerLinks.z,
    edgeLinks.z,
    cornerLinks.w,
    edgeLinks.y,
    selfIndex,
    edgeLinks.w,
    cornerLinks.x,
    edgeLinks.x,
    cornerLinks.y
  };

  samplingPosition = inputPosition + offset;
  const int2 referenceTile = floor(inputPosition);
  const int2 targetTile = floor(samplingPosition);
  int2 direction = targetTile - referenceTile;

  const int xIndex = clamp(direction.x + 1, 0, 2);
  const int zIndex = clamp(direction.y + 1, 0, 2);
  int directionIndex = zIndex * 3 + xIndex;
  samplingIndex = directionalLinks[directionIndex];

  weight = 1;
  if (samplingIndex < 0) {
    samplingIndex = selfIndex;
    weight = 0;
  }
}
