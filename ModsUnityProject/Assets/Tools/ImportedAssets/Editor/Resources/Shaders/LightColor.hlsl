void LightColor_float(out float3 color) {
  Light mainLight = GetMainLight();
  color = mainLight.color;  
}

