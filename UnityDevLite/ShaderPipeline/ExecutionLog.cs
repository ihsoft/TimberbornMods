// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.ShaderPipeline {

/// <summary>Helper class that is used to obtain execution logs from the shader pipeline buffers.</summary>
/// <remarks>
/// When this object is passed to the buffer action, the action is expected to add all important steps and information
/// into the log. The idea is to get a clear view of how the pipeline is being executed. The log iis refreshed on every
/// pipeline call.
/// </remarks>
/// <seealso cref="BaseBuffer"/>
/// <seealso cref="ShaderPipeline"/>
public sealed class ExecutionLog {
  /// <summary>The log records of the pipeline execution.</summary>
  /// <remarks>
  /// Any code can add here anything that is useful. Do not try to format data! It's up to the consumer present this
  /// data the right way.
  /// </remarks>
  public readonly List<string> Records = new ();

  /// <summary>Logs a "SetData" operation on the buffer.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RecordBufferSet(BaseBuffer baseBuffer) {
    Records.Add($"SetData on buffer: {GetBufferDecl(baseBuffer)}");
  }
  
  /// <summary>Logs a "GetData" operation on the buffer.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RecordBufferGet(BaseBuffer baseBuffer) {
    Records.Add($"GetData on buffer: {GetBufferDecl(baseBuffer)}");
  }

  /// <summary>Logs a "SetBuffer" operation on the buffer.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RecordSetupBuffer(string group, BaseBuffer baseBuffer) {
    Records.Add($"{group}: SetBuffer '{baseBuffer.Name}' to kernel");
  }

  /// <summary>Records a set of "SetData" operations.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RecordPushDataToGpu(int bufferCount) {
    Records.Add("");
    Records.Add(bufferCount > 0 ? $"Push data to GPU ({bufferCount}):" : "No data to push to GPU");
  }

  /// <summary>Records a set of "GetData" operations.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RecordPullDataFromGpu(int bufferCount) {
    Records.Add("");
    Records.Add(bufferCount > 0 ? $"Pull data from GPU ({bufferCount}):" : "Not data to pull from GPU");
  }

  /// <summary>Describes a buffer.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static string GetBufferDecl(BaseBuffer baseBuffer) {
    var buffer = baseBuffer.Buffer;
    var aligned = buffer.stride % 16 == 0 ? "aligned" : "not aligned";
    var optimal = (buffer.stride * buffer.count) % 16 == 0 ? "size is optimal" : "size is not optimal";
    return $"name={baseBuffer.Name}, stride={buffer.stride}, count={buffer.count}, {aligned}, {optimal}";
  }
}

}
