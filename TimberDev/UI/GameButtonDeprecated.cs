// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.TimberDev.UI;

/// <summary>This is a temporary solution until TAPI is updated from 0.7.6.0.</summary>
public class GameButtonDeprecated : ButtonGameDeprecated<GameButtonDeprecated>
{
  protected override GameButtonDeprecated BuilderInstance => this;
}

