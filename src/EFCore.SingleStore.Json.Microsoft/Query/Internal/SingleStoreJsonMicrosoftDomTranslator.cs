﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using EntityFrameworkCore.SingleStore.Query.Expressions.Internal;
using EntityFrameworkCore.SingleStore.Query.ExpressionTranslators.Internal;
using EntityFrameworkCore.SingleStore.Query.Internal;
using EntityFrameworkCore.SingleStore.Storage.Internal;

namespace EntityFrameworkCore.SingleStore.Json.Microsoft.Query.Internal
{
    public class SingleStoreJsonMicrosoftDomTranslator : IMemberTranslator, IMethodCallTranslator
    {
        private static readonly MethodInfo _enumerableAnyWithoutPredicate = typeof(Enumerable).GetTypeInfo()
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Single(mi => mi.Name == nameof(Enumerable.Any) && mi.GetParameters().Length == 1);

        private static readonly MemberInfo _rootElement = typeof(JsonDocument).GetProperty(nameof(JsonDocument.RootElement));
        private static readonly MethodInfo _getProperty = typeof(JsonElement).GetRuntimeMethod(nameof(JsonElement.GetProperty), new[] { typeof(string) });
        private static readonly MethodInfo _getArrayLength = typeof(JsonElement).GetRuntimeMethod(nameof(JsonElement.GetArrayLength), Type.EmptyTypes);

        private static readonly MethodInfo _arrayIndexer = typeof(JsonElement).GetProperties()
            .Single(p => p.GetIndexParameters().Length == 1 &&
                         p.GetIndexParameters()[0].ParameterType == typeof(int))
            .GetMethod;

        private static readonly string[] _getMethods =
        {
            nameof(JsonElement.GetBoolean),
            nameof(JsonElement.GetDateTime),
            nameof(JsonElement.GetDateTimeOffset),
            nameof(JsonElement.GetDecimal),
            nameof(JsonElement.GetDouble),
            nameof(JsonElement.GetGuid),
            nameof(JsonElement.GetInt16),
            nameof(JsonElement.GetInt32),
            nameof(JsonElement.GetInt64),
            nameof(JsonElement.GetSingle),
            nameof(JsonElement.GetString)
        };

        private readonly IRelationalTypeMappingSource _typeMappingSource;
        private readonly SingleStoreSqlExpressionFactory _sqlExpressionFactory;
        private readonly SingleStoreJsonPocoTranslator _jsonPocoTranslator;

        public SingleStoreJsonMicrosoftDomTranslator(
            [NotNull] SingleStoreSqlExpressionFactory sqlExpressionFactory,
            [NotNull] IRelationalTypeMappingSource typeMappingSource,
            [NotNull] SingleStoreJsonPocoTranslator jsonPocoTranslator)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _typeMappingSource = typeMappingSource;
            _jsonPocoTranslator = jsonPocoTranslator;
        }

        public SqlExpression Translate(SqlExpression instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (instance?.Type.IsGenericList() == true &&
                member.Name == nameof(List<object>.Count) &&
                instance.TypeMapping is null)
            {
                return _jsonPocoTranslator.TranslateArrayLength(instance);
            }

            if (member.DeclaringType != typeof(JsonDocument))
            {
                return null;
            }

            if (member == _rootElement &&
                instance is ColumnExpression column &&
                column.TypeMapping is SingleStoreJsonTypeMapping)
            {
                // Simply get rid of the RootElement member access
                return column;
            }

            return null;
        }

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (instance != null && instance.Type.IsGenericList() && method.Name == "get_Item" && arguments.Count == 1)
            {
                // Try translating indexing inside json column
                return _jsonPocoTranslator.TranslateMemberAccess(instance, arguments[0], method.ReturnType);
            }

            // Predicate-less Any - translate to a simple length check.
            if (method.IsClosedFormOf(_enumerableAnyWithoutPredicate) &&
                arguments.Count == 1 &&
                arguments[0].Type.TryGetElementType(out _) &&
                arguments[0].TypeMapping is SingleStoreJsonTypeMapping)
            {
                return _sqlExpressionFactory.GreaterThan(
                    _jsonPocoTranslator.TranslateArrayLength(arguments[0]),
                    _sqlExpressionFactory.Constant(0));
            }

            if (method.DeclaringType != typeof(JsonElement) ||
                !(instance.TypeMapping is SingleStoreJsonTypeMapping mapping))
            {
                return null;
            }

            // The root of the JSON expression is a ColumnExpression. We wrap that with an empty traversal
            // expression (col->'$'); subsequent traversals will gradually append the path into that.
            // Note that it's possible to call methods such as GetString() directly on the root, and the
            // empty traversal is necessary to properly convert it to a text.
            instance = instance is ColumnExpression columnExpression
                ? _sqlExpressionFactory.JsonTraversal(
                    columnExpression, returnsText: false, typeof(string), mapping)
                : instance;

            if (method == _getProperty)
            {
                return instance is SingleStoreJsonTraversalExpression prevPathTraversal
                    ? prevPathTraversal.Append(
                        ApplyPathLocationTypeMapping(arguments[0]),
                        typeof(JsonElement),
                        _typeMappingSource.FindMapping(typeof(JsonElement)))
                    : null;
            }

            if (method == _arrayIndexer)
            {
                return instance is SingleStoreJsonTraversalExpression prevPathTraversal
                    ? prevPathTraversal.Append(
                        _sqlExpressionFactory.JsonArrayIndex(ApplyPathLocationTypeMapping(arguments[0])),
                        typeof(JsonElement),
                        _typeMappingSource.FindMapping(typeof(JsonElement)))
                    : null;
            }

            if (_getMethods.Contains(method.Name) &&
                arguments.Count == 0 &&
                instance is SingleStoreJsonTraversalExpression traversal)
            {
                return ConvertFromJsonExtract(
                    traversal.Clone(
                        method.Name == nameof(JsonElement.GetString),
                        method.ReturnType,
                        _typeMappingSource.FindMapping(method.ReturnType)
                    ),
                    method.ReturnType);
            }

            if (method == _getArrayLength)
            {
                // Could return NULL if the path is not found, but we would be alright to throw then.
                return _sqlExpressionFactory.NullableFunction(
                    "JSON_LENGTH",
                    new[] { instance },
                    typeof(int),
                    false);
            }

            if (method.Name.StartsWith("TryGet") && arguments.Count == 0)
            {
                throw new InvalidOperationException($"The TryGet* methods on {nameof(JsonElement)} aren't translated yet, use Get* instead.'");
            }

            return null;
        }

        private SqlExpression ConvertFromJsonExtract(SqlExpression expression, Type returnType)
            => returnType == typeof(bool)
                ? _sqlExpressionFactory.NonOptimizedEqual(
                    expression,
                    _sqlExpressionFactory.Constant(true, _typeMappingSource.FindMapping(typeof(bool))))
                : expression;

        private SqlExpression ApplyPathLocationTypeMapping(SqlExpression expression)
        {
            var pathLocation = _sqlExpressionFactory.ApplyDefaultTypeMapping(expression);

            // Path locations are usually made of strings. And they should be rendered without surrounding quotes.
            if (pathLocation is SqlConstantExpression sqlConstantExpression &&
                sqlConstantExpression.TypeMapping is SingleStoreStringTypeMapping stringTypeMapping &&
                !stringTypeMapping.IsUnquoted)
            {
                pathLocation = sqlConstantExpression.ApplyTypeMapping(stringTypeMapping.Clone(true));
            }

            return pathLocation;
        }
    }
}
