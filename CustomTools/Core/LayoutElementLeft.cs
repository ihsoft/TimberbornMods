// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ConfigurableToolGroups.Services;

namespace IgorZ.CustomTools.Core;

sealed class LayoutElementLeft(
    CustomToolsService customToolsService, ModdableToolGroupButtonFactory groupButtonFactory)
    : AbstractLayoutElement(customToolsService, groupButtonFactory) {

    public override string Id => "CustomToolsLayoutLeft";
    protected override string Layout => "left";
}
