// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.CodeDom;
using Microsoft.CSharp;

namespace Stride.Core.Presentation.Extensions;

/// <summary>
/// Helper class to get formatted names from <see cref="Type"/>.
/// </summary>
public static class TypeExtensions
{
    private static readonly CSharpCodeProvider codeProvider = new();
    private static readonly HashSet<Type> valueTupleTypes =
    [
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    ];

    private static bool IsValueTuple(Type type) =>
        type.IsGenericType &&
        valueTupleTypes.Contains(type.GetGenericTypeDefinition());

    /// <summary>
    /// Gets C# syntax like type declaration from <see cref="Type"/>
    /// </summary>
    /// <param name="type">The type to get the name from.</param>
    /// <returns>C# syntax like type declaration from the type provided</returns>
    /// <exception cref="ArgumentNullException">If type parameter is null.</exception>
    public static string ToSimpleCSharpName(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsArray)
        {
            return ToSimpleCSharpName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (!type.IsGenericType)
        {
            //Use a CSharpCodeProvider to handle conversions like 'Int32' to 'int'
            var fullTypeName = codeProvider.GetTypeOutput(new CodeTypeReference(type));

            var simpleNameStart = fullTypeName.LastIndexOf('.') + 1;

            if (simpleNameStart >= fullTypeName.Length)
                return fullTypeName;

            return fullTypeName[simpleNameStart..];
        }

        if (Nullable.GetUnderlyingType(type) is Type nullableType)
        {
            return ToSimpleCSharpName(nullableType) + "?";
        }

        var genericParameters = string.Join(", ", type.GetGenericArguments().Select(ToSimpleCSharpName));

        if (IsValueTuple(type))
        {
            return "(" + genericParameters + ")";
        }

        return $"{type.Name[..type.Name.LastIndexOf('`')]}<{genericParameters}>";
    }
}
