#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using StirlingLabs.Utilities;
using static BigBuffers.JsonParsing.JsonParser;

namespace BigBuffers.JsonParsing
{
  public sealed partial class JsonParser<T>
  {
    private static void CreateTableFieldParsers()
    {
      Debug.Assert(Type<T>.IsAssignableTo<IBigBufferTable>());
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
        var pushOffsetMethod = Info.OfMethod("BigBuffers.JsonParsing", "BigBuffers.JsonParsing.JsonParser", nameof(PushOffset));
        //typeof(JsonParser).GetMethod(nameof(PushOffset), AnyAccessBindingFlags | BindingFlags.Static);
        body.Add(Expression.Call(pushOffsetMethod!,
          DescendPropertiesOrFields(parser,
            nameof(Builder),
            nameof(BigBufferBuilder.Offset)),
          Expression.Constant(md.Name)
        ));
#endif

        var adder = FieldIndexToAdder[i];

        Debug.Assert(adder != null);

        var adderParams = adder!.GetParameters();

        var adderParam = adderParams[1];

        var adderType = adderParam.ParameterType;
        var origAdderType = adderType;

        var isOffsetType = adderType.IsConstructedGenericType
          && adderType.GetGenericTypeDefinition() == typeof(Offset<>);
        if (isOffsetType)
          adderType = adderType.GenericTypeArguments[0];

        var isULong = adderType == typeof(ulong);

        if (!isOffsetType && isULong
          && adderParam.Name!.EndsWith("Offset"))
          isOffsetType = true;

        if (adderType.IsPrimitive && !isOffsetType)
        {
          // T.AddField(parser.Builder, PrimitiveParser(element));
          var primParser = PrimitiveParsers[adderType];
          var parsed = Expression.Call(primParser, element);
          var add = Expression.Call(adder, Expression.PropertyOrField(parser, nameof(Builder)), parsed);
          body.Add(add);
        }
        else if (isULong && isOffsetType)
        {
          var typeAdder = FieldIndexToAdder[(ushort)(i - 1)];

          var typeAdderParams = typeAdder.GetParameters();

          var typeAdderParam = typeAdderParams[1];

          var enumType = typeAdderParam.ParameterType;

          // var entity = parser.Entity;
          var parserEntity = Expression.Variable(typeof(T), "parserEntity");
          vars.Add(parserEntity);
          body.Add(Expression.Assign(parserEntity, Expression.PropertyOrField(parser, nameof(Entity))));

          // TODO: maybe defer until type has been guaranteed parsed?
          // var unionType = parser.GetUnionTypeSpec<TEnum>(fieldId);
          var unionType = Expression.Variable(enumType, "unionType");
          vars.Add(unionType);
          body.Add(Expression.Assign(unionType,
            Expression.Call(parser, GetUnionTypeSpecMethodInfo.MakeGenericMethod(enumType),
              Expression.Constant(i))));

          var enumFields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

          var switchCases = new SwitchCase[enumFields.Length];

          var e = -1;
          object noneValue = ReBoxToEnumType(0, enumType);
          foreach (var enumField in enumFields)
          {
            ++e;
            if (enumField.Name == "NONE") continue;

            var enumValue = enumField.GetRawConstantValue();

            var unionTypeAssoc = enumField.GetCustomAttribute<UnionTypeAssociationAttribute>();

            Debug.Assert(unionTypeAssoc != null);

            var assocType = unionTypeAssoc!.Type;

            if (assocType == typeof(string))
            {
              // var builder = parser.Builder;
              var builder = Expression.Variable(typeof(BigBufferBuilder), "builder");
              var getBuilder = Expression.Assign(builder, Expression.PropertyOrField(parser, nameof(Builder)));

              // builder.Prep(sizeof(ulong), sizeof(ulong));
              var prepMethod = Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.Prep));
              var prep = Expression.Call(builder, prepMethod, Expression.Constant((ulong)sizeof(ulong)), Expression.Constant((ulong)sizeof(ulong)));

              var creator = Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.MarkStringPlaceholder));

              // T.AddField(builder, builder.MarkStringPlaceholder(out var placeholder));
              var placeholder = Expression.Variable(typeof(Placeholder), "placeholder");
              var offset = Expression.Call(builder, creator!, placeholder);
              var add = Expression.Call(adder, builder, offset);

              // parser.ParseAndFillString(placeholder, element);
              var parseAndFillString = Expression.Call(parser, ParseAndFillStringMethodInfo, element, placeholder);

              //  case String: {
              //    var builder = parser.Builder;
              //    TEntity.AddField(builder, builder.MarkStringPlaceholder(out var placeholder));
              //    parser.ParseAndFillString(placeholder, element);
              //    break;
              //  }
              Expression.SwitchCase(Expression.Block(
                new [] { builder, placeholder },
                getBuilder,
                prep,
                add,
                parseAndFillString
              ));
            }
            else
            {
              var typedPlaceholder = typeof(Placeholder<>).MakeGenericType(assocType);

              // var builder = parser.Builder;
              var builder = Expression.Variable(typeof(BigBufferBuilder), "builder");
              var getBuilder = Expression.Assign(builder, Expression.PropertyOrField(parser, nameof(Builder)));

              // builder.Prep(sizeof(ulong), sizeof(ulong));
              var prepMethod = Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.Prep));
              var prep = Expression.Call(builder, prepMethod,
                Expression.Constant((ulong)sizeof(ulong)), Expression.Constant((ulong)sizeof(ulong)));

              var creator = Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.MarkOffsetPlaceholder))
                .MakeGenericMethod(assocType);

              // T.AddField(builder, builder.MarkOffsetPlaceholder(out var placeholder));
              var placeholder = Expression.Variable(typedPlaceholder, "placeholder");
              var offset = Expression.Call(builder, creator!, placeholder);
              var offsetValue = Expression.PropertyOrField(offset, nameof(Offset<T>.Value));

              var modelParserType = typeof(JsonDeferredModelParser<,>).MakeGenericType(typeof(T), assocType);
              var modelParserCtor = modelParserType.GetConstructor(
                AnyAccessBindingFlags | BindingFlags.Instance,
                null, new[] { typeof(JsonParser<T>), typedPlaceholder }, null);
              Debug.Assert(modelParserCtor != null);

              var add = Expression.Call(adder, builder, offsetValue);

              // new JsonParser<T>(parser, placeholder).Parse(element);
              var parseAndFillOffset = Expression.Call(
                Expression.New(modelParserCtor, parser, placeholder),
                modelParserType.GetMethod(nameof(IJsonElementParser.Parse))!, element);

              enumValue = ReBox(enumValue, enumType);

              //  case T: {
              //    var builder = parser.Builder;
              //    builder.Prep(sizeof(ulong), sizeof(ulong));
              //    TEntity.AddField(builder, builder.MarkOffsetPlaceholder(out var placeholder));
              //    new JsonParser<TField>(parser, placeholder).Parse(element);
              //    break;
              //  }
              var switchCaseValue = Expression.Constant(enumValue, enumType);
              switchCases[e] = Expression.SwitchCase(
                Expression.Block(
                  new [] { builder, placeholder },
                  getBuilder,
                  prep,
                  add,
                  parseAndFillOffset
                ),
                switchCaseValue);
            }
          }

          // switch(unionType) {
          //  case String: {
          //    var builder = parser.Builder;
          //    TEntity.AddField(builder, builder.MarkStringPlaceholder(out var placeholder));
          //    parser.ParseAndFillString(placeholder, element);
          //    break;
          //  }
          //  case T: { // per table or struct in union 
          //    var builder = parser.Builder;
          //    builder.Prep(sizeof(ulong), sizeof(ulong));
          //    TEntity.AddField(builder, builder.MarkOffsetPlaceholder(out var placeholder));
          //    new JsonParser<TField>(parser, placeholder).Parse(element);
          //    break;
          //  }
          //  default:
          //    throw new NotSupportedException();
          //  }
          var defaultCase = Expression.Throw(Expression.New(typeof(NotSupportedException).GetConstructor(Type.EmptyTypes)!));
          switchCases[0] = Expression.SwitchCase(defaultCase, Expression.Constant(noneValue));
          body.Add(Expression.Switch(unionType,
            defaultCase,
            switchCases
          ));
        }
        else if (adderType.IsEnum)
        {
          var enumResolver = ResolveEnumMethodInfo.MakeGenericMethod(adderType);

          // var enumValue = JsonUtilities.ResolveEnum<TEnum>(element);
          var enumValue = Expression.Variable(adderType, "enumValue");
          vars.Add(enumValue);
          body.Add(Expression.Assign(enumValue, Expression.Call(enumResolver, element)));

          // T.AddField(parser.Builder, enumValue);
          body.Add(Expression.Call(adder, Expression.PropertyOrField(parser, nameof(Builder)), enumValue));

          // if it could possibly be a union type enum...
          if (adder.Name.EndsWith("Type"))
            // parser.StoreUnionTypeSpec<TEnum>(fieldId, enumValue); 
            body.Add(Expression.Call(parser, StoreUnionTypeSpecMethodInfo.MakeGenericMethod(adderType),
              Expression.Constant(i), enumValue));
        }
        else
        {
          if (adderType == typeof(VectorOffset))
          {

            MethodInfo? filler = null;

            Type? fillerType = null;

            var haveCreator = FieldVectorCreators.TryGetValue(i, out var creator);
            if (haveCreator)
            {
              var fillers = FieldVectorFillers[i];
              foreach (var fillerCandidate in fillers)
              {
                var fillerParams = fillerCandidate.GetParameters();
                fillerType = fillerParams[1].ParameterType;

                var isArray = fillerType.IsArray;
                if (!isArray) continue;

                filler = fillerCandidate;
                break;
              }

              Debug.Assert(filler != null);
              Debug.Assert(fillerType != null);
            }

            var elemType = fillerType is null
              ? throw new NotImplementedException()
              : fillerType.GetElementType();

            Debug.Assert(elemType != null);

            isOffsetType = elemType!.IsConstructedGenericType
              && elemType.GetGenericTypeDefinition() == typeof(Offset<>);
            if (isOffsetType)
              elemType = elemType.GenericTypeArguments[0];

            isULong = elemType == typeof(ulong);

            if (!isOffsetType && isULong
              && adderParam.Name!.EndsWith("Offset"))
              isOffsetType = true;

            var fillerDlgType = typeof(PlaceholderArrayFiller<>).MakeGenericType(elemType);

            var placeholder = Expression.Variable(typeof(Placeholder), "placeholder");
            vars.Add(placeholder);

            var builder = Expression.Variable(typeof(BigBufferBuilder), "builder");
            vars.Add(builder);
            body.Add(Expression.Assign(builder, Expression.PropertyOrField(parser, nameof(Builder))));

            Debug.Assert(haveCreator);
            var offset = Expression.Call(creator!, builder, placeholder);
            var add = Expression.Call(adder, builder, offset);
            body.Add(add);

            Type? vecParserType = null;

            if (elemType.IsPrimitive && !isOffsetType)
            {
              Debug.Assert(filler != null);
              vecParserType = typeof(JsonPrimitiveVectorParser<,>)
                .MakeGenericType(typeof(T), elemType);

              var vecParser = Expression.Variable(vecParserType, "vecParser");
              vars.Add(vecParser);
              var fillerAction = Expression.Constant(filler!.CreateDelegate(fillerDlgType), fillerDlgType);
              body.Add(Expression.Assign(vecParser,
                Expression.New(vecParserType.GetConstructor(new[] { typeof(JsonParser<T>), typeof(Placeholder), fillerDlgType })!,
                  parser, placeholder, fillerAction)));

              body.Add(Expression.Call(vecParser, vecParserType.GetMethod(nameof(IJsonVectorParser.Parse))!, element));
            }
            else if (isULong && isOffsetType)
            {
              // union

              throw new NotImplementedException();
            }
            else if (typeof(IBigBufferTable).IsAssignableFrom(elemType))
            {
              // table
              vecParserType = typeof(JsonTableVectorParser<,>)
                .MakeGenericType(typeof(T), elemType);

              var vecParser = Expression.Variable(vecParserType, "vecParser");
              vars.Add(vecParser);
              body.Add(Expression.Assign(vecParser,
                Expression.New(vecParserType.GetConstructor(new[] { typeof(JsonParser<T>), typeof(Placeholder) })!,
                  parser, placeholder)));

              body.Add(Expression.Call(vecParser, vecParserType.GetMethod(nameof(IJsonVectorParser.Parse))!, element));
            }
            else if (typeof(IBigBufferStruct).IsAssignableFrom(elemType))
            {
              // struct
              vecParserType = typeof(JsonStructVectorParser<,>)
                .MakeGenericType(typeof(T), elemType);

              var vecParser = Expression.Variable(vecParserType, "vecParser");
              vars.Add(vecParser);
              body.Add(Expression.Assign(vecParser,
                Expression.New(vecParserType.GetConstructor(new[] { typeof(JsonParser<T>), typeof(Placeholder) })!,
                  parser, placeholder)));

              body.Add(Expression.Call(vecParser, vecParserType.GetMethod(nameof(IJsonVectorParser.Parse))!, element));
            }
            else if (typeof(StringOffset).IsAssignableFrom(elemType))
            {
              vecParserType = typeof(JsonStringVectorParser<>)
                .MakeGenericType(typeof(T));
              var vecParser = Expression.Variable(vecParserType, "vecParser");
              vars.Add(vecParser);
              body.Add(Expression.Assign(vecParser,
                Expression.New(vecParserType.GetConstructor(new[] { typeof(JsonParser<T>), typeof(Placeholder) })!,
                  parser, placeholder)));

              body.Add(Expression.Call(vecParser, vecParserType.GetMethod(nameof(IJsonVectorParser.Parse))!, element));
            }
            else
            {
              throw new NotImplementedException(elemType.ToString());
            }
          }
          else if (adderType == typeof(StringOffset))
          {
            var builder = Expression.Variable(typeof(BigBufferBuilder), "builder");
            vars.Add(builder);
            body.Add(Expression.Assign(builder, Expression.PropertyOrField(parser, nameof(Builder))));

            // builder.Prep(sizeof(ulong), sizeof(ulong));
            var prepMethod = Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.Prep));
            var prep = Expression.Call(builder, prepMethod,
              Expression.Constant((ulong)sizeof(ulong)), Expression.Constant((ulong)sizeof(ulong)));
            body.Add(prep);

            var creator = Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.MarkStringPlaceholder));
            //typeof(BigBufferBuilder).GetMethod(nameof(BigBufferBuilder.MarkStringPlaceholder))!;
            var placeholder = Expression.Variable(typeof(Placeholder), "placeholder");
            vars.Add(placeholder);

            var offset = Expression.Call(builder, creator!, placeholder);
            var add = Expression.Call(adder, builder, offset);
            body.Add(add);
            // parser.ParseAndFillString(element, placeholder);
            body.Add(Expression.Call(parser, ParseAndFillStringMethodInfo, element, placeholder));
          }
          else if (isOffsetType)
          {
            if (typeof(IBigBufferTable).IsAssignableFrom(adderType))
            {
              var creator = BigBufferBuilderPutOffsetPlaceholderMethodInfo.MakeGenericMethod(adderType);

              var placeholderType = typeof(Placeholder<>).MakeGenericType(adderType);
              var placeholder = Expression.Variable(placeholderType, "placeholder");
              vars.Add(placeholder);
              body.Add(Expression.Assign(placeholder, Expression.Default(placeholderType)));

              //var placeholderRef = Expression.Variable(placeholderType.MakeByRefType(), "placeholderRef");
              //body.Add(Expression.Assign(placeholderRef, placeholder));
              //var offset = Expression.Call(builder, creator, placeholderRef);

              var builder = Expression.Variable(typeof(BigBufferBuilder), "builder");
              vars.Add(builder);
              body.Add(Expression.Assign(builder, Expression.PropertyOrField(parser, nameof(Builder))));

              // builder.Prep(sizeof(ulong), sizeof(ulong));
              var prepMethod = Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.Prep));
              var prep = Expression.Call(builder, prepMethod,
                Expression.Constant((ulong)sizeof(ulong)), Expression.Constant((ulong)sizeof(ulong)));
              body.Add(prep);
              
              var offset = Expression.Call(builder, creator, placeholder);
              var add = Expression.Call(adder, builder, offset);
              body.Add(add);

              var tableParserType = typeof(JsonDeferredModelParser<,>)
                .MakeGenericType(typeof(T), adderType);

              var elemParser = Expression.Variable(tableParserType, "elemParser");
              vars.Add(elemParser);

              body.Add(Expression.Assign(elemParser,
                Expression.New(tableParserType.GetConstructor(new[] { typeof(JsonParser<T>), placeholderType })!,
                  parser, placeholder)));

              body.Add(Expression.Call(elemParser, tableParserType.GetMethod(nameof(IJsonElementParser.Parse))!, element));
            }
            else if (typeof(IBigBufferStruct).IsAssignableFrom(adderType))
            {
              var structParserType = typeof(JsonParser<>)
                .MakeGenericType(adderType);

              // var builder;
              var builder = Expression.Variable(typeof(BigBufferBuilder), "builder");
              vars.Add(builder);

              // var newStruct;
              var newStruct = Expression.Variable(adderType, "newStruct");
              vars.Add(newStruct);

              // builder = parser.Builder;
              body.Add(Expression.Assign(builder, Expression.PropertyOrField(parser, nameof(Builder))));

              var ctor = adderType.GetConstructor(AnyAccessBindingFlags | BindingFlags.Instance,
                null, EntityCtorParamTypes, null);
              Debug.Assert(ctor != null);
              var sizeConst = Expression.Constant((ulong)md.Size);
              var alignConst = Expression.Constant((ulong)md.Align);
              /*
              // if (!parser.IsInline) builder.Prep(align, size);
              body.Add(Expression.IfThen(Expression.Not(Expression.PropertyOrField(parser, nameof(IsInline))),
                Expression.Call(builder, typeof(BigBufferBuilder).GetMethod(nameof(BigBufferBuilder.Prep))!,
                  alignConst, sizeConst)));
              */

              // builder.Prep(align, size);
              body.Add(Expression.Call(builder, Info.OfMethod<BigBufferBuilder>(nameof(BigBufferBuilder.Prep)),
                alignConst, sizeConst));

              // newStruct = new T(builder.Offset, builder.ByteBuffer);
              var offsetProp = Expression.PropertyOrField(builder, nameof(BigBufferBuilder.Offset));
              body.Add(Expression.Assign(newStruct, Expression.New(ctor!,
                offsetProp,
                Expression.PropertyOrField(builder, nameof(BigBufferBuilder.ByteBuffer)))));

              // builder.Offset += size;
              body.Add(Expression.AddAssign(offsetProp, sizeConst));

              // var structParser;
              var structParser = Expression.Variable(structParserType, "elemParser");
              vars.Add(structParser);

              // structParser = new JsonParser<T>(parser); 
              body.Add(Expression.Assign(structParser,
                Expression.MemberInit(
                  Expression.New(structParserType.GetConstructor(AnyAccessBindingFlags | BindingFlags.Instance,
                      null, JsonParserCtorParamTypes, null)!,
                    parser
                  ),
                  Expression.Bind(structParserType.GetField(nameof(Entity), AnyAccessBindingFlags | BindingFlags.Instance)!,
                    newStruct)
                )));

              // structParser.Parse(element);
              body.Add(Expression.Call(structParser, structParserType.GetMethod(nameof(IJsonParser<T>.Parse))!, element));

              // T.AddField(builder, (Offset<>)newStruct)
              body.Add(Expression.Call(adder, Expression.PropertyOrField(parser, nameof(Builder)),
                Expression.Convert(newStruct, origAdderType)));

            }
            else
            {
              throw new NotImplementedException();
            }
          }
          else
          {
            throw new NotImplementedException();
          }
        }

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
