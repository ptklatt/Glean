using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Extensions;
using Glean.Internal;
using Glean.Providers;
using Glean.Signatures;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for TypeDefinition.
/// Wraps MetadataReader + TypeDefinitionHandle + cached TypeDefinition struct.
/// </summary>
/// <remarks>
/// This is the primary fast tier type API. It keeps both the raw
/// <see cref="MetadataReader"/> and <see cref="TypeDefinition"/> visible so callers can mix
/// convenience helpers with direct System.Reflection.Metadata access.
/// </remarks>
public readonly struct TypeContext : IEquatable<TypeContext>
{
    private readonly MetadataReader _reader;
    private readonly TypeDefinitionHandle _handle;
    private readonly TypeDefinition _definition;

    /// <summary>
    /// Creates a <see cref="TypeContext"/> for a <see cref="TypeDefinitionHandle"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeContext Create(MetadataReader reader, TypeDefinitionHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader));}
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new TypeContext(reader, handle, reader.GetTypeDefinition(handle));
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TypeContext UnsafeCreate(MetadataReader reader, TypeDefinitionHandle handle)
        => new TypeContext(reader, handle, reader.GetTypeDefinition(handle));

    private TypeContext(MetadataReader reader, TypeDefinitionHandle handle, TypeDefinition definition)
    {
        _reader = reader;
        _handle = handle;
        _definition = definition;
    }

    public TypeDefinitionHandle Handle => _handle;
    public TypeDefinition Definition => _definition;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Name);
    }

    /// <summary>
    /// Gets the type name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _definition.Name;

    /// <summary>
    /// Gets the namespace.
    /// </summary>
    public string Namespace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Namespace);
    }

    /// <summary>
    /// Gets the namespace handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NamespaceHandle => _definition.Namespace;

    /// <summary>
    /// Gets the full type name (Namespace.Name).
    /// </summary>
    /// <remarks>
    /// Allocates: 2-3 strings. For zero allocation identity checks use <see cref="NameIs"/> and <see cref="NamespaceIs"/>.
    /// </remarks>
    public string FullName
    {
        get
        {
            var ns = Namespace;
            var name = Name;
            return string.IsNullOrEmpty(ns) ? name : string.Concat(ns, ".", name);
        }
    }

    /// <summary>
    /// Checks whether the type name matches the specified name handle.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer <see cref="TypeDefinitionExtensions.NameIs(TypeDefinition, StringHandle)"/> on <see cref="Definition"/>
    /// to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(StringHandle nameHandle)
    {
        return _definition.Name == nameHandle;
    }

    /// <summary>
    /// Checks whether the type name matches the specified name string.
    /// </summary>
    /// <remarks>
    /// Zero allocation: uses the reader's handle based <c>MetadataStringComparer</c>.
    /// Prefer this over <c>Name == name</c>, which allocates a string.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(string name)
    {
        return _reader.StringComparer.Equals(_definition.Name, name);
    }

    /// <summary>
    /// Checks whether the namespace matches the specified namespace handle.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer <see cref="TypeDefinitionExtensions.NamespaceIs(TypeDefinition, StringHandle)"/> on <see cref="Definition"/>
    /// to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NamespaceIs(StringHandle namespaceHandle)
    {
        return _definition.Namespace == namespaceHandle;
    }

    /// <summary>
    /// Checks whether the namespace matches the specified namespace string.
    /// </summary>
    /// <remarks>
    /// Zero allocation: uses the reader's handle based <c>MetadataStringComparer</c>.
    /// Prefer this over <c>Namespace == ns</c>, which allocates a string.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NamespaceIs(string ns)
    {
        return _reader.StringComparer.Equals(_definition.Namespace, ns);
    }

    /// <summary>
    /// Checks whether namespace and name match the specified handles.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer <see cref="TypeDefinitionExtensions.Is(TypeDefinition, StringHandle, StringHandle)"/> on <see cref="Definition"/>
    /// to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Is(StringHandle namespaceHandle, StringHandle nameHandle)
    {
        return (_definition.Namespace == namespaceHandle) && (_definition.Name == nameHandle);
    }

    /// <summary>
    /// Zero allocation identity check using the reader's handle based <c>MetadataStringComparer</c>.
    /// </summary>
    /// <remarks>Equivalent to <c>NamespaceIs(ns) &amp;&amp; NameIs(name)</c> in a single call.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Is(string ns, string name)
        => _reader.StringComparer.Equals(_definition.Namespace, ns)
        && _reader.StringComparer.Equals(_definition.Name, name);

    /// <summary>
    /// Gets the type attributes.
    /// </summary>
    public TypeAttributes Attributes => _definition.Attributes;

    /// <summary>
    /// Gets the base type handle.
    /// </summary>
    public EntityHandle BaseType => _definition.BaseType;

    /// <summary>
    /// Gets the namespace definition handle.
    /// </summary>
    public NamespaceDefinitionHandle NamespaceDefinition => _definition.NamespaceDefinition;

    /// <summary>
    /// Checks if the type is a top level public type (not nested). Returns <c>false</c> for nested public types.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="Extensions.TypeDefinitionExtensions.IsPublic"/>. Both are equivalent.
    /// Use <see cref="IsNestedPublic"/> for nested types.
    /// </remarks>
    public bool IsPublic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsPublic();
    }

    /// <summary>
    /// Checks if the type is nested public.
    /// </summary>
    public bool IsNestedPublic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsNestedPublic();
    }

    /// <summary>
    /// Checks if the type is nested (any nested visibility).
    /// </summary>
    public bool IsNested
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsNested();
    }

    /// <summary>
    /// Checks if the type is an interface.
    /// </summary>
    public bool IsInterface
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsInterface();
    }

    /// <summary>
    /// Checks if the type is abstract.
    /// </summary>
    public bool IsAbstract
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsAbstract();
    }

    /// <summary>
    /// Checks if the type is sealed.
    /// </summary>
    public bool IsSealed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsSealed();
    }

    /// <summary>
    /// Checks if the type is a static class (abstract + sealed).
    /// </summary>
    public bool IsStaticClass
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsStaticClass();
    }

    /// <summary>
    /// Checks if the type is a value type.
    /// </summary>
    public bool IsValueType
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsValueType(_reader);
    }

    /// <summary>
    /// Checks if the type is an enum.
    /// </summary>
    public bool IsEnum
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsEnum(_reader);
    }

    /// <summary>
    /// Checks if the type is a delegate.
    /// </summary>
    public bool IsDelegate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsDelegate(_reader);
    }

    /// <summary>
    /// True if this type is marked with <c>[CompilerGenerated]</c>.
    /// </summary>
    public bool IsCompilerGenerated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => HasAttribute(WellKnownTypes.SystemRuntimeCompilerServicesNs, WellKnownTypes.CompilerGeneratedAttribute);
    }

    /// <summary>
    /// Decodes the underlying type of this enum (the type of <c>value__</c>).
    /// </summary>
    /// <returns>The decoded underlying type signature, or <c>null</c> if this type is not an enum.</returns>
    /// <remarks>
    /// Allocates: decodes the field signature into a <see cref="TypeSignature"/> object graph.
    /// For simple enum checks that do not require the full signature, prefer inspecting
    /// <see cref="FieldContext.DecodeSignature{TType,TContext}"/> with a lightweight provider.
    /// </remarks>
    public TypeSignature? DecodeEnumUnderlyingType()
    {
        if (!IsEnum) { return null; }

        foreach (var field in EnumerateFields())
        {
            if (field.NameIs("value__"))
            {
                return field.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
            }
        }

        return null;
    }

    /// <summary>
    /// True if this type has at least one generic type parameter (e.g., <c>List&lt;T&gt;</c>).
    /// </summary>
    /// <remarks>Equivalent to <c>Definition.GetGenericParameters().Count &gt; 0</c>.</remarks>
    public bool IsGenericTypeDefinition
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.GetGenericParameters().Count > 0;
    }

    /// <summary>
    /// Gets the declaring type of this nested type.
    /// Returns an invalid (default) context if this type is not nested.
    /// </summary>
    public TypeContext GetDeclaringType()
    {
        if (!IsNested) { return default; }

        var handle = _definition.GetDeclaringType();
        return handle.IsNil ? default : TypeContext.Create(_reader, handle);
    }

    /// <summary>
    /// Enumerates this type's methods.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodEnumerator EnumerateMethods()
    {
        return MethodEnumerator.Create(_reader, _definition.GetMethods());
    }

    /// <summary>
    /// Enumerates this type's fields.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FieldEnumerator EnumerateFields()
    {
        return FieldEnumerator.Create(_reader, _definition.GetFields());
    }

    /// <summary>
    /// Enumerates this type's properties.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyEnumerator EnumerateProperties()
    {
        return PropertyEnumerator.Create(_reader, _definition.GetProperties());
    }

    /// <summary>
    /// Enumerates generic parameters (e.g., T in class Foo&lt;T&gt;).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GenericParameterEnumerator EnumerateGenericParameters()
    {
        return GenericParameterEnumerator.Create(_reader, _definition.GetGenericParameters());
    }

    /// <summary>
    /// Enumerates this type's custom attributes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CustomAttributeEnumerator EnumerateCustomAttributes()
    {
        return CustomAttributeEnumerator.Create(_reader, _definition.GetCustomAttributes());
    }

    /// <summary>
    /// Enumerates only attributes whose type matches the specified namespace and name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerators.FilteredCustomAttributeEnumerator EnumerateAttributes(string ns, string name)
    {
        return Enumerators.FilteredCustomAttributeEnumerator.Create(_reader, _definition.GetCustomAttributes(), ns, name);
    }

    /// <summary>
    /// Enumerates nested types defined within this type.
    /// </summary>
    /// <remarks>
    /// Iterates only this type's nested types - more efficient than scanning the TypeDefinitions table and checking <see cref="IsNested"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerators.NestedTypeEnumerator EnumerateNestedTypes()
    {
        return Enumerators.NestedTypeEnumerator.Create(_reader, _definition.GetNestedTypes());
    }

    /// <summary>
    /// Enumerates method implementations (explicit interface implementations) for this type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerators.MethodImplementationEnumerator EnumerateMethodImplementations()
    {
        return Enumerators.MethodImplementationEnumerator.Create(_reader, _definition.GetMethodImplementations());
    }

    /// <summary>
    /// Enumerates implemented interfaces decoded as TypeSignatures.
    /// </summary>
    /// <remarks>
    /// Rich tier: decoding each interface entry into a <see cref="Signatures.TypeSignature"/> allocates.
    /// For fast tier (handle only) interface enumeration, use <see cref="EnumerateInterfaceTypes"/> instead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerators.InterfaceEnumerator EnumerateInterfaces()
        => _definition.GetImplementedInterfaces(_reader);

    /// <summary>
    /// Enumerates implemented interface type handles without signature decoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerators.InterfaceTypeHandleEnumerator EnumerateInterfaceTypes()
        => _definition.GetImplementedInterfaceTypes(_reader);

    /// <summary>
    /// Returns <c>true</c> if this type's InterfaceImpl table contains a direct entry for the specified interface.
    /// Matches <see cref="HandleKind.TypeReference"/> and <see cref="HandleKind.TypeDefinition"/> entries;
    /// returns <c>false</c> for generic instantiations (<see cref="HandleKind.TypeSpecification"/>).
    /// Does not walk inherited interfaces or cross assembly boundaries.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ImplementsInterfaceDirectly(string ns, string name)
        => _definition.ImplementsInterfaceDirectly(_reader, ns, name);

    /// <summary>
    /// Enumerates all attributes matching <paramref name="ns"/> and <paramref name="name"/> across this type
    /// and all its members (methods, fields, properties, events).
    /// </summary>
    /// <param name="ns">The attribute namespace (e.g., <c>"System.Runtime.CompilerServices"</c>).</param>
    /// <param name="name">The attribute type name (e.g., <c>"ObsoleteAttribute"</c>).</param>
    /// <returns>A zero allocation struct enumerator that yields each matching <see cref="CustomAttributeContext"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AllMemberAttributeEnumerator EnumerateAllAttributes(string ns, string name)
    {
        return AllMemberAttributeEnumerator.Create(this, ns, name);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _definition.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    /// <summary>Finds a custom attribute by namespace and name; returns the context if found.</summary>
    /// <remarks>
    /// Zero allocation. 
    /// </remarks>
    public bool TryFindAttribute(string ns, string name, out CustomAttributeContext attribute)
    {
        var attributes = _definition.GetCustomAttributes();
        if (!attributes.TryFindAttributeHandle(_reader, ns, name, out var handle))
        {
            attribute = default;
            return false;
        }

        attribute = CustomAttributeContext.Create(_reader, handle);
        return true;
    }

    /// <summary>
    /// Compares two contexts for equality based on handle and reader reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TypeContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is TypeContext other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TypeContext left, TypeContext right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TypeContext left, TypeContext right)
    {
        return !left.Equals(right);
    }

}
