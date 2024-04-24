// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>A buffer that is used to pass arguments at the GPU side. The usage can be very different.</summary>
/// <typeparam name="T">type of the elements.</typeparam>
public sealed class IndirectBuffer<T> : IAbstractBuffer where T : struct {

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
  public void Initialize(ExecutionLog executionLog) {
  }

  /// <inheritdoc/>
  public void PushToGpu(ExecutionLog executionLog) {
    executionLog?.RecordBufferSet(this);
    Buffer.SetData(Values);
  }

  /// <inheritdoc/>
  public void PullFromGpu(ExecutionLog executionLog) {
    executionLog?.RecordBufferGet(this);
    Buffer.GetData(Values);
  }

  /// <inheritdoc/>
  public void Dispose() {
    Buffer.Release();
  }

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  /// <summary>Creates a buffer and binds it to the array.</summary>
  /// <param name="name">The name of the buffer, which is only used for the purpose of logging.</param>
  /// <param name="count">
  /// The number of items in the buffer. The values array will be created and hosted locally. For the best performance,
  /// keep the total buffer size of multiple of 16 bytes.
  /// </param>
  /// <seealso cref="Values"/>
  public IndirectBuffer(string name, int count) {
    Name = name;
    Values = new T[count];
    Buffer = new ComputeBuffer(count, Marshal.SizeOf(typeof(T)), ComputeBufferType.IndirectArguments);
  }
}

}
