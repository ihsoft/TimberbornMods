using System;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.TemplateSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

namespace IgorZ.DualDistrictStorage;

sealed class AsymmetricDualDistrictStorageToolFinder : IToolFinder {
  readonly ToolButtonService _toolButtonService;

  public AsymmetricDualDistrictStorageToolFinder(ToolButtonService toolButtonService) {
    _toolButtonService = toolButtonService;
  }

  public bool TryFindTool(BaseComponent entity, out Action toolActivationAction) {
    var templateName = entity.GetComponent<TemplateSpec>().TemplateName;
    var tool = _toolButtonService.ToolButtons
        .Where(toolButton => toolButton.ToolEnabled)
        .Select(toolButton => toolButton.Tool)
        .OfType<BlockObjectTool>()
        .SingleOrDefault(candidate => MatchesPhysicalTemplate(candidate, templateName));
    toolActivationAction = tool == null
        ? null
        : () => tool.ActivateWithDuplicationSource(entity);
    return tool != null;
  }

  static bool MatchesPhysicalTemplate(BlockObjectTool tool, string templateName) {
    if (!tool.Template.HasSpec<AsymmetricDualDistrictStoragePlacerSpec>()) {
      return false;
    }
    var placerSpec = tool.Template.GetSpec<AsymmetricDualDistrictStoragePlacerSpec>();
    return placerSpec.NarrowTemplateName == templateName
        || placerSpec.WideTemplateName == templateName;
  }
}
