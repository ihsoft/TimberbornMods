void SampleTriplanarTerrain_float(float3 inputPosition,
                                 float3 inputNormal,
                                 float3 inputTangent,
                                 float3 inputBitangent,
                                 const float albedoTiling,
                                 float normalTiling,
                                 UnityTexture2D albedoTexture,
                                 UnityTexture2D normalTexture,
                                 out float4 albedo,
                                 out float3 normal) {
  float3 albedoUv = inputPosition * albedoTiling;
  float3x3 transformMatrix = float3x3(inputTangent, inputBitangent, inputNormal);
  #if _SAMPLE_SPLATMAP && _USE_TRIPLANAR_SPLATMAP

  float3 blending = max(abs(inputNormal), 0);
  blending /= dot(blending, 1.0);
  float4 albedoZY = SAMPLE_TEXTURE2D(albedoTexture, albedoTexture.samplerstate, albedoUv.zy);
  float4 albedoXY = SAMPLE_TEXTURE2D(albedoTexture, albedoTexture.samplerstate, albedoUv.xy);
  float4 albedoXZ = SAMPLE_TEXTURE2D(albedoTexture, albedoTexture.samplerstate, albedoUv.xz);
  albedo = albedoZY * blending.x + albedoXY * blending.z + albedoXZ * blending.y;

  float3 normalUV = inputPosition * normalTiling;
  float3 normalZY =
    UnpackNormal(SAMPLE_TEXTURE2D(normalTexture, normalTexture.samplerstate, normalUV.zy));
  float3 normalXY =
    UnpackNormal(SAMPLE_TEXTURE2D(normalTexture, normalTexture.samplerstate, normalUV.xy));
  float3 normalXZ =
    UnpackNormal(SAMPLE_TEXTURE2D(normalTexture, normalTexture.samplerstate, normalUV.xz));
  normalZY = float3(normalZY.xy + inputNormal.zy, abs(normalZY.z) * inputNormal.x);
  normalXY = float3(normalXY.xy + inputNormal.xy, abs(normalXY.z) * inputNormal.z);
  normalXZ = float3(normalXZ.xy + inputNormal.xz, abs(normalXZ.z) * inputNormal.y);
  float3 worldNormal = normalize(normalZY.zyx * blending.x + normalXY.xyz * blending.z + normalXZ.xzy * blending.y);
  normal = TransformWorldToTangent(worldNormal, transformMatrix);
  #elif _SAMPLE_SPLATMAP
  albedo = SAMPLE_TEXTURE2D(albedoTexture, albedoTexture.samplerstate, albedoUv.xz);
  normal = TransformWorldToTangent(float3(0, 1, 0), transformMatrix);
  #else
  albedo = float4(1, 1, 1, 1);
  normal = TransformWorldToTangent(float3(0, 1, 0), transformMatrix);
  #endif
}
