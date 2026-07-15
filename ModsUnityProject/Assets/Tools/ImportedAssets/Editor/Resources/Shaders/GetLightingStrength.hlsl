#ifndef GET_LIGHTING_STRENGTH_INCLUDED
float LightingStrengthMultiplier;

void GetLightingStrength_float(out float value) {
  value = unity_RendererUserValue * LightingStrengthMultiplier;
}
#endif
