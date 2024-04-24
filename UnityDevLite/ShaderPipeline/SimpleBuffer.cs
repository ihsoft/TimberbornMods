// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>
/// Simple data buffer that can be used for transferring data between CPU and GPU. It can do both: push and pull.
/// </summary>
/// <remarks>Use this buffer when you need transfer flat arrays of data of a constant size.</remarks>
/// <typeparam name="T">The element type of the underlying array. It determines the buffer's stride size.</typeparam>
public sealed class SimpleBuffer<T> : IAbstractBuffer where T : struct {

  bool _isReady;

  #region API
  // ReSharper disable MemberCanBePrivate.Global

  /// <inheritdoc/>
  public string Name { get; }

  /// <inheritdoc/>
  public ComputeBuffer Buffer { get; }

  /// <summary>Array of values this buffer is bound to.</summary>
  /// <remarks>The data can be read and written, but re-allocation and deletion are not allowed.</remarks>
  public readonly T[] Values;

  /// <inheritdoc/>
  public void Initialize() {
    _isReady = false;
  }

  /// <inheritdoc/>
  public void PushToGpu() {
    if (_isReady) {
      return;
    }
    Buffer.SetData(Values);
    _isReady = true;
  }

  /// <inheritdoc/>
  public void PullFromGpu() {
    Buffer.GetData(Values);
  }

  /// <inheritdoc/>
  public void Dispose() {
    Buffer.Release();
  }

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  /// <summary>Creates a buffer and binds it to the array.</summary>
  /// <param name="name">The name of the buffer as specified in the shader.</param>
  /// <param name="values">The array to bind the data to.</param>
  /// <exception cref="ArgumentException">if <paramref name="values"/> is null.</exception>
  public SimpleBuffer(string name, T[] values) {
    Name = name;
    Values = values ?? throw new ArgumentException("Array mut exist", nameof(values));
    Buffer = new ComputeBuffer(values.Length, Marshal.SizeOf(typeof(T)));
  }
}

}
