using System.Collections.Generic;

namespace Timberborn.CoreUI {
  public static class SpecialStrings {
    public const string RowStarter = "● ";
  }
}

namespace Timberborn.EntityPanelSystem {
  public sealed class EntityDescription {
    EntityDescription(string content, int order) {
      Content = content;
      Order = order;
    }

    public string Content { get; }
    public int Order { get; }

    public static EntityDescription CreateTextSection(string content, int order) {
      return new EntityDescription(content, order);
    }
  }

  public interface IEntityDescriber {
    IEnumerable<EntityDescription> DescribeEntity();
  }
}
