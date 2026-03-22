using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Internal;

// Disable obsolete warning for enums as this is a usability extension
#pragma warning disable SYSLIB0050

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="TypeDefinition"/>.
/// Primarily fast tier flag/handle checks with explicit rich formatting/decode helpers.
/// </summary>
public static class TypeDefinitionExtensions
{
    // == Visibility checks ===================================================

    /// <summary>
    /// Checks if the type is public (top level public).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
    }

    /// <summary>
    /// Checks if the type is internal (assembly internal, not nested).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternal(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NotPublic;
    }

    /// <summary>
    /// Checks if the type is nested public.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedPublic(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic;
    }

    /// <summary>
    /// Checks if the type is nested private.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedPrivate(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPrivate;
    }

    /// <summary>
    /// Checks if the type is nested family (protected).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedFamily(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedFamily;
    }

    /// <summary>
    /// Checks if the type is nested assembly (internal).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedAssembly(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedAssembly;
    }

    /// <summary>
    /// Checks if the type is nested (any nested visibility).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNested(this TypeDefinition type)
    {
        var visibility = type.Attributes & TypeAttributes.VisibilityMask;
        return (visibility >= TypeAttributes.NestedPublic) && (visibility <= TypeAttributes.NestedFamORAssem);
    }

    // == Type kind checks ====================================================

    /// <summary>
    /// Checks if the type is an interface.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInterface(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.Interface) != 0;
    }

    /// <summary>
    /// Checks if the type is abstract.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAbstract(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.Abstract) != 0;
    }

    /// <summary>
    /// Checks if the type is sealed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSealed(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.Sealed) != 0;
    }

    /// <summary>
    /// Checks if the type is a static class (abstract + sealed).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStaticClass(this TypeDefinition type)
    {
        const TypeAttributes staticMask = TypeAttributes.Abstract | TypeAttributes.Sealed;
        return (type.Attributes & staticMask) == staticMask;
    }

    /// <summary>
    /// True if this type has at least one generic type parameter (e.g., <c>List&lt;T&gt;</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGenericTypeDefinition(this TypeDefinition type)
        => type.GetGenericParameters().Count > 0;

    /// <summary>
    /// Checks if the type is a value type by examining base type.
    /// </summary>
    /// <param name="type">The type definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the type inherits from System.ValueType or System.Enum.</returns>
    public static bool IsValueType(this TypeDefinition type, MetadataReader reader)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        // System.ValueType and System.Enum are reference types.
        if (reader.StringComparer.Equals(type.Namespace, WellKnownTypes.SystemNs) && 
           (reader.StringComparer.Equals(type.Name, WellKnownTypes.ValueType) ||
            reader.StringComparer.Equals(type.Name, WellKnownTypes.Enum)))
        {
            return false;
        }

        var baseType = type.BaseType;
        if (baseType.IsNil) { return false; }

        if (baseType.Kind == HandleKind.TypeReference)
        {
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)baseType);
            return reader.StringComparer.Equals(typeRef.Namespace, WellKnownTypes.SystemNs) &&
                  (reader.StringComparer.Equals(typeRef.Name, WellKnownTypes.ValueType) ||
                   reader.StringComparer.Equals(typeRef.Name, WellKnownTypes.Enum));
        }

        // Handle TypeDefinition base types (e.g., in CoreLib where ValueType is defined locally)
        if (baseType.Kind == HandleKind.TypeDefinition)
        {
            var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)baseType);
            return reader.StringComparer.Equals(typeDef.Namespace, WellKnownTypes.SystemNs) &&
                  (reader.StringComparer.Equals(typeDef.Name, WellKnownTypes.ValueType) ||
                   reader.StringComparer.Equals(typeDef.Name, WellKnownTypes.Enum));
        }

        return false;
    }

    /// <summary>
    /// Checks if the type is an enum by examining base type.
    /// </summary>
    /// <param name="type">The type definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the type inherits from System.Enum.</returns>
    public static bool IsEnum(this TypeDefinition type, MetadataReader reader)
    {
        var baseType = type.BaseType;
        if (baseType.IsNil) { return false; }

        if (baseType.Kind == HandleKind.TypeReference)
        {
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)baseType);
            return reader.StringComparer.Equals(typeRef.Namespace, WellKnownTypes.SystemNs) &&
                   reader.StringComparer.Equals(typeRef.Name, WellKnownTypes.Enum);
        }

        // Handle TypeDefinition base types (e.g., in CoreLib where Enum is defined locally)
        if (baseType.Kind == HandleKind.TypeDefinition)
        {
            var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)baseType);
            return reader.StringComparer.Equals(typeDef.Namespace, WellKnownTypes.SystemNs) &&
                   reader.StringComparer.Equals(typeDef.Name, WellKnownTypes.Enum);
        }

        return false;
    }

    /// <summary>
    /// Checks if the type is a delegate by examining base type.
    /// </summary>
    /// <param name="type">The type definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the type inherits from System.Delegate or System.MulticastDelegate.</returns>
    public static bool IsDelegate(this TypeDefinition type, MetadataReader reader)
    {
        var baseType = type.BaseType;
        if (baseType.IsNil) { return false; }

        if (baseType.Kind == HandleKind.TypeReference)
        {
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)baseType);
            return reader.StringComparer.Equals(typeRef.Namespace, WellKnownTypes.SystemNs) &&
                  (reader.StringComparer.Equals(typeRef.Name, WellKnownTypes.Delegate) ||
                   reader.StringComparer.Equals(typeRef.Name, WellKnownTypes.MulticastDelegate));
        }

        if (baseType.Kind == HandleKind.TypeDefinition)
        {
            var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)baseType);
            return reader.StringComparer.Equals(typeDef.Namespace, WellKnownTypes.SystemNs) &&
                  (reader.StringComparer.Equals(typeDef.Name, WellKnownTypes.Delegate) ||
                   reader.StringComparer.Equals(typeDef.Name, WellKnownTypes.MulticastDelegate));
        }

        return false;
    }

    // == Special attributes ==================================================

    /// <summary>
    /// Checks if the type has a special name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.SpecialName) != 0;
    }

    /// <summary>
    /// Checks if the type is serializable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSerializable(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.Serializable) != 0;
    }

    /// <summary>
    /// Checks if the type is imported (extern).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsImport(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.Import) != 0;
    }

    /// <summary>
    /// Checks if the type has explicit layout.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsExplicitLayout(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.LayoutMask) == TypeAttributes.ExplicitLayout;
    }

    /// <summary>
    /// Checks if the type has sequential layout.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSequentialLayout(this TypeDefinition type)
    {
        return (type.Attributes & TypeAttributes.LayoutMask) == TypeAttributes.SequentialLayout;
    }

    // == Identity checks =====================================================

    /// <summary>
    /// Checks if the type matches the specified namespace and name.
    /// Zero allocation identity check using MetadataReader.StringComparer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Is(this TypeDefinition type, MetadataReader reader, string ns, string name)
    {
        return reader.StringComparer.Equals(type.Namespace, ns) &&
               reader.StringComparer.Equals(type.Name, name);
    }

    /// <summary>
    /// Checks if the type matches the specified namespace and name handles.
    /// Fast path identity check with no string materialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Is(this TypeDefinition type, StringHandle nameSpaceHandle, StringHandle nameHandle)
    {
        return (type.Namespace == nameSpaceHandle) && (type.Name == nameHandle);
    }

    /// <summary>
    /// Checks if the type name matches (ignores namespace).
    /// Zero allocation identity check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this TypeDefinition type, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(type.Name, name);
    }

    /// <summary>
    /// Checks if the type name matches the specified name handle.
    /// Fast path identity check with no string materialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this TypeDefinition type, StringHandle nameHandle)
    {
        return type.Name == nameHandle;
    }

    /// <summary>
    /// Checks if the type namespace matches the specified namespace handle.
    /// Fast path identity check with no string materialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NamespaceIs(this TypeDefinition type, StringHandle nameSpaceHandle)
    {
        return type.Namespace == nameSpaceHandle;
    }

    /// <summary>
    /// Formats the type full name as a managed string.
    /// This is a rich tier formatting helper and allocates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToFullNameString(this TypeDefinition type, MetadataReader reader)
    {
        var ns = reader.GetString(type.Namespace);
        var name = reader.GetString(type.Name);
        return string.IsNullOrEmpty(ns) ? name : string.Concat(ns, ".", name);
    }

    // == Metadata access =====================================================

    /// <summary>
    /// Gets implemented interfaces decoded as TypeSignatures.
    /// Uses Signatures infrastructure for advanced type analysis.
    /// Decoding each interface signature may allocate.
    /// </summary>
    /// <remarks>
    /// Rich tier: each enumerated item is a decoded <see cref="Signatures.TypeSignature"/> and allocates.
    /// For fast tier (handle only) interface enumeration, use <see cref="GetImplementedInterfaceTypes"/> instead.
    /// </remarks>
    public static Enumerators.InterfaceEnumerator GetImplementedInterfaces(
        this TypeDefinition type,
        MetadataReader reader)
    {
        var provider = Providers.SignatureTypeProvider.Instance;
        var genericContext = Providers.SignatureDecodeContext.Empty;

        return Enumerators.InterfaceEnumerator.Create(
            reader,
            type.GetInterfaceImplementations(),
            provider,
            genericContext);
    }

    /// <summary>
    /// Gets implemented interfaces decoded as TypeSignatures with a custom provider.
    /// Decoding each interface signature may allocate.
    /// </summary>
    /// <remarks>
    /// Rich tier: each enumerated item is a decoded <see cref="Signatures.TypeSignature"/> and allocates.
    /// For fast tier (handle only) interface enumeration, use <see cref="GetImplementedInterfaceTypes"/> instead.
    /// </remarks>
    public static Enumerators.InterfaceEnumerator GetImplementedInterfaces(
        this TypeDefinition type,
        MetadataReader reader,
        ISignatureTypeProvider<Signatures.TypeSignature, Providers.SignatureDecodeContext> provider,
        Providers.SignatureDecodeContext genericContext)
    {
        return Enumerators.InterfaceEnumerator.Create(
            reader,
            type.GetInterfaceImplementations(),
            provider,
            genericContext);
    }

    /// <summary>
    /// Gets implemented interface type handles without decoding signatures.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Enumerators.InterfaceTypeHandleEnumerator GetImplementedInterfaceTypes(
        this TypeDefinition type,
        MetadataReader reader)
    {
        return Enumerators.InterfaceTypeHandleEnumerator.Create(reader, type.GetInterfaceImplementations());
    }
    
    /// <summary>
    /// Checks whether the type directly implements the specified interface without decoding signatures.
    /// This is a fast path intended for direct System.Reflection.Metadata parity scenarios.
    /// </summary>
    /// <remarks>
    /// Checks only this type's direct InterfaceImpl entries. Does not check inherited interfaces.
    /// This method matches interfaces by <c>(namespace, name)</c> on <see cref="TypeReference"/> or
    /// <see cref="TypeDefinition"/> handles in the InterfaceImpl table. It intentionally returns false
    /// for <see cref="HandleKind.TypeSpecification"/> (e.g., generic instantiations), since decoding
    /// those requires signature processing.
    /// </remarks>
    public static bool ImplementsInterfaceDirectly(
        this TypeDefinition type,
        MetadataReader reader,
        string ns,
        string name)
    {
        foreach (var ifaceHandle in type.GetInterfaceImplementations())
        {
            var ifaceImpl = reader.GetInterfaceImplementation(ifaceHandle);
            var ifaceType = ifaceImpl.Interface;

            if (ifaceType.Kind == HandleKind.TypeReference)
            {
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)ifaceType);
                if (reader.StringComparer.Equals(typeRef.Namespace, ns) &&
                    reader.StringComparer.Equals(typeRef.Name, name))
                {
                    return true;
                }

                continue;
            }

            if (ifaceType.Kind == HandleKind.TypeDefinition)
            {
                var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)ifaceType);
                if (reader.StringComparer.Equals(typeDef.Namespace, ns) &&
                    reader.StringComparer.Equals(typeDef.Name, name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the base type as a TypeSignature (if not System.Object).
    /// </summary>
    /// <remarks>
    /// Rich tier: decodes the base type into a <see cref="Signatures.TypeSignature"/> object graph and allocates.
    /// For the base type name as a plain string, use <see cref="GetBaseTypeName"/>.
    /// For fast tier handle access, inspect <see cref="TypeDefinition.BaseType"/> directly.
    /// </remarks>
    public static Signatures.TypeSignature? GetBaseTypeSignature(
        this TypeDefinition type,
        MetadataReader reader)
    {
        if (type.BaseType.IsNil) { return null; }

        var provider = Providers.SignatureTypeProvider.Instance;
        var genericContext = Providers.SignatureDecodeContext.Empty;
        return type.BaseType.DecodeTypeSignature(reader, provider, genericContext);
    }

    /// <summary>
    /// Gets the base type name as "Namespace.Name" (or just "Name" if no namespace).
    /// Returns null if the type has no base type (System.Object itself).
    /// </summary>
    public static string? GetBaseTypeName(this TypeDefinition type, MetadataReader reader)
    {
        var baseType = type.BaseType;
        if (baseType.IsNil) { return null; }

        if (baseType.Kind == HandleKind.TypeReference)
        {
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)baseType);
            var ns = reader.GetString(typeRef.Namespace);
            var name = reader.GetString(typeRef.Name);
            return string.IsNullOrEmpty(ns) ? name : string.Concat(ns, ".", name);
        }

        if (baseType.Kind == HandleKind.TypeDefinition)
        {
            var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)baseType);
            var ns = reader.GetString(typeDef.Namespace);
            var name = reader.GetString(typeDef.Name);
            return string.IsNullOrEmpty(ns) ? name : string.Concat(ns, ".", name);
        }

        return null;
    }
}
