using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Extensions;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for AssemblyDefinition.
/// </summary>
public readonly struct AssemblyContext : IEquatable<AssemblyContext>
{
    private readonly MetadataReader _reader;
    private readonly AssemblyDefinition _definition;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssemblyContext Create(MetadataReader reader)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (!reader.IsAssembly) { throw new ArgumentException("MetadataReader does not represent an assembly.", nameof(reader)); }

        return new AssemblyContext(reader, reader.GetAssemblyDefinition());
    }

    private AssemblyContext(MetadataReader reader, AssemblyDefinition definition)
    {
        _reader = reader;
        _definition = definition;
    }

    public AssemblyDefinition Definition => _definition;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null;
    }

    /// <summary>
    /// Gets the assembly name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Name);
    }

    /// <summary>
    /// Gets the assembly culture.
    /// </summary>
    public string Culture
    {
        get
        {
            var cultureHandle = _definition.Culture;
            return cultureHandle.IsNil ? string.Empty : _reader.GetString(cultureHandle);
        }
    }

    /// <summary>
    /// Gets the assembly version.
    /// </summary>
    public Version Version => _definition.Version;

    /// <summary>
    /// Gets the assembly flags.
    /// </summary>
    public AssemblyFlags Flags => _definition.Flags;

    /// <summary>
    /// Gets the assembly public key blob handle.
    /// </summary>
    public BlobHandle PublicKeyHandle => _definition.PublicKey;

    /// <summary>
    /// Gets the assembly hash algorithm.
    /// </summary>
    public AssemblyHashAlgorithm HashAlgorithm => _definition.HashAlgorithm;

    /// <summary>
    /// Enumerates all type definitions in this assembly.
    /// Returns struct enumerator for zero allocation iteration.
    /// </summary>
    /// <remarks>
    /// Includes all types (top level and nested). Nested types also appear
    /// independently in the TypeDef table; filter with <c>type.IsNested</c> if needed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeDefinitionEnumerator EnumerateTypes()
    {
        return TypeDefinitionEnumerator.Create(_reader, _reader.TypeDefinitions);
    }

    /// <summary>
    /// Enumerates this assembly's custom attributes.
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
    /// Enumerates exported types (type forwarders).
    /// Returns struct enumerator for zero allocation iteration.
    /// </summary>
    /// <remarks>
    /// Exported types are used for type forwarding, where a type that was previously defined
    /// in this assembly has been moved to another assembly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ExportedTypeEnumerator EnumerateExportedTypes()
    {
        return ExportedTypeEnumerator.Create(_reader);
    }

    /// <summary>
    /// Enumerates manifest resources embedded in or linked to the assembly.
    /// Returns struct enumerator for zero allocation iteration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ManifestResourceEnumerator EnumerateManifestResources()
    {
        return ManifestResourceEnumerator.Create(_reader);
    }

    /// <summary>
    /// Enumerates assembly references.
    /// Returns struct enumerator for zero allocation iteration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssemblyReferenceEnumerator EnumerateAssemblyReferences()
    {
        return AssemblyReferenceEnumerator.Create(_reader, _reader.AssemblyReferences);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _definition.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    /// <summary>Finds a custom attribute by namespace and name; returns the context if found.</summary>
    /// <remarks>Zero allocation.</remarks>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssemblyContext other)
    {
        // Assembly contexts are equal if they share the same reader
        return ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return (obj is AssemblyContext other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return _reader?.GetHashCode() ?? 0;
    }

    public static bool operator ==(AssemblyContext left, AssemblyContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AssemblyContext left, AssemblyContext right)
    {
        return !left.Equals(right);
    }
}
