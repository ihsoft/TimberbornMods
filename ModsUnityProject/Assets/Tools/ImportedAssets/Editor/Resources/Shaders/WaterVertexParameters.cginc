#ifndef WATER_VERTEX_PARAMETERS_INCLUDED
#define WATER_VERTEX_PARAMETERS_INCLUDED

static const float4 InfluenceDirections[16] =
{
  float4(-1, 0, 0, -1),
  float4(-0.5, 0, 0, -1),
  float4(0.5, 0, 0, -1),
  float4(1, 0, 0, -1),
  float4(-1, 0, 0, -0.5),
  float4(-0.5, 0, 0, -0.5),
  float4(0.5, 0, 0, -0.5),
  float4(1, 0, 0, -0.5),
  float4(-1, 0, 0, 0.5),
  float4(-0.5, 0, 0, 0.5),
  float4(0.5, 0, 0, 0.5),
  float4(1, 0, 0, 0.5),
  float4(-1, 0, 0, 1),
  float4(-0.5, 0, 0, 1),
  float4(0.5, 0, 0, 1),
  float4(1, 0, 0, 1),
};

// Vertex neighbours
static const int4 VertexNeighbours[48] =
{
  // Top surface
  int4(0, 0, 0, 0),
  int4(1, 0, 0, 0),
  int4(2, 0, 0, 0),
  int4(3, 0, 0, 0),
  int4(0, 1, 0, 0),
  int4(1, 1, 0, 0),
  int4(2, 1, 0, 0),
  int4(3, 1, 0, 0),
  int4(0, 2, 0, 0),
  int4(1, 2, 0, 0),
  int4(2, 2, 0, 0),
  int4(3, 2, 0, 0),
  int4(0, 3, 0, 0),
  int4(1, 3, 0, 0),
  int4(2, 3, 0, 0),
  int4(3, 3, 0, 0),
  // Side skirts (edge)
  int4(0, 0, 0, -1),
  int4(1, 0, 0, -1),
  int4(2, 0, 0, -1),
  int4(3, 0, 0, -1),
  int4(0, 0, -1, 0),
  int4(0, 1, -1, 0),
  int4(0, 2, -1, 0),
  int4(0, 3, -1, 0),
  int4(3, 0, 1, 0),
  int4(3, 1, 1, 0),
  int4(3, 2, 1, 0),
  int4(3, 3, 1, 0),
  int4(0, 3, 0, 1),
  int4(1, 3, 0, 1),
  int4(2, 3, 0, 1),
  int4(3, 3, 0, 1), 
  // Side skirts (floor)
  int4(0, 0, 0, -1),
  int4(1, 0, 0, -1),
  int4(2, 0, 0, -1),
  int4(3, 0, 0, -1),
  int4(0, 0, -1, 0),
  int4(0, 1, -1, 0),
  int4(0, 2, -1, 0),
  int4(0, 3, -1, 0),
  int4(3, 0, 1, 0),
  int4(3, 1, 1, 0),
  int4(3, 2, 1, 0),
  int4(3, 3, 1, 0),
  int4(0, 3, 0, 1),
  int4(1, 3, 0, 1),
  int4(2, 3, 0, 1),
  int4(3, 3, 0, 1),
};

#endif
