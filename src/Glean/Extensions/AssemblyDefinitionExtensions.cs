using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="AssemblyDefinition"/>.
/// </summary>
public static class AssemblyDefinitionExtensions
{
    // == Flag checks =========================================================

    /// <summary>
    /// Checks whether the assembly has <c>[ReferenceAssembly]</c>.
    /// </summary>
    /// <remarks>
    /// This checks for <c>System.Runtime.CompilerServices.ReferenceAssemblyAttribute</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceAssembly(this AssemblyDefinition assembly, MetadataReader reader)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }

        return assembly.GetCustomAttributes()
            .TryFindAttribute(reader, WellKnownTypes.SystemRuntimeCompilerServicesNs, "ReferenceAssemblyAttribute", out _);
    }

    /// <summary>
    /// Checks if the assembly has a public key (strong-named).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasPublicKey(this AssemblyDefinition assembly)
        => !assembly.PublicKey.IsNil;

    /// <summary>
    /// Checks if the assembly is retargetable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRetargetable(this AssemblyDefinition assembly)
        => (assembly.Flags & AssemblyFlags.Retargetable) != 0;

    /// <summary>
    /// Gets whether the assembly disables the JIT compile optimizer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsJitCompileOptimizerDisabled(this AssemblyDefinition assembly)
        => (assembly.Flags & AssemblyFlags.DisableJitCompileOptimizer) != 0;

    /// <summary>
    /// Gets whether the assembly enables JIT compile tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsJitCompileTrackingEnabled(this AssemblyDefinition assembly)
        => (assembly.Flags & AssemblyFlags.EnableJitCompileTracking) != 0;

    // === Content type =====================================================

    /// <summary>
    /// Checks if the assembly content type is default.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDefaultContentType(this AssemblyDefinition assembly)
        => (assembly.Flags & AssemblyFlags.ContentTypeMask) == (AssemblyFlags)0;

    /// <summary>
    /// Checks if the assembly content type is WindowsRuntime.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWindowsRuntime(this AssemblyDefinition assembly)
        => (assembly.Flags & AssemblyFlags.ContentTypeMask) == (AssemblyFlags)0x00000200;

    // == Metadata access =====================================================

    /// <summary>
    /// Gets the assembly name string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetName(this AssemblyDefinition assembly, MetadataReader reader)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }

        return reader.GetString(assembly.Name);
    }

    /// <summary>
    /// Gets the assembly culture string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetCulture(this AssemblyDefinition assembly, MetadataReader reader)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (assembly.Culture.IsNil) { return string.Empty; }

        return reader.GetString(assembly.Culture);
    }

    /// <summary>
    /// Gets the assembly public key bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GetPublicKey(this AssemblyDefinition assembly, MetadataReader reader)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (assembly.PublicKey.IsNil) { return Array.Empty<byte>(); }

        return reader.GetBlobBytes(assembly.PublicKey);
    }

    /// <summary>
    /// Gets the assembly public key as a zero allocation span.
    /// </summary>
    /// <remarks>
    /// The returned span is valid as long as the <paramref name="reader"/> is alive.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<byte> GetPublicKeySpan(this AssemblyDefinition assembly, MetadataReader reader)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (assembly.PublicKey.IsNil) { return ReadOnlySpan<byte>.Empty; }

        var blobReader = reader.GetBlobReader(assembly.PublicKey);
        return new ReadOnlySpan<byte>(blobReader.StartPointer, blobReader.Length);
    }

    /// <summary>
    /// Checks if the assembly name matches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this AssemblyDefinition assembly, MetadataReader reader, string name)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }

        return reader.StringComparer.Equals(assembly.Name, name);
    }
}
