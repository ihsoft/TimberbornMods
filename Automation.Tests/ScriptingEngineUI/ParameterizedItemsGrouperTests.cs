using IgorZ.Automation.ScriptingEngineUI;

namespace Automation.Tests;

static class ParameterizedItemsGrouperTests {
  readonly record struct Item(string Name, string GroupKey);

  public static void RequiresMinimumGroupSize() {
    var items = new[] {
        new Item("first", "small"),
        new Item("second", "small"),
        new Item("third", "large"),
        new Item("fourth", "large"),
        new Item("fifth", "large"),
    };

    var groups = ParameterizedItemsGrouper.MakeGroups(items, item => item.GroupKey, minimumGroupSize: 3);

    Assert.Equal(1, groups.Length);
    Assert.Equal("large", groups[0].Key);
    Assert.Equal(3, groups[0].Items.Length);
  }

  public static void PreservesGroupAndItemOrder() {
    var items = new[] {
        new Item("b1", "group-b"),
        new Item("a1", "group-a"),
        new Item("b2", "group-b"),
        new Item("a2", "group-a"),
        new Item("b3", "group-b"),
        new Item("a3", "group-a"),
    };

    var groups = ParameterizedItemsGrouper.MakeGroups(items, item => item.GroupKey, minimumGroupSize: 3);

    Assert.Equal("group-b", groups[0].Key);
    Assert.Equal("group-a", groups[1].Key);
    Assert.Equal("b1", groups[0].Items[0].Name);
    Assert.Equal("b2", groups[0].Items[1].Name);
    Assert.Equal("b3", groups[0].Items[2].Name);
    Assert.Equal("a1", groups[1].Items[0].Name);
    Assert.Equal("a2", groups[1].Items[1].Name);
    Assert.Equal("a3", groups[1].Items[2].Name);
  }
}
