// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class ParserContext {

  #region Input settings 

  /// <summary>
  /// Callback to call when any of the signal sources change their value. If set to null, then no callbacks will be
  /// registered
  /// </summary>
  /// <remarks>This must be set before the parser is called.</remarks>
  public Action OnSignalChanged;

  /// <summary>Host object that will own this expression.</summary>
  /// <remarks>This must be set before the parser is called.</remarks>
  public BaseComponent ScriptHost;

  #endregion

  #region Output data


  /// <summary>All the signal sources that are referenced in the parsed expression.</summary>
  /// <remarks>
  /// If there was <see cref="OnSignalChanged"/> action specified, then the sources must be disposed before cleaning
  /// up the parsed expression.
  /// </remarks>
  public readonly Dictionary<string, ITriggerSource> SignalSources = new();

  /// <summary>On successful parsing, this property will contain the parsed expression.</summary>
  public IExpression ParsedExpression { get; internal set; }

  /// <summary>On parsing error, this property will contain the error message.</summary>
  public string LastError { get; internal set; }

  #endregion

  /// <summary>
  /// Clears all internal states in the scripting engine. Must be called if the parsed expression is no more needed.
  /// </summary>
  public void Release() {
    foreach (var source in SignalSources.Values) {
      source.Dispose();
    }
    SignalSources.Clear();
  }

  ~ParserContext() {
    if (OnSignalChanged == null || SignalSources.Count <= 0) {
      return;
    }
    DebugEx.Error("Context was not cleared before being garbage collected: {0}", ParsedExpression);
    Release();
  }
}
