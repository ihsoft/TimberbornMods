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

// Common structures.
struct InputStruct1 {
    float Contaminations;
    float WaterDepths;
    int UnsafeCellHeights;
    uint BitmapFlags;

    static const uint ContaminationBarrierBit = 0x0001;
    static const uint AboveMoistureBarrierBit = 0x0002;
    static const uint FullMoistureBarrierBit = 0x0004;
    static const uint WaterTowerIrrigatedBit = 0x0008;
};

#define AboveMoistureBarriers(index) CheckBitmapFlag(PackedInput1[index], InputStruct1::AboveMoistureBarrierBit)
#define ContaminationBarriers(index) CheckBitmapFlag(PackedInput1[index], InputStruct1::ContaminationBarrierBit)
#define FullMoistureBarriers(index) CheckBitmapFlag(PackedInput1[index], InputStruct1::FullMoistureBarrierBit)
#define WaterTowerIrrigated(index) CheckBitmapFlag(PackedInput1[index], InputStruct1::WaterTowerIrrigatedBit)
#define UnsafeCellHeights(index) PackedInput1[index].UnsafeCellHeights
