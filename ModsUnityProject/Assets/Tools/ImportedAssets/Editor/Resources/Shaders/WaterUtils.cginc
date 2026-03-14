#ifndef WATER_UTILS_INCLUDED
#define WATER_UTILS_INCLUDED

#include "WaterSettings.cginc"
#define EDGE_VERTEX_BIT 0
#define CORNER_VERTEX_BIT 1
#define SKIRT_BIT 2
#define LEFT_SKIRT_BIT 3
#define RIGHT_SKIRT_BIT 4
#define TOP_SKIRT_BIT 5
#define BOTTOM_SKIRT_BIT 6
#define FLOOR_SKIRT_BIT 7

struct InData {
  float Depth;
  int Floor;
  int Ceiling;
  float Height;
  bool CanLinkTop;
  bool CanLinkLeft;
  bool CanLinkBottom;
  bool CanLinkRight;
  float FlowLimit;
  float2 Outflow;
};

struct OutData {
  float Depth;
  int Floor;
  int Ceiling;
  float Height;
};

inline bool IsMaskBitSet(const int number, const int bitNumber) {
  return (number & 1 << bitNumber) != 0;
}

inline bool IsMaskBitSet(const uint number, const int bitNumber) {
  return (number & 1 << bitNumber) != 0;
}

InData CreateInData(const float depth,
                    const int floor,
                    const int ceiling,
                    const float2 flowLimiter,
                    const float flowLimit,
                    const float2 outflow) {
  InData data;
  data.Depth = depth;
  data.Floor = floor;
  data.Ceiling = ceiling;
  data.Height = floor + depth;
  data.CanLinkTop = flowLimiter.x <= ZERO_EPS;
  data.CanLinkLeft = flowLimiter.y <= ZERO_EPS;
  data.CanLinkBottom = flowLimiter.x <= ZERO_EPS;
  data.CanLinkRight = flowLimiter.y <= ZERO_EPS;
  data.FlowLimit = flowLimit;
  data.Outflow = outflow;
  return data;
}

OutData CreateOutData(const float depth,
                      const int floor,
                      const int ceiling) {
  OutData data;
  data.Depth = depth;
  data.Floor = floor;
  data.Ceiling = ceiling;
  data.Height = floor + depth;
  return data;
}


#endif // WATER_UTILS_INCLUDED
