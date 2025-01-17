// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Parser;

interface IValueExpr {
   public enum ValueType {
     Undefined,
     Number,
     String,
   }

   public ValueType Type { get; }
   public Func<string> GetStringValue { get; }
   public Func<int> GetNumberValue { get; }
 }
 