float3 RotateVectorByQuaternion(float3 v, float4 q) {
  const float3 t = 2.0 * cross(q.xyz, v);
  return v + q.w * t + cross(q.xyz, t);
}

void Animate_float(float vertexId,
                   float3 inputPosition,
                   float3 inputNormal,
                   float3 inputTangent,
                   float animationTime,
                   float animatedVertexCount,
                   float frameCount,
                   UnityTexture2D offsets,
                   UnityTexture2D rotations,
                   float looped,
                   UnitySamplerState loopedOffsetsSampler,
                   UnitySamplerState oneShotOffsetsSampler,
                   UnitySamplerState loopedRotationsSampler,
                   UnitySamplerState oneShotRotationsSampler,
                   out float3 position,
                   out float3 normal,
                   out float3 tangent) {
  if (vertexId < animatedVertexCount) {
    const float vertexIndex = (vertexId + 0.5) / (animatedVertexCount);
    const float frameIndex = animationTime + 0.5 * (1.0 / frameCount);
    const float3 offset = looped
                            ? SAMPLE_TEXTURE2D_LOD(offsets, loopedOffsetsSampler,
                                                   float2(vertexIndex, frameIndex), 0).xyz
                            : SAMPLE_TEXTURE2D_LOD(offsets, oneShotOffsetsSampler,
                                                   float2(vertexIndex, frameIndex), 0).xyz;

    const float targetFrame = frameIndex * frameCount;
    const float fromFrame = floor(targetFrame) / frameCount;
    const float toFrame = ceil(targetFrame) / frameCount;
    const float weight = frac(targetFrame);

    const float4 fromRotation = looped
                                  ? SAMPLE_TEXTURE2D_LOD(rotations, loopedRotationsSampler,
                                                         float2(vertexIndex, fromFrame), 0)
                                  : SAMPLE_TEXTURE2D_LOD(rotations, oneShotRotationsSampler,
                                                         float2(vertexIndex, fromFrame), 0);

    const float4 toRotation = looped
                                ? SAMPLE_TEXTURE2D_LOD(rotations, loopedRotationsSampler,
                                                       float2(vertexIndex, toFrame), 0)
                                : SAMPLE_TEXTURE2D_LOD(rotations, oneShotRotationsSampler,
                                                       float2(vertexIndex, toFrame), 0);

    position = inputPosition + offset;
    const float3 fromNormal = RotateVectorByQuaternion(float3(-1, 0, 0), fromRotation);
    const float3 toNormal = RotateVectorByQuaternion(float3(-1, 0, 0), toRotation);
    const float3 fromTangent = RotateVectorByQuaternion(float3(0, 0, -1), fromRotation);
    const float3 toTangent = RotateVectorByQuaternion(float3(0, 0, -1), toRotation);

    normal = lerp(fromNormal, toNormal, weight);
    tangent = lerp(fromTangent, toTangent, weight);
  } else {
    position = inputPosition;
    normal = inputNormal;
    tangent = inputTangent;
  }
}
