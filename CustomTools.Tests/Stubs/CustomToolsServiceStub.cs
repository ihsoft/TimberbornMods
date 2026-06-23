using System.Collections.Generic;
using IgorZ.CustomTools.Tools;

namespace IgorZ.CustomTools.Core;

public class CustomToolsService {
  public readonly Dictionary<string, AbstractCustomTool> BlockObjectTools = new();
  public AbstractCustomTool SelectedTool { get; private set; }
  public string SelectedToolId { get; private set; }
  public string SelectedToolType { get; private set; }

  public void SelectTool(AbstractCustomTool tool) {
    SelectedTool = tool;
  }

  public void SelectToolById(string customToolId) {
    SelectedToolId = customToolId;
  }

  public void SelectToolByType(string type) {
    SelectedToolType = type;
  }
}
