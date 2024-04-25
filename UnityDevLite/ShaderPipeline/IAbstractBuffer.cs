// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>Abstract interface for the compute buffers in shader pipeline.</summary>
/// <remarks>
/// The client code can create instances of the buffers that need special handling. Otherwise, let the pipeline creating
/// and managing them.
/// </remarks>
public interface IAbstractBuffer {
  /// <summary>Name of the buffer as used in the shader.</summary>
  public string Name { get; }

  /// <summary>The underlying <see cref="ComputeBuffer"/> object.</summary>
  public ComputeBuffer Buffer { get; }

  /// <summary>Prepares the buffer for the usage.</summary>
  /// <remarks>The current data of the buffer should be invalided.</remarks>
  /// <param name="executionLog">
  /// If not <c>null</c>, then the buffer should record key actions being performed on the buffer. This information is
  /// used to produce execution plans.
  /// </param>
  /// FIXME docs
  public void Initialize(ExecutionLog executionLog);

  /// <summary>Gets data from GPU.</summary>
  /// <remarks>The buffer implementation specifies where the data is fetched and how it can be accessed.</remarks>
  /// <param name="executionLog">
  /// If not <c>null</c>, then the buffer should record key actions being performed on the buffer. This information is
  /// used to produce execution plans.
  /// </param>
  public void PullFromGpu(ExecutionLog executionLog);

  /// <summary>Sends data to GPU.</summary>
  /// <remarks>The buffer implementation specifies where the data is copied from and how to modify it.</remarks>
  /// <param name="executionLog">
  /// If not <c>null</c>, then the buffer should record key actions being performed on the buffer. This information is
  /// used to produce execution plans.
  /// </param>
  public void PushToGpu(ExecutionLog executionLog);

  /// <summary>Releases all resources and destroys the buffer.</summary>
  public void Dispose();
}

}
