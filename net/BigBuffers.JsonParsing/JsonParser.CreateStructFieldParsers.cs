#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using StirlingLabs.Utilities.Magic;
using static BigBuffers.JsonParsing.JsonParser;

namespace BigBuffers.JsonParsing
{
  public sealed partial class JsonParser<T>
  {
    private static void CreateStructFieldParsers()
    {
      Debug.Assert(IfType<T>.IsAssignableTo<IBigBufferStruct>());
      FieldParsers.Clear();

      for (var i = (ushort)0; i < Metadata.Length; i++)
      {
        var parser = Expression.Parameter(typeof(JsonParser<T>), "parser");
        var element = Expression.Parameter(typeof(JsonElement), "element");

        var body = new List<Expression>();
        var vars = new List<ParameterExpression>();

        ref var md = ref Metadata[i];

        var name = md.Name;

#if DEBUG
        body.Add(Expression.Call(typeof(Debug).GetMethod(nameof(Debug.Assert))!,
          Expression.NotEqual(Expression.PropertyOrField(parser, nameof(Entity)), Expression.Constant(default(T))),
          Expression.Constant("Entity missing on JsonParser handling a Struct.")));

        // var sizeAssert = parser.Builder.Offset; 
        var offsetCheck = Expression.Variable(typeof(ulong), "offsetCheck");
        vars.Add(offsetCheck);
        body.Add(Expression.Assign(offsetCheck,
          Expression.PropertyOrField(Expression.PropertyOrField(parser, nameof(Builder)), nameof(BigBufferBuilder.Offset))));

        var entityOffsetCheck = Expression.Variable(typeof(ulong), "entityOffsetCheck");
        vars.Add(entityOffsetCheck);
        body.Add(Expression.Assign(entityOffsetCheck,
          DescendPropertiesOrFields(parser, nameof(Entity), "_model", nameof(BigBufferBuilder.Offset))));

        var pushOffsetMethod = typeof(JsonParser).GetMethod(nameof(PushOffset), AnyAccessBindingFlags | BindingFlags.Static);
        body.Add(Expression.Call(pushOffsetMethod!,
          Expression.Add(entityOffsetCheck, Expression.Constant((ulong)md.Offset)),
          Expression.Constant(name)));
#endif

        if (FieldIndexToSetter.TryGetValue(i, out var setter))
        {
          var setterParams = setter.GetParameters();

          var setterParamsLength = setterParams.Length;
          var isIndexer = setterParamsLength > 1;

          var setterType = setterParams[setterParamsLength - 1].ParameterType;

          // var entity = parser.Model;
          var entity = Expression.Variable(typeof(T), "entity");
          vars.Add(entity);
          body.Add(Expression.Assign(entity, Expression.PropertyOrField(parser, nameof(Entity))));

          if (setterType.IsPrimitive)
          {
            if (isIndexer)
            {
              throw new NotImplementedException();
              // TODO:
              // for ( var i = 0; i < FieldSize/ElementSize; ++i)
              //   entity.SetField(index, PrimitiveParser(element));
            }
            else
            {
              var primParser = PrimitiveParsers[setterType];

              // entity.SetField(PrimitiveParser(element))
              body.Add(Expression.Call(entity, setter, Expression.Call(primParser, element)));
            }
          }
          else if (setterType.IsEnum)
          {
            if (isIndexer)
            {
              throw new NotImplementedException();
              // TODO:
              // for ( var i = 0; i < FieldSize/EnumSize; ++i)
              //   entity.SetField(index, PrimitiveParser(element));
            }
            else
            {
              var enumResolver = ResolveEnumMethodInfo.MakeGenericMethod(setterType);

              // entity.SetField(JsonUtilities.ResolveEnum<T>(element))
              body.Add(Expression.Call(entity, setter, Expression.Call(enumResolver, element)));

            }
          }
          else
          {
            throw new NotImplementedException();
          }
        }
        else if (StructFieldsIndex.TryGetValue(i, out var structField))
        {
          var structFieldParams = structField.GetParameters();
          var setterParamsLength = structFieldParams.Length;
          var isIndexer = setterParamsLength > 1;

          if (isIndexer)
          {
            throw new NotImplementedException();
            // TODO:
            // for ( var i = 0; i < FieldSize/StructSize; ++i)
            //   ???
          }

          var structType = structField.ReturnType;

          // var builder;
          var builder = Expression.Variable(typeof(BigBufferBuilder), "builder");
          vars.Add(builder);

          // var newStruct;
          var newStruct = Expression.Variable(structType, "newStruct");
          vars.Add(newStruct);

          // builder = parser.Builder;
          body.Add(Expression.Assign(builder, Expression.PropertyOrField(parser, nameof(Builder))));

#if DEBUG
          var peekMethodInfo = typeof(JsonParser<T>).GetMethod(nameof(Peek), AnyAccessBindingFlags | BindingFlags.Instance)!;
#endif

          var structCtor = structType.GetConstructor(AnyAccessBindingFlags | BindingFlags.Instance,
            null, EntityCtorParamTypes, null);
          Debug.Assert(structCtor != null);

          // newStruct = new T(parser.Entity._model.Offset + OffsetConst, builder.ByteBuffer);
          var offsetProp = Expression.PropertyOrField(
            Expression.PropertyOrField(
              Expression.PropertyOrField(parser, nameof(Entity)),
              "_model"),
            nameof(Model.Offset)
          );
#if DEBUG
          body.Add(Expression.Call(parser, peekMethodInfo,
            Expression.NewArrayInit(typeof(object),
              Expression.Constant("offsetProp"), Expression.Convert(offsetProp, typeof(object)))));
#endif

          body.Add(Expression.Assign(newStruct, Expression.New(structCtor!,
            Expression.Add(Expression.Constant((ulong)md.Offset), offsetProp),
            Expression.PropertyOrField(builder, nameof(BigBufferBuilder.ByteBuffer)))));

#if DEBUG
          body.Add(Expression.Call(parser, peekMethodInfo,
            Expression.NewArrayInit(typeof(object),
              Expression.Constant("newStruct"), Expression.Convert(newStruct, typeof(object)))));
#endif

          // new JsonParser<T>(parser) { Entity = newStruct }.Parse(element);
          var structParserType = typeof(JsonParser<>).MakeGenericType(structType);
          var modelMember = structParserType.GetField(nameof(Entity), AnyAccessBindingFlags | BindingFlags.Instance);
          Debug.Assert(modelMember != null);

          var structParser = Expression.Variable(structParserType, "structParserType");
          vars.Add(structParser);

          var structParserCtor = structParserType.GetConstructor(AnyAccessBindingFlags | BindingFlags.Instance,
            null, new[] { typeof(IJsonParser), structType }, null)!;

          body.Add(Expression.Assign(structParser,
            Expression.New(structParserCtor,
              parser, newStruct
            )));

#if DEBUG
          body.Add(Expression.Call(parser, peekMethodInfo,
            Expression.NewArrayInit(typeof(object),
              Expression.Constant("structParser"), Expression.Convert(structParser, typeof(object)))));
#endif

          var parseMethodInfo = structParserType.GetMethod(nameof(Parse), AnyAccessBindingFlags | BindingFlags.Instance)!;
          body.Add(Expression.Call(structParser, parseMethodInfo, element));
        }
        else
        {
          throw new NotImplementedException();
        }

#if DEBUG
        // JsonParser<T>.OffsetAssert(offset, sizeConst, parser.Builder.Offset);
        body.Add(Expression.Call(
          typeof(JsonParser<T>).GetMethod(nameof(OffsetAssert), AnyAccessBindingFlags | BindingFlags.Static)!,
          Expression.Constant(md.Name),
          offsetCheck,
          Expression.Constant(0uL),
          DescendPropertiesOrFields(parser, nameof(Builder), nameof(BigBufferBuilder.Offset))
        ));
#endif

        var paramExprs = new[] { parser, element };
        var lambdaExpr = Expression.Lambda<Action<JsonParser<T>, JsonElement>>
          (Expression.Block(vars, body), name, paramExprs);

        var compiledLambda = lambdaExpr.Compile();

#if DEBUG
        FieldParserExpressions.Add(name, lambdaExpr);
#endif
        FieldParsers.Add(name, compiledLambda);
      }
    }
  }
}
