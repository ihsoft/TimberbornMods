// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>Container for a buffer that is not used for data transfer between CPu and GPU.</summary>
/// <remarks>Such buffers are usually internal to the shader and used by kernels to share data.</remarks>
public sealed class IntermediateBuffer : BaseBuffer {

  /// <inheritdoc/>
  protected override Array ValuesArray => null;

  /// <inheritdoc/>
  public override void Initialize(ExecutionLog executionLog) {
    throw new InvalidOperationException("Unsupported in IntermediateBuffer");
  }

  /// <inheritdoc/>
  public override void PushToGpu(ExecutionLog executionLog) {
    throw new InvalidOperationException("Unsupported in IntermediateBuffer");
  }

  /// <inheritdoc/>
  public override void PullFromGpu(ExecutionLog executionLog) {
    throw new InvalidOperationException("Unsupported in IntermediateBuffer");
  }

  /// <summary>Creates a buffer that is not bound to any dara source.</summary>
  /// <param name="name">The name of the buffer as specified in the shader.</param>
  /// <param name="stride">Size type of an element.</param>
  /// <param name="count">The size of the buffer.</param>
  public IntermediateBuffer(string name, int stride, int count) {
    Name = name;
    Buffer = new ComputeBuffer(count, stride);
  }
}

}
