// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

interface IValueExpr {
   public ScriptValue.ValueType Type { get; }
   public Func<ScriptValue> ValueFn { get; }
}
