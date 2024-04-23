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

  /// <summary>Sets this buffer to an incomplete state.</summary>
  /// <remarks>
  /// The buffer cannot be trusted in this state. For inout buffers this means data needs tio be uploaded to GPU. For
  /// the output buffer it means the data needs to be downloaded. 
  /// </remarks>
  public void MarkIncomplete();

  /// <summary>Gets data from GPU.</summary>
  /// <remarks>The buffer implementation specifies where the data is fetched and how it can be accessed.</remarks>
  public void PullFromGpu();

  /// <summary>Sends data to GPU.</summary>
  /// <remarks>The buffer implementation specifies where the data is copied from and how to modify it.</remarks>
  public void PushToGpu();

  /// <summary>Releases all resources and destroys the buffer.</summary>
  public void Dispose();
}

}
