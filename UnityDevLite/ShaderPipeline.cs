// UnityDev Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.TimberCommons {

/// <summary>Helper wrapper to sequentially run kernels in a shader.</summary>
/// <remarks>
/// This wrapper allows declaring the execution order via a builder. Then, will automatically handle data preparation
/// and stages synchronization.
/// </remarks>
public sealed class ShaderPipeline {

  #region API

  /// <summary>Starts builder for a new pipeline</summary>
  /// <param name="shader">
  /// The shader to make pipeline for. Do not re-use shaders! Each shader must be owned by exactly one pipeline.
  /// </param>
  public static Builder NewBuilder(ComputeShader shader) {
    return new Builder(shader);
  }

  /// <summary>Runs kernels and block until all the results are ready.</summary>
  public void RunBlocking() {
    for (var i = _allSources.Count - 1; i >= 0; i--) {
      _allSources[i].Reset();
    }
    var queueSize = _dispatchQueue.Count;
    for (var index = 0; index < queueSize; index++) {
      var kernel = _dispatchQueue[index];
      for (var i = kernel.Sources.Count - 1; i >= 0; i--) {
        var buffer = kernel.Sources[i];
        buffer.SetData();
      }
      _shader.Dispatch(
          kernel.Index, Mathf.CeilToInt((float)kernel.DataSize.x / kernel.GroupSize.x),
          Mathf.CeilToInt((float)kernel.DataSize.y / kernel.GroupSize.y),
          Mathf.CeilToInt((float)kernel.DataSize.z / kernel.GroupSize.z));
      foreach (var buffer in kernel.Outputs) {
        AsyncGPUReadback.Request(buffer).WaitForCompletion();
      }
      foreach (var buffer in kernel.Results) {
        buffer.GetData();
      }
    }
  }

  /// <summary>Destroys shader and releases all resources.</summary>
  public void Dispose() {
    foreach (var buffer in _allBuffers) {
      buffer.Release();
    }
    UnityEngine.Object.DestroyImmediate(_shader);
  }

  #endregion

  /// <summary>Builder to compose a pipeline.</summary>
  public sealed class Builder {

    #region API

    /// <summary>Binds a constant value to the shader.</summary>
    /// <remarks>The value is captured at the moment of the method call and never updated afterwards.</remarks>
    public Builder WithConstantValue(string name, float value) {
      if (_constants.Contains(name)) {
        throw new ArgumentException($"Constant '{name}' is already defined");
      }
      _constants.Add(name);
      _shader.SetFloat(name, value);
      return this;
    }

    /// <summary>Binds a constant value to the shader.</summary>
    /// <remarks>The value is captured at the moment of the method call and never updated afterwards.</remarks>
    public Builder WithConstantValue(string name, int value) {
      if (_constants.Contains(name)) {
        throw new ArgumentException($"Constant '{name}' is already defined");
      }
      _constants.Add(name);
      _shader.SetInt(name, value);
      return this;
    }

    /// <summary>Binds a constant value to the shader.</summary>
    /// <remarks>The value is captured at the moment of the method call and never updated afterwards.</remarks>
    public Builder WithConstantValue(string name, bool value) {
      if (_constants.Contains(name)) {
        throw new ArgumentException($"Constant '{name}' is already defined");
      }
      _constants.Add(name);
      _shader.SetBool(name, value);
      return this;
    }

    /// <summary>Binds a read-only buffer that is used to provide kernel with data.</summary>
    /// <param name="name">The name as specified in the shader.</param>
    /// <param name="values">The array to copy data from.</param>
    public Builder WithInputBuffer<T>(string name, T[] values) {
      if (values == null) {
        throw new ArgumentException($"Array mut exist for buffer '{name}'", nameof(values));
      }
      if (_allBufferNames.Contains(name)) {
        throw new ArgumentException($"Buffer '{name}' is already defined", nameof(name));
      }
      _allBufferNames.Add(name);
      var buffer = new BufferDecl {
        Name = name,
        Buffer = new ComputeBuffer(values.Length, Marshal.SizeOf(typeof(T))),
        Array = values,
      };
      _inputBuffers.Add(name, buffer);
      return this;
    }

    /// <summary>Declares a buffer that is only used internally by the kernels.</summary>
    /// <remarks>The data in this buffer won't be transferred between CPU and GPU.</remarks>
    /// <param name="name">The name as specified in the shader.</param>
    /// <param name="type">Type of the data in the buffer.</param>
    /// <param name="count">Number of elements in the buffer.</param>
    public Builder WithIntermediateBuffer(string name, Type type, int count) {
      if (_allBufferNames.Contains(name)) {
        throw new ArgumentException($"Buffer '{name}' is already defined", nameof(name));
      }
      _allBufferNames.Add(name);
      _intermediateBuffers.Add(name, new ComputeBuffer(count, Marshal.SizeOf(type)));
      return this;
    }

    /// <summary>Binds a read-write buffer that is used to get data from the kernel.</summary>
    /// <param name="name">The name as specified in the shader.</param>
    /// <param name="values">The array to copy data into.</param>
    /// <param name="initialize">Indicates if the buffer needs to be filled with data before first usage.</param>
    public Builder WithOutputBuffer<T>(string name, T[] values, bool initialize = false) {
      if (values == null) {
        throw new ArgumentException($"Array mut exist for buffer '{name}'", nameof(values));
      }
      if (_allBufferNames.Contains(name)) {
        throw new ArgumentException($"Buffer '{name}' is already defined", nameof(name));
      }
      _allBufferNames.Add(name);
      var buffer = new BufferDecl {
          Name = name,
          Buffer = new ComputeBuffer(values.Length, Marshal.SizeOf(typeof(T))),
          Array = values,
          NeedsInitialization = initialize,
      };
      _outputBuffers.Add(name, buffer);
      return this;
    }

    /// <summary>Defines a kernel dispatch.</summary>
    /// <remarks>The pipeline will execute kernels in the kernels in the order of which they were declared.</remarks>
    /// <param name="kernelName">The name of the kernel to execute.</param>
    /// <param name="dataSize">Data size in the input.</param>
    /// <param name="buffersDefs">
    /// Definitions of teh input/output buffers. All buffers that teh kernel depends on must be defined for the proper
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
          DataSize = dataSize,
          Index = _shader.FindKernel(kernelName)
      };
      _shader.GetKernelThreadGroupSizes(kernel.Index, out var xSize, out var ySize, out var zSize);
      kernel.GroupSize = new Vector3Int(Convert.ToInt32(xSize), Convert.ToInt32(ySize), Convert.ToInt32(zSize));
      foreach (var bufferDef in buffersDefs) {
        var prefix = bufferDef.Substring(0, 2);
        var name = bufferDef.Substring(2);
        ComputeBuffer buffer;
        if (prefix == "s:") {
          var bufferDecl = GetInDependency(name);
          buffer = bufferDecl.Buffer;
          kernel.Sources.Add(bufferDecl);
        } else if (prefix == "i:") {
          buffer = GetAnyBuffer(name);
          kernel.Inputs.Add(buffer);
        } else if (prefix == "o:") {
          buffer = GetIntermediateBuffer(name);
          kernel.Outputs.Add(buffer);
        } else if (prefix == "r:") {
          var bufferDecl = GetOutputBufferDecl(name);
          buffer = bufferDecl.Buffer;
          kernel.Results.Add(bufferDecl);
        } else {
          throw new ArgumentException($"Cannot parse buffer declaration: {bufferDef}");
        }
        _shader.SetBuffer(kernel.Index, name, buffer);
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
      var allBuffers = new List<ComputeBuffer>();
      allBuffers.AddRange(_inputBuffers.Select(x => x.Value.Buffer));
      allBuffers.AddRange(_outputBuffers.Select(x => x.Value.Buffer));
      allBuffers.AddRange(_intermediateBuffers.Values);
      return new ShaderPipeline(_shader, _dispatchQueue, allBuffers);
    }

    #endregion

    #region Implementation

    readonly ComputeShader _shader;
    readonly HashSet<string> _constants = new();
    readonly HashSet<string> _allBufferNames = new();
    readonly Dictionary<string, BufferDecl> _inputBuffers = new();
    readonly Dictionary<string, ComputeBuffer> _intermediateBuffers = new();
    readonly Dictionary<string, BufferDecl> _outputBuffers = new();
    readonly List<KernelDecl> _dispatchQueue = new();
    readonly HashSet<string> _usedBuffers = new();

    internal Builder(ComputeShader shader) {
      _shader = shader;
    }

    ComputeBuffer GetIntermediateBuffer(string name) {
      if (!_intermediateBuffers.TryGetValue(name, out var buffer)) {
        throw new ArgumentException($"Intermediate buffer with name '{name}' was not declared");
      }
      return buffer;
    }

    BufferDecl GetInDependency(string name) {
      if (_inputBuffers.TryGetValue(name, out var inputBuffer)) {
        return inputBuffer;
      }
      if (_outputBuffers.TryGetValue(name, out var outputBuffer)) {
        if (!outputBuffer.NeedsInitialization) {
          throw new ArgumentException($"Output buffer '{name}' must require initialization");
        }
        return outputBuffer;
      }
      throw new ArgumentException($"Input/output buffer with name '{name}' was not declared");
    }

    BufferDecl GetOutputBufferDecl(string name) {
      if (!_outputBuffers.TryGetValue(name, out var bufferDecl)) {
        throw new ArgumentException($"Output buffer with name '{name}' was not declared");
      }
      return bufferDecl;
    }

    ComputeBuffer GetAnyBuffer(string name) {
      if (_inputBuffers.TryGetValue(name, out var inputBuffer)) {
        return inputBuffer.Buffer;
      }
      if (_outputBuffers.TryGetValue(name, out var outputBuffer)) {
        return outputBuffer.Buffer;
      }
      if (_intermediateBuffers.TryGetValue(name, out var intermediateBuffer)) {
        return intermediateBuffer;
      }
      throw new ArgumentException($"Unknown buffer name: {name}");
    }

    #endregion
  }

  #region Implementation

  class BufferDecl {
    public string Name;
    public ComputeBuffer Buffer;
    public Array Array;
    public bool NeedsInitialization;

    bool _isReady;

    public void SetData() {
      if (_isReady) {
        return;
      }
      //FIXME: here use begin/end write.
      Buffer.SetData(Array);
      _isReady = true;
    }

    public void GetData() {
      Buffer.GetData(Array);
    }

    public void Reset() {
      _isReady = false;
    }
  }

  sealed class KernelDecl {
    public int Index;
    public Vector3Int DataSize;
    public Vector3Int GroupSize;
    public readonly List<BufferDecl> Sources = new();
    public readonly List<ComputeBuffer> Inputs = new();
    public readonly List<ComputeBuffer> Outputs = new();
    public readonly List<BufferDecl> Results = new();
  }

  readonly ComputeShader _shader;
  readonly List<KernelDecl> _dispatchQueue;
  readonly List<ComputeBuffer> _allBuffers;
  readonly List<BufferDecl> _allSources = new();

  ShaderPipeline(ComputeShader shader, List<KernelDecl> dispatchQueue, List<ComputeBuffer> allBuffers) {
    _shader = shader;
    _dispatchQueue = dispatchQueue;
    _allBuffers = allBuffers;
    foreach (var kernel in _dispatchQueue) {
      _allSources.AddRange(kernel.Sources);
    }
  }

  #endregion
}

}
