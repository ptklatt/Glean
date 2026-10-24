using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Extensions;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for <see cref="ModuleDefinition"/>.
/// </summary>
/// <remarks>
/// This is the primary fast tier module API. It keeps the underlying
/// <see cref="MetadataReader"/> visible via <see cref="Reader"/> so callers can mix
/// convenience helpers with direct System.Reflection.Metadata access.
/// </remarks>
public readonly struct ModuleContext : IEquatable<ModuleContext>
{
    private readonly MetadataReader _reader;
    private readonly ModuleDefinition _definition;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ModuleContext Create(MetadataReader reader)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        return new ModuleContext(reader, reader.GetModuleDefinition());
    }

    private ModuleContext(MetadataReader reader, ModuleDefinition definition)
    {
        _reader = reader;
        _definition = definition;
    }

    public ModuleDefinition Definition => _definition;

    /// <summary>
    /// Gets the raw metadata reader for System.Reflection.Metadata native access.
    /// </summary>
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null;
    }

    /// <summary>
    /// Gets the module name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Name);
    }

    /// <summary>
    /// Gets the name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _definition.Name;

    /// <summary>
    /// Gets the module generation.
    /// </summary>
    public int Generation => _definition.Generation;

    /// <summary>
    /// Gets the module version ID handle.
    /// </summary>
    public GuidHandle MvidHandle => _definition.Mvid;

    /// <summary>
    /// Gets the module version ID.
    /// </summary>
    public Guid Mvid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetGuid(_definition.Mvid);
    }

    /// <summary>
    /// Gets the generation ID handle.
    /// </summary>
    public GuidHandle GenerationIdHandle => _definition.GenerationId;

    /// <summary>
    /// Gets the generation ID when present.
    /// </summary>
    public Guid? GenerationId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.GenerationId.IsNil ? null : _reader.GetGuid(_definition.GenerationId);
    }

    /// <summary>
    /// Gets the base generation ID handle.
    /// </summary>
    public GuidHandle BaseGenerationIdHandle => _definition.BaseGenerationId;

    /// <summary>
    /// Gets the base generation ID when present.
    /// </summary>
    public Guid? BaseGenerationId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.BaseGenerationId.IsNil ? null : _reader.GetGuid(_definition.BaseGenerationId);
    }

    /// <summary>
    /// Gets whether this is the manifest module for an assembly.
    /// </summary>
    public bool IsManifestModule
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.IsAssembly;
    }

    /// <summary>
    /// Enumerates all type definitions in this module.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeDefinitionEnumerator EnumerateTypes()
    {
        return TypeDefinitionEnumerator.Create(_reader, _reader.TypeDefinitions);
    }

    /// <summary>
    /// Enumerates all type references in this module.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeReferenceEnumerator EnumerateTypeReferences()
    {
        return TypeReferenceEnumerator.Create(_reader, _reader.TypeReferences);
    }

    /// <summary>
    /// Enumerates all member references in this module.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MemberReferenceEnumerator EnumerateMemberReferences()
    {
        return MemberReferenceEnumerator.Create(_reader, _reader.MemberReferences);
    }

    /// <summary>
    /// Enumerates all assembly references in this module.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssemblyReferenceEnumerator EnumerateAssemblyReferences()
    {
        return AssemblyReferenceEnumerator.Create(_reader, _reader.AssemblyReferences);
    }

    /// <summary>
    /// Enumerates all module references in this module.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ModuleReferenceEnumerator EnumerateModuleReferences()
    {
        return ModuleReferenceEnumerator.Create(_reader);
    }

    /// <summary>
    /// Enumerates this module's custom attributes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CustomAttributeEnumerator EnumerateCustomAttributes()
    {
        return CustomAttributeEnumerator.Create(_reader, _definition.GetCustomAttributes());
    }

    /// <summary>
    /// Finds a type definition by namespace and name.
    /// </summary>
    public bool TryFindType(string ns, string name, out TypeContext type)
    {
        foreach (var candidate in EnumerateTypes())
        {
            if (!candidate.Is(ns, name))
            {
                continue;
            }

            type = candidate;
            return true;
        }

        type = default;
        return false;
    }

    /// <summary>
    /// Returns true if a custom attribute of the specified type is present.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _definition.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    /// <summary>
    /// Finds a custom attribute by namespace and name and returns the context if found.
    /// </summary>
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
    public bool Equals(ModuleContext other)
    {
        return ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return (obj is ModuleContext other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return _reader?.GetHashCode() ?? 0;
    }

    public static bool operator ==(ModuleContext left, ModuleContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ModuleContext left, ModuleContext right)
    {
        return !left.Equals(right);
    }
}
