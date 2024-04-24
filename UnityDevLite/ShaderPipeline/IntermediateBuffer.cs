using System;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>Container for a buffer that is not used for data transfer between CPu and GPU.</summary>
/// <remarks>Such buffers are usually internal to the shader and used by kernels to share data.</remarks>
public sealed class IntermediateBuffer : IAbstractBuffer {

  #region API
  // ReSharper disable MemberCanBePrivate.Global

  /// <inheritdoc/>
  public string Name { get; }

  /// <inheritdoc/>
  public ComputeBuffer Buffer { get; }

  /// <inheritdoc/>
  public void Initialize(ExecutionLog executionLog) {
    throw new InvalidOperationException("Unsupported in IntermediateBuffer");
  }

  /// <inheritdoc/>
  public void PushToGpu(ExecutionLog executionLog) {
    throw new InvalidOperationException("Unsupported in IntermediateBuffer");
  }

  /// <inheritdoc/>
  public void PullFromGpu(ExecutionLog executionLog) {
    throw new InvalidOperationException("Unsupported in IntermediateBuffer");
  }

  /// <inheritdoc/>
  public void Dispose() {
    Buffer.Release();
  }

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  /// <summary>Creates a buffer that is not bound to any dara source.</summary>
  /// <param name="name">The name of the buffer as specified in the shader.</param>
  /// <param name="type">The type of elements. This will define the stride size.</param>
  /// <param name="count">The size of the buffer.</param>
  public IntermediateBuffer(string name, Type type, int count) {
    Name = name;
    Buffer = new ComputeBuffer(count, Marshal.SizeOf(type));
  }
}

}
