// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>Base buffer class for the compute buffers in shader pipeline.</summary>
/// <remarks>
/// The client code can create instances of the buffers that need special handling. Otherwise, let the pipeline creating
/// and managing them via a buffer declaration.
/// </remarks>
public abstract class BaseBuffer {
  /// <summary>Name of the buffer as used in the shader.</summary>
  public string Name { get; protected set; }

  /// <summary>The underlying <see cref="ComputeBuffer"/> object.</summary>
  public ComputeBuffer Buffer { get; protected set; }

  /// <summary>Array reference to the underlying values of the buffer.</summary>
  /// <remarks>It will be used to transfer the data between CPU and GPU.</remarks>
  protected abstract Array ValuesArray { get; } 

  /// <summary>Prepares the buffer for the usage.</summary>
  /// <remarks>The current data of the buffer should be invalided.</remarks>
  /// <param name="executionLog">
  /// If not <c>null</c>, then the buffer should record key actions being performed on the buffer. This information is
  /// used to produce execution plans.
  /// </param>
  public virtual void Initialize(ExecutionLog executionLog) {}

  /// <summary>Gets data from GPU.</summary>
  /// <remarks>The buffer implementation specifies where the data is fetched and how it can be accessed.</remarks>
  /// <param name="executionLog">
  /// If not <c>null</c>, then the buffer should record key actions being performed on the buffer. This information is
  /// used to produce execution plans.
  /// </param>
  public virtual void PullFromGpu(ExecutionLog executionLog) {
    if (executionLog != null) {
      executionLog.RecordBufferGet(this);
    } else {
      Buffer.GetData(ValuesArray);
    }
  }

  /// <summary>Sends data to GPU.</summary>
  /// <remarks>The buffer implementation specifies where the data is copied from and how to modify it.</remarks>
  /// <param name="executionLog">
  /// If not <c>null</c>, then the buffer should record key actions being performed on the buffer. This information is
  /// used to produce execution plans.
  /// </param>
  public virtual void PushToGpu(ExecutionLog executionLog) {
    if (executionLog != null) {
      executionLog.RecordBufferSet(this);
    } else {
      Buffer.SetData(ValuesArray);
    }
  }

  /// <summary>Releases all resources and destroys the buffer.</summary>
  public virtual void Dispose() {
    Buffer.Release();
  }
}

}
