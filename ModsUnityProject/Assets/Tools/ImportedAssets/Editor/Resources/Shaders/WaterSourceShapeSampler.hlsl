#define UV_00 0.0
#define UV_03 0.33333333333
#define UV_05 0.5
#define UV_06 0.66666666666
#define UV_10 1.0

shared static const float4 WaterSourceShapeUVOffsets[13] =
{
    // 2x2 size
    float4(UV_00, UV_05, UV_00, UV_05),
    float4(UV_00, UV_05, UV_05, UV_10),
    float4(UV_05, UV_10, UV_00, UV_05),
    float4(UV_05, UV_10, UV_05, UV_10),
    // 3x3 size
    float4(UV_00, UV_03, UV_00, UV_03),
    float4(UV_00, UV_03, UV_03, UV_06),
    float4(UV_00, UV_03, UV_06, UV_10),
    float4(UV_03, UV_06, UV_00, UV_03),
    float4(UV_03, UV_06, UV_03, UV_06),
    float4(UV_03, UV_06, UV_06, UV_10),
    float4(UV_06, UV_10, UV_00, UV_03),
    float4(UV_06, UV_10, UV_03, UV_06),
    float4(UV_06, UV_10, UV_06, UV_10),

};

void GetWaterSourceShapeUVAndMask_float(float index, out float2 uOffsetRange, out float2 vOffsetRange, out float mask)
{
    uOffsetRange = float2(0, 0);
    vOffsetRange = float2(0, 0);
    mask = 0;
    if (index > 0)
    {
        int indexAsByte = (255 * index) - 1;
        uOffsetRange = WaterSourceShapeUVOffsets[indexAsByte].xy;
        vOffsetRange = WaterSourceShapeUVOffsets[indexAsByte].zw;
        mask = 1;
    }
        
}