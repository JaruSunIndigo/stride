// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using System.Reflection.Emit;

namespace Stride.Core.Reflection;

public static class AbstractObjectInstantiator
{
    private static readonly Dictionary<Type, Type> ConstructedTypes = [];

    /// <summary>
    /// Creates an instance of a type implementing the specified <paramref name="baseType"/>.
    /// </summary>
    /// <param name="baseType"></param>
    /// <remarks>
    /// If <paramref name="baseType"/> is already a concrete type (not an abstract type nor an interface, the method returns an instance of <paramref name="baseType"/> itself.
    /// </remarks>
    /// <returns>An instance of a type implementing the specified <paramref name="baseType"/>.</returns>
    /// <seealso cref="Activator.CreateInstance(Type)"/>
    public static object CreateConcreteInstance(Type baseType)
    {
        ArgumentNullException.ThrowIfNull(baseType);

        if (!baseType.IsAbstract && !baseType.IsInterface)
            return Activator.CreateInstance(baseType)!;

        Type? constructedType;
        lock (ConstructedTypes)
        {
            if (!ConstructedTypes.TryGetValue(baseType, out constructedType))
            {
                var asmName = new AssemblyName($"ConcreteObject_{Guid.NewGuid():N}");

                // Create assembly (in memory)
                var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
                var moduleBuilder = asmBuilder.DefineDynamicModule("DynamicModule");

                // Define type
                var typeBuilder = moduleBuilder.DefineType($"{baseType}Impl");
                InitializeTypeBuilderFromType(typeBuilder, baseType);

                // Create type
                constructedType = typeBuilder.CreateTypeInfo();
                ConstructedTypes.Add(baseType, constructedType);

            }
        }
        return Activator.CreateInstance(constructedType)!;
    }

    /// <summary>
    /// Initializes the <paramref name="typeBuilder"/> using the provided <paramref name="baseType"/>.
    /// </summary>
    /// <param name="typeBuilder">The type builder to initialize.</param>
    /// <param name="baseType">The base type of the type currently under construction.</param>
    public static void InitializeTypeBuilderFromType(TypeBuilder typeBuilder, Type baseType)
    {
        ArgumentNullException.ThrowIfNull(typeBuilder);
        ArgumentNullException.ThrowIfNull(baseType);

        // Inherit expected base type
        if (baseType.IsInterface)
            typeBuilder.AddInterfaceImplementation(baseType);
        else
            typeBuilder.SetParent(baseType);

        // Build list of class hierarchy (from deeper to closer)
        var currentType = baseType;
        var abstractBaseTypes = new List<Type>();
        while (currentType != null)
        {
            abstractBaseTypes.Add(currentType);
            currentType = currentType.BaseType;
        }
        abstractBaseTypes.Reverse();

        // Check that all interfaces are implemented
        var interfaceMethods = new List<MethodInfo>();
        foreach (var @interface in baseType.GetInterfaces())
        {
            interfaceMethods.AddRange(@interface.GetMethods(BindingFlags.Public | BindingFlags.Instance));
        }

        // Build list of abstract methods
        var abstractMethods = new List<MethodInfo>();
        foreach (var currentBaseType in abstractBaseTypes)
        {
            foreach (var method in currentBaseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if ((method.Attributes & MethodAttributes.Abstract) != 0)
                {
                    // abstract: add it
                    abstractMethods.Add(method);
                }
                else if ((method.Attributes & MethodAttributes.Virtual) != 0 && (method.Attributes & MethodAttributes.NewSlot) == 0)
                {
                    // override: check if it overrides a previously described abstract method
                    for (var index = 0; index < abstractMethods.Count; index++)
                    {
                        var abstractMethod = abstractMethods[index];
                        if (abstractMethod.Name == method.Name && CompareMethodSignature(abstractMethod, method))
                        {
                            // Found a match, let's remove it from list of method to reimplement
                            abstractMethods.RemoveAt(index);
                            break;
                        }
                    }
                }

                // Remove interface methods already implemented
                // override: check if it overrides a previously described abstract method
                for (var index = 0; index < interfaceMethods.Count; index++)
                {
                    var interfaceMethod = interfaceMethods[index];
                    if ((interfaceMethod.Name == method.Name
                         // explicit interface implementation
                         || $"{interfaceMethod.DeclaringType?.FullName}.{interfaceMethod.Name}" == method.Name)
                         && CompareMethodSignature(interfaceMethod, method))
                    {
                        // Found a match, let's remove it from list of method to reimplement
                        interfaceMethods.RemoveAt(index--);
                    }
                }
            }
        }

        // Note: It seems that C# also creates a Property/Event for each override; but it doesn't seem to fail when creating the type with only non-abstract getter/setter -- so we don't recreate the property/event
        // Implement all abstract methods
        foreach (var method in abstractMethods.Concat(interfaceMethods))
        {
            // Updates MethodAttributes for override method
            var attributes = method.Attributes;
            attributes &= ~MethodAttributes.Abstract;
            attributes &= ~MethodAttributes.NewSlot;
            attributes |= MethodAttributes.HideBySig;

            var overrideMethod = typeBuilder.DefineMethod(method.Name, attributes, method.CallingConvention, method.ReturnType, method.GetParameters().Select(x => x.ParameterType).ToArray());
            var overrideMethodIL = overrideMethod.GetILGenerator();

            // TODO: For properties, do we want get { return default(T); } set { } instead?
            //       And for events, add { } remove { } too?
            overrideMethodIL.ThrowException(typeof(NotImplementedException));
        }
    }

    /// <summary>
    /// Compares the parameter types of two <see cref="MethodInfo"/>.
    /// </summary>
    /// <param name="method1"></param>
    /// <param name="method2"></param>
    /// <returns><c>true</c> if the parameter types match one by one; otherwise, <c>false</c>.</returns>
    private static bool CompareMethodSignature(MethodInfo method1, MethodInfo method2)
    {
        var parameters1 = method1.GetParameters();
        var parameters2 = method2.GetParameters();

        if (parameters1.Length != parameters2.Length)
            return false;

        for (var i = 0; i < parameters1.Length; ++i)
        {
            if (parameters1[i].ParameterType != parameters2[i].ParameterType)
                return false;
        }

        return true;
    }
}
