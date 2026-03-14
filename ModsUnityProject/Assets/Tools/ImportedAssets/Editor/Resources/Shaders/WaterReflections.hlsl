void WaterReflections_float(float3 normal,          
                            float3 viewDirection,              
                            float3 cameraDirection,        
                            float3 position,
                            float specularExponent,
                            float fresnelPower,
                            float fresnelWrap,
                            float lightHorizonBias,
                            float lightTilt,
                            float clearReflectionIntensity,
                            float contaminatedReflectionIntensity,
                            float contamination,
                            float waterfallMask,
                            out float3 reflectionColor) {
  #if _HIGH_QUALITY_WATER_ENABLED
  cameraDirection = normalize(cameraDirection);

  #if SHADERGRAPH_PREVIEW
  float4 shadowCoords = TransformWorldToShadowCoord(position);
  #else
  float4 shadowCoords = float4(0, 0, 0, 0);
  #endif
  Light mainLight = GetMainLight(shadowCoords, position, float4(0,0,0,0));

  float3 planarCameraDirection = normalize(float3(cameraDirection.x, 0, cameraDirection.z));
  float3 lightDirection = normalize(lerp(cameraDirection, planarCameraDirection, lightHorizonBias)
                         + float3(0, -saturate(lightTilt), 0));
  
  float3 reflectedView = reflect(-lightDirection, normal);
  float specularLight = saturate(dot(reflectedView, viewDirection));
  specularLight = pow(specularLight, specularExponent);

  float nv = saturate(dot(normal, viewDirection));
  float fresnel = pow(1.0 - nv, fresnelPower);          
  float nl = saturate(dot(normal, lightDirection));
  float fresnelWrapping = saturate((nl + fresnelWrap) / (1.0 + fresnelWrap));
  
  float horizonFade = saturate(1.0 - pow(abs(normal.y), 3.0));
  float specularMask = specularLight * (0.5 + 0.5 * fresnel) * fresnelWrapping * horizonFade;

  float reflectionIntensity = lerp(clearReflectionIntensity, contaminatedReflectionIntensity, contamination);
  reflectionIntensity *= (1 - waterfallMask);
  reflectionColor = specularMask * reflectionIntensity * mainLight.shadowAttenuation * mainLight.color;
  #else
  reflectionColor = float3(0, 0, 0);
  #endif
}

