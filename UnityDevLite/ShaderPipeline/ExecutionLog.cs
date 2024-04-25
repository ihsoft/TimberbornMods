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
/// <seealso cref="IAbstractBuffer"/>
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
  public void RecordBufferSet(IAbstractBuffer abstractBuffer) {
    Records.Add($"SetData on buffer: {GetBufferDecl(abstractBuffer)}");
  }
  
  /// <summary>Logs a "GetData" operation on the buffer.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void RecordBufferGet(IAbstractBuffer abstractBuffer) {
    Records.Add($"GetData on buffer: {GetBufferDecl(abstractBuffer)}");
  }

  /// <summary>Describes a buffer.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static string GetBufferDecl(IAbstractBuffer abstractBuffer) {
    var buffer = abstractBuffer.Buffer;
    var aligned = buffer.stride % 16 == 0 ? "aligned" : "not aligned";
    var optimal = (buffer.stride * buffer.count) % 16 == 0 ? "size is optimal" : "size is not optimal";
    //FIXME: pull data type 
    return $"name={abstractBuffer.Name}, stride={buffer.stride}, count={buffer.count}, {aligned}, {optimal}";
  }
}

}
