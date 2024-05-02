// UnityDev Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Timberborn.Common;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>Helper wrapper to sequentially run kernels in a shader.</summary>
/// <remarks>
/// <p>
/// This wrapper allows declaring the execution order via a builder. Then, will automatically handle data preparation
/// and stages synchronization.
/// </p>
/// <p>
/// The concept assumes that you first set constants, then define buffers and, eventually, specify a sequence of kernel
/// dispatches.
/// <list type="bullet">
/// <item>Constants are values that are applied to the shader at the moment of building the pipeline.</item>
/// <item>
/// Buffers are used to exchange data between kernels and GPU/CPU. every buffer in the shader must have a declaration in
/// the pipeline. Even if the buffer is not transferred between CPU and GPU.
/// </item>
/// <item>
/// Kernel dispatch is the actual handler of the work. You need to specify which buffers are needed by the kernel and
/// how are they used: fetch from CPU, get from other kernel, pass to other kernel, or pass to CPU.
/// </item>
/// </list>
/// These calls have no mandatory order of calling and you can mix them to achieve a logical grouping. Just keep in mind
/// that all constants are applied only once at the pipeline setup. And buffers need to be known before referring them
/// in kernel dispatch definition.
/// </p>
/// <p>
/// The pipeline does its best to optimize the time spend for data preparation and the actual kernel running. The input
/// data is not set to the buffers immediately, it happens only when a kernel that needs it is being dispatched. Thus,
/// once the input data is prepared, don't modify it until the pipeline has finished.
/// </p>
/// <example>
/// Here is an example of calling three kernels from a shader, each of which provides output for the child down below.
/// Also, each stage gets extra data source that is provided by CPU.
/// <p>
/// <code><![CDATA[
///     var simulationSpeed = 2f;
///     var flatWorkSize = new Vector3Int(100, 1, 1);
///     var cpuData1 = new float[flatWorkSize.x];
///     var cpuData2 = new float[flatWorkSize.x];
///     var cpuData3 = new float[flatWorkSize.x];
///     var twoDWorkSize = new Vector3Int(256, 256, 1);
///     var result = new float[twoDWorkSize.x * twoDWorkSize.y];
///     
///     var pipeline = ShaderPipeline.NewBuilder()
///       // Constants.
///       .WithConstantValue("Scale", 0.1f)
///       .WithConstantValue("Speed", simulationSpeed) // It will be captured only once!
///       // Buffers.
///       .WithInputBuffer("dataFromCpu1", cpuData1)
///       .WithInputBuffer("dataFromCpu2", cpuData2)
///       .WithInputBuffer("dataFromCpu2", cpuData3)
///       .WithIntermediateBuffer("outputForKernel-1", typeof(float), 1000) // Not bound to flatArraySize
///       .WithIntermediateBuffer("outputForKernel-2", typeof(uint), 500) // Not bound to flatArraySize
///       .WithOutputBuffer("Result", result)
///       // Kernels.
///       .DispatchKernel("stage0", flatWorkSize, "s:dataFromCpu1", "o:outputForKernel-1")
///       .DispatchKernel("stage1", flatWorkSize, "s:dataFromCpu2", "i:outputForKernel-1", "o:outputForKernel-2")
///       .DispatchKernel("stage2", twoDWorkSize, "s:dataFromCpu2", "i:outputForKernel-2", "r:Result")
///       // Done!
///       .Build();
///     
///       // Populate cpuData1, cpuData2, and cpuData3.
///       SetInputData();  
///       pipeline.RunBlocking();
///       // The result array is now ready!
///       foreach (var item in result) {
///         // ...
///       }
/// ]]></code>
/// </p>
/// </example>
/// </remarks>
/// <seealso cref="SimpleBuffer{T}"/>
/// <seealso cref="AppendBuffer{T}"/>
/// <seealso cref="IntermediateBuffer"/>
public sealed class ShaderPipeline {

  #region API
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable UnusedAutoPropertyAccessor.Global

  /// <summary>Shader of this pipeline.</summary>
  public ComputeShader Shader { get; }

  /// <summary>Time, spent for running last <see cref="RunBlocking"/>.</summary>
  public TimeSpan LastRunDuration { get; private set; }

  /// <summary>Starts builder for a new pipeline</summary>
  /// <remarks>
  /// Building is expensive step, but it usually needs to be done only once. Once pipeline created, just call
  /// <see cref="RunBlocking"/> in every frame or physics update.</remarks>
  /// <param name="shader">
  /// The shader to make pipeline for. Do not re-use shaders! Each shader must be owned by exactly one pipeline.
  /// </param>
  public static Builder NewBuilder(ComputeShader shader) {
    return new Builder(shader);
  }

  /// <summary>Runs kernels and block until all the results are ready.</summary>
  /// <remarks>
  /// Ths method can be called as many times as needed. All source buffers will be updated and the result buffers
  /// flushed.
  /// </remarks>
  public void RunBlocking() {
    var stopwatch = Stopwatch.StartNew();
    _executionLog?.Records.AddRange(_parametersLog);

    _executionLog?.RecordPushDataToGpu(_allSources.Count);
    for (var i = _allSources.Count - 1; i >= 0; i--) {
      _allSources[i].PushToGpu(_executionLog);
    }

    var queueSize = _dispatchQueue.Count;
    for (var index = 0; index < queueSize; index++) {
      var kernel = _dispatchQueue[index];
      RecordKernelStart(_executionLog, kernel);
      for (var i = kernel.Sources.Count - 1; i >= 0; i--) {
        var buffer = kernel.Sources[i];
        _executionLog?.RecordSetupBuffer("source", buffer);
        Shader.SetBuffer(kernel.Index, buffer.Name, buffer.Buffer);
      }
      for (var i = kernel.Inputs.Count - 1; i >= 0; i--) {
        var buffer = kernel.Inputs[i];
        _executionLog?.RecordSetupBuffer("input", buffer);
        Shader.SetBuffer(kernel.Index, buffer.Name, buffer.Buffer);
      }
      for (var i = kernel.Outputs.Count - 1; i >= 0; i--) {
        var buffer = kernel.Outputs[i];
        _executionLog?.RecordSetupBuffer("output", buffer);
        Shader.SetBuffer(kernel.Index, buffer.Name, buffer.Buffer);
      }
      for (var i = kernel.Results.Count - 1; i >= 0; i--) {
        var buffer = kernel.Results[i];
        _executionLog?.RecordSetupBuffer("result", buffer);
        Shader.SetBuffer(kernel.Index, buffer.Name, buffer.Buffer);
        kernel.Results[i].Initialize(_executionLog);
      }
      var executionSize = GetExecutionSize(kernel);
      if (_executionLog != null) {
        RecordKernelDispatch(_executionLog, kernel, executionSize);
      } else {
        Shader.Dispatch(kernel.Index, executionSize.x, executionSize.y, executionSize.z);
      }
      RecordKernelEnd(_executionLog, kernel);
    }

    _executionLog?.RecordPullDataFromGpu(_allResults.Count);
    for (var index = _allResults.Count - 1; index >= 0; index--) {
      _allResults[index].PullFromGpu(_executionLog);
    }

    stopwatch.Stop();
    LastRunDuration = stopwatch.Elapsed;
  }

  /// <summary>Returns a trace of what the pipeline would execute.</summary>
  /// <remarks>No actual changes to the buffers or kernels are made.</remarks>
  public List<string> GetExecutionPlan() {
    _executionLog = new ExecutionLog();
    RunBlocking();
    var res = _executionLog.Records;
    _executionLog = null;
    return res;
  }

  /// <summary>Destroys shader and releases all resources.</summary>
  public void Dispose() {
    foreach (var buffer in _allBuffers) {
      buffer.Dispose();
    }
    UnityEngine.Object.DestroyImmediate(Shader);
  }

  // ReSharper restore UnusedAutoPropertyAccessor.Global
  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  /// <summary>Builder to compose a pipeline.</summary>
  public sealed class Builder {

    #region API

    /// <summary>Binds a constant value to the shader.</summary>
    /// <remarks>The value is captured at the moment of the method call and never updated afterwards.</remarks>
    public Builder WithConstantValue(string name, float value) {
      AddNewConstant(name, value);
      _shader.SetFloat(name, value);
      return this;
    }

    /// <summary>Binds a constant value to the shader.</summary>
    /// <remarks>The value is captured at the moment of the method call and never updated afterwards.</remarks>
    public Builder WithConstantValue(string name, int value) {
      AddNewConstant(name, value);
      _shader.SetInt(name, value);
      return this;
    }

    /// <summary>Binds a constant value to the shader.</summary>
    /// <remarks>The value is captured at the moment of the method call and never updated afterwards.</remarks>
    public Builder WithConstantValue(string name, bool value) {
      AddNewConstant(name, value);
      _shader.SetBool(name, value);
      return this;
    }

    /// <summary>Binds a read-only buffer that is used to provide kernel with data.</summary>
    /// <param name="name">The name as specified in the shader.</param>
    /// <param name="values">The array to copy data from.</param>
    public Builder WithInputBuffer<T>(string name, T[] values) where T : struct {
      AddNewBufferName(name);
      _inputBuffers.Add(name, new SimpleBuffer<T>(name, values));
      return this;
    }

    /// <summary>Binds a buffer that is used internally by the kernels or is handled outside of the pipeline.</summary>
    /// <remarks>It's a syntax sugar for <see cref="WithIntermediateBuffer(BaseBuffer)"/> method.</remarks>
    /// <param name="name">The name as specified in the shader.</param>
    /// <param name="stride">Size of the item in the buffer.</param>
    /// <param name="count">Number of elements in the buffer.</param>
    public Builder WithIntermediateBuffer(string name, int stride, int count) {
      AddNewBufferName(name);
      _intermediateBuffers.Add(name, new IntermediateBuffer(name, stride, count));
      return this;
    }

    /// <summary>Binds a buffer that is used internally by the kernels or is handled outside of the pipeline.</summary>
    /// <remarks>
    /// The pipeline will only verify the completion state of such buffers to sync between the kernels. If needed, the
    /// data still can be transferred, but this should be done by the client's code.
    /// </remarks>
    /// <param name="buffer">The buffer to use for state completion checking.</param>
    public Builder WithIntermediateBuffer(BaseBuffer buffer) {
      AddNewBufferName(buffer.Name);
      _intermediateBuffers.Add(buffer.Name, buffer);
      return this;
    }

    /// <summary>Binds a read-write buffer that is used to get data from the kernel.</summary>
    /// <remarks>It's a syntax sugar for <see cref="WithOutputBuffer"/> method.</remarks>
    /// <param name="name">The name as specified in the shader.</param>
    /// <param name="values">The array to copy data into.</param>
    public Builder WithOutputBuffer<T>(string name, T[] values) where T : struct {
      AddNewBufferName(name);
      _outputBuffers.Add(name, new SimpleBuffer<T>(name, values));
      return this;
    }

    /// <summary>Binds a read-write append buffer that is used to get data from the kernel.</summary>
    /// <remarks>The <paramref name="buffer"/> can be accessed and manipulated outside of the pipeline.</remarks>
    /// <param name="buffer">The buffer to add.</param>
    /// <seealso cref="SimpleBuffer{T}"/>
    /// <seealso cref="AppendBuffer{T}"/>
    public Builder WithOutputBuffer(BaseBuffer buffer) {
      AddNewBufferName(buffer.Name);
      _outputBuffers.Add(buffer.Name, buffer);
      return this;
    }

    void AddNewConstant(string name, object value) {
      if (_constants.Keys.Contains(name)) {
        throw new ArgumentException($"Constant '{name}' is already defined");
      }
      _constants.Add(name, value);
    }

    void AddNewBufferName(string name) {
      if (_allBufferNames.Contains(name)) {
        throw new ArgumentException($"Buffer '{name}' is already defined", nameof(name));
      }
      _allBufferNames.Add(name);
    }

    /// <summary>Defines a kernel dispatch.</summary>
    /// <remarks>The pipeline will execute kernels in the kernels in the order of which they were declared.</remarks>
    /// <param name="kernelName">The name of the kernel to execute.</param>
    /// <param name="dataSize">Data size in the input.</param>
    /// <param name="buffersDefs">
    /// Definitions of the input/output buffers. All buffers that the kernel depends on must be defined for the proper
    /// execution. Each name should has a prefix to indicate how the buffers is used by the kernel:
    /// <list>
    /// <item>"s:" - source data, it will be pushed to GPU via an input buffer.</item>
    /// <item>"i:" - inout data, that was generated by another kernel.</item>
    /// <item>"o:" - output data, that is generated by the kernel, but is not consumed by CPU.</item>
    /// <item>"r:" - result data, it will be consumed by CPU.</item>
    /// </list>
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public Builder DispatchKernel(string kernelName, Vector3Int dataSize, params string[] buffersDefs) {
      if (!_shader.HasKernel(kernelName)) {
        throw new ArgumentException($"Unknown kernel name '{kernelName}'");
      }
      var kernel = new KernelDecl {
          Name = kernelName,
          Index = _shader.FindKernel(kernelName),
          DataSize = dataSize,
      };
      _shader.GetKernelThreadGroupSizes(kernel.Index, out var xSize, out var ySize, out var zSize);
      kernel.GroupSize = new Vector3Int(Convert.ToInt32(xSize), Convert.ToInt32(ySize), Convert.ToInt32(zSize));
      foreach (var bufferDef in buffersDefs) {
        var prefix = bufferDef.Substring(0, 2);
        var name = bufferDef.Substring(2);
        BaseBuffer buffer;
        if (prefix == "s:") {
          buffer = GetInDependency(name);
          kernel.Sources.Add(buffer);
        } else if (prefix == "i:") {
          buffer = GetAnyBuffer(name);
          kernel.Inputs.Add(buffer);
        } else if (prefix == "o:") {
          buffer = GetIntermediateBuffer(name);
          kernel.Outputs.Add(buffer);
        } else if (prefix == "r:") {
          buffer = GetOutputBufferDecl(name);
          kernel.Results.Add(buffer);
        } else {
          throw new ArgumentException($"Cannot parse buffer declaration: {bufferDef}");
        }
        _usedBuffers.Add(name);
      }
      _dispatchQueue.Add(kernel);
      return this;
    }

    /// <summary>Creates pipeline.</summary>
    /// <exception cref="InvalidDataException">if not all required data was provided.</exception>
    public ShaderPipeline Build() {
      foreach (var bufferName in _allBufferNames) {
        if (!_usedBuffers.Contains(bufferName)) {
          throw new InvalidDataException($"Unused buffer: {bufferName}");
        }
      }
      if (_outputBuffers.Count == 0) {
        throw new InvalidDataException($"At least one output buffer must exist");
      }
      return new ShaderPipeline(_shader, _dispatchQueue, _constants);
    }

    #endregion

    #region Implementation

    readonly ComputeShader _shader;
    readonly Dictionary<string, object> _constants = new();
    readonly HashSet<string> _allBufferNames = new();
    readonly Dictionary<string, BaseBuffer> _inputBuffers = new();
    readonly Dictionary<string, BaseBuffer> _intermediateBuffers = new();
    readonly Dictionary<string, BaseBuffer> _outputBuffers = new();
    readonly List<KernelDecl> _dispatchQueue = new();
    readonly HashSet<string> _usedBuffers = new();

    internal Builder(ComputeShader shader) {
      _shader = shader;
    }

    BaseBuffer GetIntermediateBuffer(string name) {
      if (!_intermediateBuffers.TryGetValue(name, out var buffer)) {
        throw new ArgumentException($"Intermediate buffer with name '{name}' was not declared");
      }
      return buffer;
    }

    BaseBuffer GetInDependency(string name) {
      if (_inputBuffers.TryGetValue(name, out var inputBuffer)) {
        return inputBuffer;
      }
      if (_outputBuffers.TryGetValue(name, out var outputBuffer)) {
        return outputBuffer;
      }
      throw new ArgumentException($"Input/output buffer with name '{name}' was not declared");
    }

    BaseBuffer GetOutputBufferDecl(string name) {
      if (!_outputBuffers.TryGetValue(name, out var bufferDecl)) {
        throw new ArgumentException($"Output buffer with name '{name}' was not declared");
      }
      return bufferDecl;
    }

    BaseBuffer GetAnyBuffer(string name) {
      if (_inputBuffers.TryGetValue(name, out var inputBuffer)) {
        return inputBuffer;
      }
      if (_outputBuffers.TryGetValue(name, out var outputBuffer)) {
        return outputBuffer;
      }
      if (_intermediateBuffers.TryGetValue(name, out var intermediateBuffer)) {
        return intermediateBuffer;
      }
      throw new ArgumentException($"Unknown buffer name: {name}");
    }

    #endregion
  }

  #region Implementation

  sealed class KernelDecl {
    public string Name;
    public int Index;
    public Vector3Int DataSize;
    public Vector3Int GroupSize;
    public readonly List<BaseBuffer> Sources = new();
    public readonly List<BaseBuffer> Inputs = new();
    public readonly List<BaseBuffer> Outputs = new();
    public readonly List<BaseBuffer> Results = new();
  }

  readonly List<KernelDecl> _dispatchQueue;
  readonly List<BaseBuffer> _allBuffers;
  readonly List<BaseBuffer> _allSources;
  readonly List<BaseBuffer> _allResults;
  readonly List<string> _parametersLog = new();
  ExecutionLog _executionLog;

  ShaderPipeline(ComputeShader shader, List<KernelDecl> dispatchQueue, Dictionary<string, object> parameters) {
    Shader = shader;
    _dispatchQueue = dispatchQueue;
    var allBuffers = new HashSet<BaseBuffer>();
    var allSources = new HashSet<BaseBuffer>();
    var allResults = new HashSet<BaseBuffer>();
    foreach (var kernel in _dispatchQueue) {
      allSources.AddRange(kernel.Sources);
      allResults.AddRange(kernel.Results);
      allBuffers.AddRange(kernel.Sources);
      allBuffers.AddRange(kernel.Inputs);
      allBuffers.AddRange(kernel.Outputs);
      allBuffers.AddRange(kernel.Results);
    }
    // Order buffers by name to keep execution log consistent. keep in mind that we iterate them in reverse.  
    _allSources = allSources.OrderByDescending(x => x.Name).ToList();
    _allResults = allResults.OrderByDescending(x => x.Name).ToList();
    _allBuffers = allBuffers.ToList();
    if (parameters.Count > 0) {
      var ordered = parameters.OrderBy(x => x.Key);
      foreach (var pair in ordered) {
        _parametersLog.Add($"Parameter: {pair.Key} = {pair.Value}");
      }
    } else {
      _parametersLog.Add("No parameters in the pipeline");
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static Vector3Int GetExecutionSize(KernelDecl kernel) {
    return new Vector3Int(
        Mathf.CeilToInt((float)kernel.DataSize.x / kernel.GroupSize.x),
        Mathf.CeilToInt((float)kernel.DataSize.y / kernel.GroupSize.y),
        Mathf.CeilToInt((float)kernel.DataSize.z / kernel.GroupSize.z));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void RecordKernelStart(ExecutionLog log, KernelDecl kernel) {
    log?.Records.Add("");
    log?.Records.Add(
        $"Kernel #{kernel.Index} ({kernel.Name}): groupSize={kernel.GroupSize}, dataSize={kernel.DataSize}");
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void RecordKernelEnd(ExecutionLog log, KernelDecl kernel) {
    log?.Records.Add($"Kernel #{kernel.Index} finished");
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void RecordKernelDispatch(ExecutionLog log, KernelDecl kernel, Vector3Int executionSize) {
    log.Records.Add($"Dispatch(size={executionSize})");
  }

  #endregion
}

}
