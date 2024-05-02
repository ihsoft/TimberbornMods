// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>Buffer with variable size that can grow. It can do both: push and pull data.</summary>
/// <remarks>Use this buffer when you need to collect a variable number of elements from the kernel.</remarks>
/// <typeparam name="T">
/// The element type of the underlying array. It determines the buffer's stride size. The performance of the shader can
/// be a bit better if the size of <typeparamref name="T"/> is a multiple of 16 bytes.
/// </typeparam>
public sealed class AppendBuffer<T> : BaseBuffer where T : struct {

  readonly IndirectBuffer<int> _argsBuffer = new("_IndirectArguments", 4);  // Keep the length of multiple of 16.

  /// <summary>Array of values this buffer is bound to.</summary>
  /// <remarks>The data can be read and written, but re-allocation and deletion are not allowed.</remarks>
  public readonly T[] Values;

  /// <inheritdoc/>
  protected override Array ValuesArray => Values;

  /// <summary>Actual data size. It's only contain relevant value after <see cref="PullFromGpu"/>.</summary>
  /// <seealso cref="Values"/>
  public int DataLength => _argsBuffer.Values[0];

  /// <inheritdoc/>
  public override void Initialize(ExecutionLog executionLog) {
    if (executionLog != null) {
      executionLog.Records.Add($"Reset append buffer '{Name}'");
    } else {
      _argsBuffer.Values[0] = 0;
      Buffer.SetCounterValue(0);
    }
  }

  /// <inheritdoc/>
  public override void PullFromGpu(ExecutionLog executionLog) {
    base.PullFromGpu(executionLog);
    if (executionLog == null) {
      ComputeBuffer.CopyCount(Buffer, _argsBuffer.Buffer, 0);
    }
    _argsBuffer.PullFromGpu(executionLog);
  }

  /// <inheritdoc/>
  public override void Dispose() {
    base.Dispose();
    _argsBuffer.Dispose();
  }

  /// <summary>Creates a buffer which can grow from zero up to the maximum size.</summary>
  /// <remarks>
  /// This buffer is emptied before running the kernel. On kernel completion, it will hold the data that was added by
  /// that kernel. This buffer is not suitable for passing data between the kernels as it's always reset before another
  /// kernel starts.
  /// </remarks>
  /// <param name="name">The name of the buffer as specified in the shader.</param>
  /// <param name="values">
  /// The array to bind the data to. The size of the array will determine the maximum available capacity of this buffer.
  /// </param>
  /// <exception cref="ArgumentException">if <paramref name="values"/> is null.</exception>
  public AppendBuffer(string name, T[] values) {
    Name = name;
    Values = values ?? throw new ArgumentException("Array must exist", nameof(values));
    Buffer = new ComputeBuffer(values.Length, Marshal.SizeOf<T>(), ComputeBufferType.Append);
  }
}

}
