// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>A buffer that is used to pass arguments at the GPU side. The usage can be very different.</summary>
/// <typeparam name="T">type of the elements.</typeparam>
public sealed class IndirectBuffer<T> : BaseBuffer where T : struct {

  /// <summary>Array of values this buffer is bound to.</summary>
  /// <remarks>The data can be read and written, but re-allocation and deletion are not allowed.</remarks>
  public readonly T[] Values;

  /// <inheritdoc/>
  protected override Array ValuesArray => Values;

  /// <summary>Creates a buffer and binds it to an internal array.</summary>
  /// <param name="name">The name of the buffer, which is only used for the purpose of logging.</param>
  /// <param name="count">
  /// The number of items in the buffer. The values array will be created and hosted internally. For the best
  /// performance, keep the total buffer size of multiple of 16 bytes.
  /// </param>
  /// <seealso cref="Values"/>
  public IndirectBuffer(string name, int count) {
    Name = name;
    Values = new T[count];
    Buffer = new ComputeBuffer(count, Marshal.SizeOf(typeof(T)), ComputeBufferType.IndirectArguments);
  }
}

}
