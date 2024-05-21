using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TestShaderBehavior : MonoBehaviour {
  [SerializeField] ComputeShader _shader;
  [SerializeField] ComputeShader _shader2;

  // Start is called before the first frame update
  void Start() {
  }

  // Update is called once per frame
  void Update() {
  }

  void OnGUI() {
    if (GUI.Button(new Rect(0, 0, 100, 50), "Run 1")) {
      Debug.LogWarning("CLICK 1");
      TestShader();
    }
    if (GUI.Button(new Rect(0, 50, 100, 50), "Run 2")) {
      Debug.LogWarning("CLICK 2");
      TestShader2();
    }
  }
  
  void TestShader() {
    var shader = _shader;
    //FIXME get UpdateContaminationsFromCandidates
    //FIXME try chaining them via canidates buffer internally
    var kernelIndex = shader.FindKernel("CalculateContaminationCandidates");
    shader.GetKernelThreadGroupSizes(kernelIndex, out var xSize, out var ySize, out var zSize);
    Debug.LogWarningFormat("*** Loaded shader: kernel={0}, group=({1}, {2}, {3})", shader, xSize, ySize, zSize);

    //var size = new Vector2Int(6, 6);
    //var size = new Vector2Int(1026, 1026);
    var size = new Vector2Int(1026, 1026);
    var bufferSize = size.x * size.y;
    var barriers = new uint[bufferSize];
    var candidates = new float[bufferSize];

    // uint maxWidth;
    // uint maxHeight;
    // float ContaminationSpreadingRate;
    // float ContaminationDecayRate;
    // float MinimumWaterContamination;
    // float ContaminationScaler;
    // float VerticalCostModifier;
    // float RegularSpreadCost;
    // float DiagonalSpreadCost;
    shader.SetInt("maxWidth", size.x);
    shader.SetInt("maxHeight", size.y);

    var intArray = new uint[bufferSize];
    var floatArray = new float[bufferSize];

    var woCandidatesBuffer = new ComputeBuffer(bufferSize, sizeof(float));
    var roLastTickContaminationCandidatesBuffer = new ComputeBuffer(bufferSize, sizeof(float));
    var roContaminationBuffer = new ComputeBuffer(bufferSize, sizeof(float));
    var roUnsafeCellHeightBuffer = new ComputeBuffer(bufferSize, sizeof(float));
    var roContaminationBarriersBuffer = new ComputeBuffer(bufferSize, sizeof(uint));
    var roAboveMoistureBarriersBuffer = new ComputeBuffer(bufferSize, sizeof(uint));
    var roCeiledWaterHeightBuffer = new ComputeBuffer(bufferSize, sizeof(uint));

    // StructuredBuffer<float> LastTickContaminationCandidates;
    // StructuredBuffer<float> Contamination;
    // StructuredBuffer<float> UnsafeCellHeight;
    // StructuredBuffer<uint> ContaminationBarriers;  // FIXME: make it boolean
    // StructuredBuffer<uint> AboveMoistureBarriers;  // FIXME: make it boolean
    // StructuredBuffer<uint> CeiledWaterHeight;
    shader.SetBuffer(kernelIndex, "LastTickContaminationCandidates", roLastTickContaminationCandidatesBuffer);
    shader.SetBuffer(kernelIndex, "ContaminationCandidates", roContaminationBuffer);
    shader.SetBuffer(kernelIndex, "Contamination", roContaminationBuffer);
    shader.SetBuffer(kernelIndex, "UnsafeCellHeight", roUnsafeCellHeightBuffer);
    shader.SetBuffer(kernelIndex, "ContaminationBarriers", roContaminationBarriersBuffer);
    shader.SetBuffer(kernelIndex, "AboveMoistureBarriers", roAboveMoistureBarriersBuffer);
    shader.SetBuffer(kernelIndex, "CeiledWaterHeight", roCeiledWaterHeightBuffer);

    var processingTime = Stopwatch.StartNew();
    roLastTickContaminationCandidatesBuffer.SetData(floatArray);
    roContaminationBuffer.SetData(floatArray);
    roUnsafeCellHeightBuffer.SetData(floatArray);
    roContaminationBarriersBuffer.SetData(intArray);
    roAboveMoistureBarriersBuffer.SetData(intArray);
    roCeiledWaterHeightBuffer.SetData(intArray);
    processingTime.Stop();

    var shaderTime = Stopwatch.StartNew();
    shader.Dispatch(kernelIndex, Mathf.CeilToInt((float)size.x / xSize), Mathf.CeilToInt((float)size.y / ySize), 1);
    shaderTime.Stop();
    processingTime.Start();
    woCandidatesBuffer.GetData(candidates);
    processingTime.Stop();

    Debug.LogWarningFormat("Processing cost: {0} ticks ({1:0.###} ms)",
                           processingTime.ElapsedTicks, 1000.0 * processingTime.ElapsedTicks / Stopwatch.Frequency);
    Debug.LogWarningFormat("Shader cost: {0} ticks ({1:0.###} ms)",
                           shaderTime.ElapsedTicks, 1000.0 * shaderTime.ElapsedTicks / Stopwatch.Frequency);

    // for (var i = 0; i < candidates.Length; i++) {
    //   Debug.LogWarningFormat("*** sample #{0}: {1}", i, candidates[i]);
    // }
    // Debug.LogWarningFormat("*** done!");
    
    woCandidatesBuffer.Release();
    roLastTickContaminationCandidatesBuffer.Release();
    roContaminationBuffer.Release();
    roUnsafeCellHeightBuffer.Release();
    roContaminationBarriersBuffer.Release();
    roAboveMoistureBarriersBuffer.Release();
    roCeiledWaterHeightBuffer.Release();
    //
    // var contaminations = new float[bufferSize];
    // var contaminationLevels = new float[bufferSize];
    // ShaderPipeline pipeline = ShaderPipeline.newBuilder(_shader)
    //     .withSyncBuffer("sync")
    //     .bindConstant("maxWidth", size.x)
    //     .bindConstant("maxHeight", size.y)
    //     .bindCinstant("ContaminationSpreadingRate", 1f)
    //     .bindInput("Contamination", contaminations)
    //     //More inputs
    //     .bindOutput("ContaminationLevels", bufferSize, ref contaminationLevels)
    //     .excuteKernel("SavePreviousState", bufferSize)
    //     .thenExcuteKernel("CalculateContaminationCandidates", bufferSize)
    //     .thenExcuteKernel("UpdateContaminationsFromCandidates", bufferSize)
    //     .build();
    // pipeline.RunAndWait();
    // OR: from Update() => pipeline.Run() / eventually => pipeline.Wait();
  }

  void TestShader2() {
    var bufferSize = 10000;
    var size = 8;
    var buffer1 = new ComputeBuffer(bufferSize, sizeof(int));
    var buffer2 = new ComputeBuffer(bufferSize, sizeof(int));
    var buffer3 = new ComputeBuffer(bufferSize, sizeof(int));
    var buffer4 = new ComputeBuffer(bufferSize, sizeof(int));
    _shader2.SetBuffer(0, "Buffer1", buffer1);

    _shader2.SetBuffer(1, "Buffer1", buffer1);
    _shader2.SetBuffer(1, "Buffer2", buffer2);

    _shader2.SetBuffer(2, "Buffer2", buffer2);
    _shader2.SetBuffer(2, "Buffer3", buffer3);

    //_shader2.SetBuffer(3, "Buffer3", buffer3);
    _shader2.SetBuffer(3, "Buffer4", buffer4);

    _shader2.Dispatch(0, Mathf.CeilToInt((float)bufferSize / size), 1, 1);
    _shader2.Dispatch(1, Mathf.CeilToInt((float)bufferSize / size), 1, 1);
    _shader2.Dispatch(2, Mathf.CeilToInt((float)bufferSize / size), 1, 1);
    _shader2.Dispatch(3, Mathf.CeilToInt((float)bufferSize / size), 1, 1);

    // for (var index = 0; index < result4.Length; index++) {
    //   var result = result4[index];
    //   Debug.LogWarningFormat("*** result4: #{0}={1}", index, result);
    // }
    // var result4 = new int[bufferSize];
    // buffer4.GetData(result4);
    Debug.LogWarningFormat("*** result4, all==   1: {0}", CheckAllEqual(buffer1, bufferSize, 1));
    Debug.LogWarningFormat("*** result1, all==  11: {0}", CheckAllEqual(buffer2, bufferSize, 11));
    Debug.LogWarningFormat("*** result2, all== 111: {0}", CheckAllEqual(buffer3, bufferSize, 111));
    Debug.LogWarningFormat("*** result3, all==1111: {0}", CheckAllEqual(buffer4, bufferSize, 1111));

    buffer1.Release();
    buffer2.Release();
    buffer3.Release();
    buffer4.Release();
  }

  bool CheckAllEqual(ComputeBuffer buffer, int size, int test) {
    var result = new int[size];
    buffer.GetData(result);
    return result.All(x => x == test);
  }
}
