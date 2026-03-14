// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ConfigurableToolGroups.Services;

namespace IgorZ.CustomTools.Core;

sealed class LayoutElementRight(
    CustomToolsService customToolsService, ModdableToolGroupButtonFactory groupButtonFactory)
    : AbstractLayoutElement(customToolsService, groupButtonFactory) {

  public override string Id => "CustomToolsLayoutRight";
  protected override string Layout => "right";
}
