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
/// Zero allocation context for MethodDefinition.
/// Wraps MetadataReader + MethodDefinitionHandle + cached MethodDefinition struct.
/// </summary>
public readonly struct MethodContext : IEquatable<MethodContext>
{
    private readonly MetadataReader _reader;
    private readonly MethodDefinitionHandle _handle;
    private readonly MethodDefinition _definition;

    /// <summary>
    /// Creates a <see cref="MethodContext"/> for a <see cref="MethodDefinitionHandle"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodContext Create(MetadataReader reader, MethodDefinitionHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new MethodContext(reader, handle, reader.GetMethodDefinition(handle));
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodContext UnsafeCreate(MetadataReader reader, MethodDefinitionHandle handle)
        => new MethodContext(reader, handle, reader.GetMethodDefinition(handle));

    private MethodContext(MetadataReader reader, MethodDefinitionHandle handle, MethodDefinition definition)
    {
        _reader = reader;
        _handle = handle;
        _definition = definition;
    }

    public MethodDefinitionHandle Handle => _handle;
    public MethodDefinition Definition => _definition;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the method name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Name);
    }

    /// <summary>
    /// Checks whether the method name matches the specified name string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(string name)
    {
        return _reader.StringComparer.Equals(_definition.Name, name);
    }

    /// <summary>
    /// Gets the method name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _definition.Name;

    /// <summary>
    /// Gets the method attributes.
    /// </summary>
    public MethodAttributes Attributes => _definition.Attributes;

    /// <summary>
    /// Gets the method implementation attributes.
    /// </summary>
    public MethodImplAttributes ImplAttributes => _definition.ImplAttributes;

    /// <summary>
    /// Gets the relative virtual address (RVA) of the method body. 0 for abstract or extern methods.
    /// </summary>
    public int RelativeVirtualAddress => _definition.RelativeVirtualAddress;

    /// <summary>
    /// Checks if the method is public.
    /// </summary>
    public bool IsPublic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsPublic();
    }

    /// <summary>
    /// Checks if the method is private.
    /// </summary>
    public bool IsPrivate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsPrivate();
    }

    /// <summary>
    /// Checks if the method is static.
    /// </summary>
    public bool IsStatic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsStatic();
    }

    /// <summary>
    /// Checks if the method is virtual.
    /// </summary>
    public bool IsVirtual
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsVirtual();
    }

    /// <summary>
    /// Checks if the method is abstract.
    /// </summary>
    public bool IsAbstract
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsAbstract();
    }

    /// <summary>
    /// Checks if the method is final (sealed).
    /// </summary>
    public bool IsFinal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsFinal();
    }

    /// <summary>
    /// Checks if the method is a constructor.
    /// </summary>
    public bool IsConstructor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsConstructor(_reader);
    }

    /// <summary>
    /// Checks if the method is a special name (property accessor, etc.).
    /// </summary>
    public bool IsSpecialName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.IsSpecialName();
    }

    /// <summary>
    /// True if this method has at least one generic type parameter (e.g., <c>void Foo&lt;T&gt;()</c>).
    /// </summary>
    /// <remarks>Equivalent to <c>Definition.GetGenericParameters().Count &gt; 0</c>.</remarks>
    public bool IsGenericMethodDefinition
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.GetGenericParameters().Count > 0;
    }

    /// <summary>
    /// True if this method is marked with <c>[CompilerGenerated]</c>.
    /// </summary>
    public bool IsCompilerGenerated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => HasAttribute(WellKnownTypes.SystemRuntimeCompilerServicesNs, WellKnownTypes.CompilerGeneratedAttribute);
    }

    /// <summary>
    /// Decodes and returns the return type of this method (allocates: signature decode).
    /// For the full signature, use <see cref="DecodeSignature{TType, TContext}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeSignature DecodeReturnType()
        => _definition.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty).ReturnType;

    /// <summary>
    /// Gets the declaring type context (computed on demand).
    /// </summary>
    public TypeContext DeclaringType
    {
        get
        {
            var declaringTypeHandle = _definition.GetDeclaringType();
            return TypeContext.Create(_reader, declaringTypeHandle);
        }
    }

    /// <summary>
    /// Enumerates this method's parameters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ParameterEnumerator EnumerateParameters()
    {
        return ParameterEnumerator.Create(_reader, _definition.GetParameters(), _handle);
    }

    /// <summary>
    /// Enumerates this method's generic parameters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GenericParameterEnumerator EnumerateGenericParameters()
    {
        return GenericParameterEnumerator.Create(_reader, _definition.GetGenericParameters());
    }
    
    /// <summary>
    /// Enumerates this method's custom attributes.
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
    public FilteredCustomAttributeEnumerator EnumerateAttributes(string ns, string name)
    {
        return FilteredCustomAttributeEnumerator.Create(_reader, _definition.GetCustomAttributes(), ns, name);
    }

    /// <summary>
    /// Decodes the method signature using the specified provider.
    /// </summary>
    /// <typeparam name="TType">The type representation returned by the provider.</typeparam>
    /// <typeparam name="TContext">The generic context type.</typeparam>
    /// <param name="provider">The signature type provider.</param>
    /// <param name="genericContext">The generic context for parameter substitution.</param>
    /// <returns>The decoded method signature.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodSignature<TType> DecodeSignature<TType, TContext>(
        ISignatureTypeProvider<TType, TContext> provider,
        TContext genericContext)
    {
        return _definition.DecodeSignature(provider, genericContext);
    }

    /// <summary>
    /// Decodes and returns a readable signature string like "void Main(string[])".
    /// </summary>
    /// <returns>A formatted method signature string.</returns>
    /// <remarks>
    /// Allocates strings. For zero allocation scenarios, use
    /// <see cref="DecodeSignature{TType, TContext}"/> with a custom provider.
    /// </remarks>
    public string DecodeSignatureString()
    {
        return StringTypeProvider.FormatMethodSignature(_reader, _definition, includeReturnType: true);
    }

    /// <summary>
    /// Decodes and returns a readable signature string with optional return type.
    /// </summary>
    /// <param name="includeReturnType">Whether to include the return type in the output.</param>
    /// <returns>A formatted method signature string.</returns>
    public string DecodeSignatureString(bool includeReturnType)
    {
        return StringTypeProvider.FormatMethodSignature(_reader, _definition, includeReturnType);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _definition.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    /// <summary>
    /// Compares two contexts for equality based on handle and reader reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MethodContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is MethodContext other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(MethodContext left, MethodContext right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(MethodContext left, MethodContext right)
    {
        return !left.Equals(right);
    }
}
