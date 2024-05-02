// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>
/// Simple data buffer that can be used for transferring data between CPU and GPU. It can do both: push and pull.
/// </summary>
/// <remarks>Use this buffer when you need transfer flat arrays of data of a constant size.</remarks>
/// <typeparam name="T">
/// The element type of the underlying array. It determines the buffer's stride size. The performance of the shader can
/// be a bit better if the size of <typeparamref name="T"/> is a multiple of 16 bytes.
/// </typeparam>
public sealed class SimpleBuffer<T> : BaseBuffer where T : struct {

  /// <summary>Array of values this buffer is bound to.</summary>
  /// <remarks>The data can be read and written, but re-allocation and deletion are not allowed.</remarks>
  public readonly T[] Values;

  /// <inheritdoc/>
  protected override Array ValuesArray => Values;

  /// <summary>Creates a buffer and binds it to the array.</summary>
  /// <param name="name">The name of the buffer as specified in the shader.</param>
  /// <param name="values">The array to bind the data to.</param>
  /// <exception cref="ArgumentException">if <paramref name="values"/> is null.</exception>
  public SimpleBuffer(string name, T[] values) {
    Name = name;
    Values = values ?? throw new ArgumentException("Array must exist", nameof(values));
    Buffer = new ComputeBuffer(values.Length, Marshal.SizeOf(typeof(T)));
  }
}

}
