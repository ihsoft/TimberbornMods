static float3 JitterHash(float3 input) {
  float n = sin(dot(input, float3(7.0, 157.0, 113.0)));
  return frac(float3(2097152.0, 104857.0, 32768.0) * n);
}

static float ExistenceHash(float3 input) {
  float h = dot(input, float3(127.1, 311.7, 74.7));
  return frac(sin(h) * 43758.5453123);
}

static float HueHash(float input) {
  input = frac(input * 0.1031);
  input *= input + 33.33;
  input *= input + input;
  return saturate(frac(input));
}

static float GetStarShape(float radius, float size, float softness) {
  float edge0 = size;
  float edge1 = size * (1.0 + softness);
  float shape = 1.0 - smoothstep(edge0, edge1, radius);
  return shape * shape;
}

static float3 GetStarColor(float temperature) {
  float3 color = lerp(_ColdColor.xyz, _WarmColor.xyz, temperature);
  color.g = lerp(color.g, (color.r + color.b) * 0.5, 0.15);
  return color;
}

void SkyboxStars_float(float3 direction, out float3 color) {
  float3 position = normalize(direction) * _StarScale;
  float3 cell = floor(position);
  float3 localOffset = position - cell;
  float3 starColor = 0;
  [unroll]
  for (int x = 0; x <= 1; x++) {
    [unroll]
    for (int y = 0; y <= 1; y++) {
      [unroll]
      for (int z = 0; z <= 1; z++) {
        float3 samplePosition = cell + float3(x, y, z);
        float3 jitter = JitterHash(samplePosition) - 0.5;
        float3 starPosition = float3(x, y, z) + jitter;

        float3 radius = localOffset - starPosition;
        float radiusLength = length(radius);
        float starShape = GetStarShape(radiusLength, _StarSize, _StarSoftness);

        float starExists = step(1.0 - _StarDensity, ExistenceHash(samplePosition));
        float luminance = starShape * starExists;
        float hueSeed = HueHash(dot(samplePosition, 1.0));
        starColor += GetStarColor(hueSeed) * luminance * 1;
      }
    }
  }
  color = saturate(starColor * _StarIntensity);
}
