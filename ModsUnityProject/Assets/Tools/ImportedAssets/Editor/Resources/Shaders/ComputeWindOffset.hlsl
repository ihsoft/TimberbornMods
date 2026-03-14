#define PHI 1.6185
#define FLUTTER_THRESHOLD_BAND 0.05
#define VERTICAL_SWAY 0.5
#define WIND_GUST_THRESHOLD 0.75
#define WIND_GUST_MODIFIER 1.5

float HashFromPosition(float3 position) {
  return frac(sin(dot(position, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

void ComputeWindOffset_float(float3 vertexPosition,
                             float3 objectPosition,
                             float2 uv,
                             float time,
                             float noise,
                             out float3 outPosition) {
  float hash = HashFromPosition(objectPosition);
  float swayPhaseOffset    = TWO_PI * hash; 
  float flutterPhaseOffset = TWO_PI * frac(noise * PHI);

  float movementIntensity = uv.y;
  float sway = _SwayStrength
                * sin(swayPhaseOffset + time * _SwaySpeed) * pow(movementIntensity, _SwayExponent);
  float flutter = _FlutterStrength
                * sin(flutterPhaseOffset + time * _FlutterSpeed) * pow(movementIntensity, _FlutterExponent)
                * smoothstep(_FlutterThreshold - FLUTTER_THRESHOLD_BAND, _FlutterThreshold, movementIntensity);
  
  float2 windDirection = float2(cos(swayPhaseOffset + time), sin(swayPhaseOffset));
  float2 windDirectionPerpendicular = float2(windDirection.y, -windDirection.x);
  float2 offset = (windDirection * sway + windDirectionPerpendicular * flutter);

  float windStrengthModifier = smoothstep(WIND_GUST_THRESHOLD, 1, _WindStrength);
  float windStrength = _WindModifier * lerp(1, WIND_GUST_MODIFIER, windStrengthModifier);
  outPosition = vertexPosition + windStrength * float3(offset.x, VERTICAL_SWAY * sway, offset.y);
}

