# Differences from FlatBuffers / FlatC

Struct type keys are supported on tables.

Each generated entity has a Metadata static class that describes it's fields.

Tables with key fields have `IComparable<T>` implemented based on the key fields raw binary value.

Structs have `IEquatable<T>` based on it's position in the buffer and `IComparable<T>` implemented based on it's raw binary value.

Note that `IComparable<T>` implementations will incorrectly order negative integers and floating point value types.
  - This is acceptable if the use case is to provide separation for sorted collection resolution.

See `BigBuffer.Tests/GeneratedStructuralTests.cs` for examples of construction (including the use of placeholders).
```csharp
	var bb = new BigBufferBuilder();
	TableQ.StartTableQ(bb);
	TableQ.AddX(bb, bb.MarkOffsetPlaceholder<TableN>(out var qx));
	TableQ.AddY(bb, bb.MarkOffsetPlaceholder<TableP>(out var qy));
	TableQ.AddZ(bb, bb.MarkOffsetPlaceholder<TableJ>(out var qz).Value);
	TableQ.AddZType(bb, UnionM.TableJ);
	var qo = TableQ.EndTableQ(bb);
	qo.Value.Should().Be(0);

	TableN.StartTableN(bb);
	TableN.AddX(bb, bb.MarkOffsetPlaceholder<TableJ>(out var nx).Value);
	TableN.AddXType(bb, UnionM.TableJ);
	var no = TableN.EndTableN(bb);

	TableJ.StartTableJ(bb);
	TableJ.AddX(bb, bb.MarkVectorPlaceholder(out var jx));
	var jo = TableJ.EndTableJ(bb);

	jx.FillVector(() => {
	bb.WriteOffset(jo);
	return 1;
	});
	nx.Fill(jo);

	TableP.StartTableP(bb);
	TableP.AddX(bb, EnumO.z);
	var po = TableP.EndTableP(bb);

	qx.Fill(no);
	qy.Fill(po);
	qz.Fill(jo);

	Placeholder.ValidateAllFilled(bb);
```

See `BigBuffer.Tests/GeneratedStructuralTests.cs` for examples of reading from buffers.
```csharp
	var t = TableQ.GetRootAsTableQ(bb.ByteBuffer);
```

Schema models with field names that equal their type names will be renamed and a warning will be issued.

Field accessors and mutators have metadata identifying them.

Union enums have metadata identifying their associated type.

Struct fields in factory methods are constructed from tuples.
  - Prefer creation of structs manually if there are nested structs.
    - There are padding and alignment bugs with the current implementation.

There is a `JsonParser` implementation that constructs a buffer from a `JsonDocument`.

`BigBuffers.Tests.MonsterDataTest.WriteMonsterDataTest` is a good example of the usage of the `JsonParser<T>`.

```csharp
	var MonsterDataTestJsonBytes = File.ReadAllBytes("monsterdata_test.json");
	var MonsterDataTestJsonDoc = JsonDocument.Parse(MonsterDataTestJsonBytes);
	var jsonRoot = MonsterDataTestJsonDoc.RootElement;
	var builder = new BigBufferBuilder();
	MyGame.Example.Monster.BeginMonsterBuffer(builder);
	var parser = new JsonParser<MyGame.Example.Monster>(builder);
	var monster = parser.Parse(jsonRoot);
	MyGame.Example.Monster.FinishMonsterBuffer(builder, monster);
```

The placeholder mechanism provides some debug-config-only validation;
  - `Placeholder.IsPlaceholder(builder, offset)`
  - `Placeholder.ValidateAllFilled(builder)`
  - `Placeholder.GetUnfilledCount(builder, out var count)`
  - `Placeholder.ValidateUnfilledCount(builder, out var count)`

Runtime configuration for `BigBufferBuilder` is provided by some static members;
  - `BigBufferBuilder.UseExistingVTables`, default true.
    - Should not be necessary to disable. Can be turned off to work
      around potential encoding problems.
  - `BigBufferBuilder.EnableAlignmentPadding`, default true.
    - Can be turned off to reduce size while maintaining compatibility.

