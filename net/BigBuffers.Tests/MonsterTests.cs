using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using BigBuffers.JsonParsing;
using FluentAssertions;
using NUnit.Framework;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Assertions;

namespace BigBuffers.Tests
{
  public static class MonsterDataTest
  {
    private static readonly byte[] MonsterDataTestJsonBytes = File.ReadAllBytes("monsterdata_test.json");
    private static readonly JsonDocument MonsterDataTestJsonDoc = JsonDocument.Parse(MonsterDataTestJsonBytes);

    [Theory]
    public static void ReadMonsterDataTest(bool validate)
    {
      //var bytes = File.ReadAllBytes("monsterdata_test.mon");

      //var buffer = new ByteBuffer(bytes);

      var mmf = MemoryMappedFile.CreateFromFile
      ("monsterdata_test.mon", FileMode.Open,
        null, 0, MemoryMappedFileAccess.Read);

      var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

      var buffer = new ByteBuffer(view.SafeMemoryMappedViewHandle);

      var root = buffer.__indirect(0);

      MyGame.Example.Monster.MonsterBufferHasIdentifier(buffer)
        .Should().BeTrue();

      buffer.Position = root;

      var monster = MyGame.Example.Monster.GetRootAsMonster(buffer);
      monster._model.Offset.Should().Be(root);

      if (validate)
        ValidateMonsterJson(monster);
    }

    [Theory]
    public static void WriteMonsterDataTest(bool validate)
    {
      //BigBufferBuilder.EnableAlignmentPadding = false;

      var jsonRoot = MonsterDataTestJsonDoc.RootElement;

      var builder = new BigBufferBuilder();
      MyGame.Example.Monster.BeginMonsterBuffer(builder);
      var parser = new JsonParser<MyGame.Example.Monster>(builder);
      var monster = parser.Parse(jsonRoot);
      MyGame.Example.Monster.FinishMonsterBuffer(builder, monster);

      if (!validate) return;

      Placeholder.ValidateAllFilled(builder);
      ValidateMonsterJson(monster);

      using var f = File.OpenWrite("monsterdata_test_check.mon");
      f.SetLength(0);
      f.Write(builder.SizedReadOnlySpan());
      f.Close();
    }

    private static void ValidateMonsterJson(MyGame.Example.Monster monster)
    {

      monster.Pos.Should().NotBeNull();
      var pos = monster.Pos!.Value;

      pos.X.Should().Be(1);
      pos.Y.Should().Be(2);
      pos.Z.Should().Be(3);
      pos.Test1.Should().Be(3.0);
      pos.Test2.Should().Be(MyGame.Example.Color.Green);

      var posTest3 = pos.Test3;

      posTest3.A.Should().Be(5);
      posTest3.B.Should().Be(6);

      monster.Hp.Should().Be(80);

      monster.Name.Should().Be("MyMonster");

      monster.InventoryLength.Should().Be(5);

      var inventoryExpected = new byte[] { 0, 1, 2, 3, 4 };

      BigSpanAssert.AreEqual(BigSpan.Create(inventoryExpected), monster.GetInventorySpan());

      monster.TestType.Should().Be(MyGame.Example.Any.Monster);

      var testMon = monster.Test<MyGame.Example.Monster>();
      testMon
        .Should().NotBeNull()
        .And.BeOfType<MyGame.Example.Monster>();

      var testMonValue = testMon!.Value;

      testMonValue.Name.Should().Be("Fred");

      monster.Test4Length.Should().Be(2);

      var test4First = monster.Test4(0);
      test4First.Should().NotBeNull();

      var test4FirstValue = test4First!.Value;
      test4FirstValue.Should().BeOfType<MyGame.Example.Test>();

      test4FirstValue.A.Should().Be(10);
      test4FirstValue.B.Should().Be(20);

      var test4Second = monster.Test4(1);
      test4Second.Should().NotBeNull();

      var test4SecondValue = test4Second!.Value;
      test4SecondValue.Should().BeOfType<MyGame.Example.Test>();

      test4SecondValue.A.Should().Be(30);
      test4SecondValue.B.Should().Be(40);

      monster.TestarrayofstringLength.Should().Be(2);

      var taoS0 = monster.Testarrayofstring(0);
      taoS0.Should().Be("test1");

      var taoS1 = monster.Testarrayofstring(1);
      taoS1.Should().Be("test2");

      monster.Testarrayofstring2Length.Should().Be(0);

      monster.TestarrayofboolsLength.Should().Be(3);
      monster.Testarrayofbools(0).Should().BeTrue();
      monster.Testarrayofbools(1).Should().BeFalse();
      monster.Testarrayofbools(2).Should().BeTrue();
    }
  }
}
