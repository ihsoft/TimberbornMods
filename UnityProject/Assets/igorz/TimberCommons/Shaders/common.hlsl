// This file is shared between multiple simulation shaders. It contains common functions, parameters, and structures.
// The actual logic of running kernels is implemented in the Unity code. See GpuSimulatorsController.cs.

// Common functions.
#define MAX4(v1, v2, v3, v4) max(max(v1, v2), max(v3, v4))
#define MAX8(v1, v2, v3, v4, v5, v6, v7, v8) max(MAX4(v1, v2, v3, v4), MAX4(v5, v6, v7, v8))
#define CoordinatesToIndex(coordinates) ((coordinates.y + 1) * Stride + coordinates.x + 1)
#define IndexToCoordinates(index) int2((index % Stride) - 1, (index / Stride) - 1)
#define Clamp01(value) clamp(value, 0.0, 1.0)
#define InverseLerp(a, b, value) (a != b ? Clamp01((value - a) / (b - a)) : 0.0) 
#define SetBitmapFlag(reference, flag, value) reference.BitmapFlags = value ? reference.BitmapFlags | flag : reference.BitmapFlags & ~flag
#define CheckBitmapFlag(reference, flag) ((reference.BitmapFlags & flag) != 0)

// Common parameters. Set the values only once.
uint Stride;

// Common structures and types.
struct BitmapFlags {
    static const uint ContaminationBarrierBit = 0x0001;
    static const uint AboveMoistureBarrierBit = 0x0002;
    static const uint FullMoistureBarrierBit = 0x0004;
    static const uint WaterTowerIrrigatedBit = 0x0008;
    static const uint PartialObstaclesBit = 0x0010;
    static const uint IsInActualMapBit = 0x0020;
};

#define AboveMoistureBarriers(index) (BitmapFlagsBuff[index] & BitmapFlags::AboveMoistureBarrierBit) != 0
#define ContaminationBarriers(index) (BitmapFlagsBuff[index] & BitmapFlags::ContaminationBarrierBit) != 0
#define FullMoistureBarriers(index) (BitmapFlagsBuff[index] & BitmapFlags::FullMoistureBarrierBit) != 0
#define WaterTowerIrrigated(index) (BitmapFlagsBuff[index] & BitmapFlags::WaterTowerIrrigatedBit) != 0
#define ImpermeableSurfaceServicePartialObstacles(index) (BitmapFlagsBuff[index] & BitmapFlags::PartialObstaclesBit) != 0
#define IndexIsInActualMap(index) (BitmapFlagsBuff[index] & BitmapFlags::IsInActualMapBit) != 0

typedef StructuredBuffer<float> TContaminationsBuff;
typedef RWStructuredBuffer<float> TRWContaminationsBuff;
typedef StructuredBuffer<float> TWaterDepthsBuff;
typedef RWStructuredBuffer<float> TRWWaterDepthsBuff;
typedef StructuredBuffer<int> TUnsafeCellHeightsBuff;
typedef StructuredBuffer<float> TEvaporationModifiersBuff;
typedef RWStructuredBuffer<float> TRWEvaporationModifiersBuff;
typedef StructuredBuffer<uint> TBitmapFlagsBuff;

#define Contaminations(index) ContaminationsBuff[index]
#define WaterDepths(index) WaterDepthsBuff[index]
#define UnsafeCellHeights(index) UnsafeCellHeightsBuff[index]
#define EvaporationModifiers(index) EvaporationModifiersBuff[index]

#define CeiledWaterHeights(index) ceil(WaterDepths(index) + UnsafeCellHeights(index))
