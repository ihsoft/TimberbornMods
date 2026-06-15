using System.IO;
using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class StringProtoSerializerTests {
  public static void SerializesObjectToBase64AndDeserializesIt() {
    StringProtoSerializer.AddType<TestProtoData>(["Number", "Text"]);
    var serialized = StringProtoSerializer.Serialize(new TestProtoData {
        Number = 42,
        Text = "answer",
    });

    var deserialized = StringProtoSerializer.Deserialize<TestProtoData>(serialized);

    Assert.True(serialized.Length > 0);
    Assert.Equal(42, deserialized.Number);
    Assert.Equal("answer", deserialized.Text);
  }

  public static void ValidatesAddType() {
    StringProtoSerializer.AddType<AnotherProtoData>(["Value"]);
    StringProtoSerializer.AddType<AnotherProtoData>(["Value"]);

    Assert.Throws<InvalidDataException>(() => StringProtoSerializer.AddType<AnotherProtoData>(["Other"]));
    Assert.Throws<InvalidDataException>(() => StringProtoSerializer.AddType<AnotherProtoData>(["Value", "Other"]));
  }

  public sealed class TestProtoData {
    public int Number { get; set; }
    public string Text { get; set; }
  }

  public sealed class AnotherProtoData {
    public int Value { get; set; }
  }
}
