using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for accessing IL method body and local variables.
/// </summary>
public static partial class MethodDefinitionExtensions
{
    /// <summary>
    /// Gets the method body containing IL bytes and metadata.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>The method body, or default if the method has no body (abstract, extern, etc.).</returns>
    /// <remarks>
    /// This method returns the raw IL bytes and metadata. Use <see cref="DecodeILBytes"/> for direct IL access.
    /// Methods without implementation (abstract, extern, delegate, interface) return default.
    /// Requires a PEReader because IL bytes are in the PE file, not in metadata tables.
    /// Not aggressively inlined due to complex control flow (try/catch, multiple branches) that would increase code size.
    /// </remarks>
    public static MethodBodyBlock? GetMethodBody(this MethodDefinition method, PEReader peReader)
    {
        // Check if method has implementation
        if ((method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL)
        {
            return null;
        }

        if ((method.Attributes & MethodAttributes.Abstract) != 0)
        {
            return null;
        }

        var rva = method.RelativeVirtualAddress;
        if (rva == 0)
        {
            return null;
        }

        try
        {
            return peReader.GetMethodBody(rva);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Decodes and returns the raw IL bytes for the method.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>The IL byte array, or empty if the method has no body.</returns>
    /// <remarks>
    /// Allocates a byte array. For zero allocation access, use <see cref="GetILSpan"/>.
    /// </remarks>
    public static byte[] DecodeILBytes(this MethodDefinition method, PEReader peReader)
    {
        var body = method.GetMethodBody(peReader);
        if (body == null)
        {
            return Array.Empty<byte>();
        }

        return body.GetILBytes() ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Gets the raw IL bytes as a truly zero allocation span.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>The IL bytes as a span, or empty if the method has no body.</returns>
    /// <remarks>
    /// The returned span is valid as long as the <paramref name="peReader"/> is alive.
    /// Prefer this over <see cref="GetILBytes"/> on hot paths to avoid byte[] allocation.
    /// Parses the IL method body header directly from PE mapped memory without allocating a MethodBodyBlock.
    /// </remarks>
    public static unsafe ReadOnlySpan<byte> GetILSpan(this MethodDefinition method, PEReader peReader)
    {
        if ((method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL)
        {
            return ReadOnlySpan<byte>.Empty;
        }
        
        if ((method.Attributes & MethodAttributes.Abstract) != 0)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        int rva = method.RelativeVirtualAddress;
        if (rva == 0)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        var block = peReader.GetSectionData(rva);
        if (block.Length < 1)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        var span = new ReadOnlySpan<byte>(block.Pointer, block.Length);
        byte flags = span[0];

        int ilOffset, ilLength;

        if ((flags & 0x3) == 0x2) // CorILMethod_TinyFormat
        {
            ilOffset = 1;
            ilLength = flags >> 2;
        }
        else if ((flags & 0x3) == 0x3) // CorILMethod_FatFormat
        {
            if (span.Length < 12)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            // Byte 1 upper nibble: header size in 32 bit DWords
            int headerSize = (span[1] >> 4) * 4;
            if (headerSize < 8) // need at least 8 bytes to reach CodeSize at offset 4
            {
                return ReadOnlySpan<byte>.Empty;
            }

            ilLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4));
            ilOffset = headerSize;
        }
        else
        {
            return ReadOnlySpan<byte>.Empty;
        }

        if (ilLength <= 0 || ilOffset + ilLength > span.Length)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return span.Slice(ilOffset, ilLength);
    }

    /// <summary>
    /// Checks if the method has an IL body.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <returns>True if the method has IL implementation; false for abstract, extern, or delegate methods.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasMethodBody(this MethodDefinition method)
    {
        if ((method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL)
        {
            return false;
        }

        if ((method.Attributes & MethodAttributes.Abstract) != 0)
        {
            return false;
        }

        return method.RelativeVirtualAddress != 0;
    }

    /// <summary>
    /// Gets the maximum stack size required by the method.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>The max stack size, or 0 if the method has no body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxStack(this MethodDefinition method, PEReader peReader)
    {
        var body = method.GetMethodBody(peReader);
        return body?.MaxStack ?? 0;
    }

    /// <summary>
    /// Checks if the method initializes locals to zero.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>True if locals are initialized; false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetLocalVariablesInitialized(this MethodDefinition method, PEReader peReader)
    {
        var body = method.GetMethodBody(peReader);
        return body?.LocalVariablesInitialized ?? false;
    }

    /// <summary>
    /// Gets the local variable signature token.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>The standalone signature handle for locals, or default if no locals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StandaloneSignatureHandle GetLocalSignature(this MethodDefinition method, PEReader peReader)
    {
        var body = method.GetMethodBody(peReader);
        return body?.LocalSignature ?? default;
    }

    /// <summary>
    /// Decodes the local variable types for the method.
    /// </summary>
    /// <typeparam name="TType">The type representation returned by the provider.</typeparam>
    /// <typeparam name="TSignatureDecodeContext">The generic context type.</typeparam>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <param name="metadataReader">The metadata reader for decoding signatures.</param>
    /// <param name="provider">The signature type provider.</param>
    /// <param name="genericContext">The generic context for type parameter resolution.</param>
    /// <returns>An array of local variable types, or empty if no locals.</returns>
    /// <remarks>
    /// This allocates an ImmutableArray. The local variables are in the order they appear in the IL.
    /// Requires both PEReader (for IL body) and MetadataReader (for signature decoding).
    /// </remarks>
    public static ImmutableArray<TType> DecodeLocalVariableTypes<TType, TSignatureDecodeContext>(
        this MethodDefinition method,
        PEReader peReader,
        MetadataReader metadataReader,
        ISignatureTypeProvider<TType, TSignatureDecodeContext> provider,
        TSignatureDecodeContext genericContext)
    {
        var body = method.GetMethodBody(peReader);
        if (body == null)
        {
            return ImmutableArray<TType>.Empty;
        }

        var localSig = body.LocalSignature;
        if (localSig.IsNil)
        {
            return ImmutableArray<TType>.Empty;
        }

        var signature = metadataReader.GetStandaloneSignature(localSig);
        return signature.DecodeLocalSignature(provider, genericContext);
    }

    /// <summary>
    /// Decodes the exception handling regions for the method.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>A collection of exception handling regions (try/catch/finally blocks).</returns>
    /// <remarks>
    /// Returns an empty collection if the method has no exception handlers.
    /// Exception regions describe try/catch/finally/fault blocks in the IL.
    /// </remarks>
    public static ImmutableArray<ExceptionRegion> DecodeExceptionRegions(
        this MethodDefinition method,
        PEReader peReader)
    {
        var body = method.GetMethodBody(peReader);
        if (body == null)
        {
            return ImmutableArray<ExceptionRegion>.Empty;
        }

        return body.ExceptionRegions;
    }

    /// <summary>
    /// Decodes detailed method body information as a structured object.
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="peReader">The PE reader containing the IL bytes.</param>
    /// <returns>Detailed method body information, or null if no body.</returns>
    /// <remarks>
    /// Allocates a <see cref="MethodBodyInfo"/> struct containing IL bytes and body metadata.
    /// Not a hot path accessor; use individual property methods (GetMaxStack, DecodeILBytes, etc.) for performance critical code.
    /// </remarks>
    public static MethodBodyInfo? DecodeMethodBodyInfo(this MethodDefinition method, PEReader peReader)
    {
        var body = method.GetMethodBody(peReader);
        if (body == null)
        {
            return null;
        }

        return new MethodBodyInfo(
            ilBytes: body.GetILBytes() ?? Array.Empty<byte>(),
            maxStack: body.MaxStack,
            localsInitialized: body.LocalVariablesInitialized,
            localSignature: body.LocalSignature,
            exceptionRegionCount: body.ExceptionRegions.Length);
    }
}

/// <summary>
/// Structured information about a method's IL body.
/// </summary>
/// <remarks>
/// This is a readonly struct to avoid allocations when passing method body details.
/// </remarks>
public readonly struct MethodBodyInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MethodBodyInfo"/> struct.
    /// </summary>
    public MethodBodyInfo(
        byte[] ilBytes,
        int maxStack,
        bool localsInitialized,
        StandaloneSignatureHandle localSignature,
        int exceptionRegionCount)
    {
        ILBytes = ilBytes;
        MaxStack = maxStack;
        LocalsInitialized = localsInitialized;
        LocalSignature = localSignature;
        ExceptionRegionCount = exceptionRegionCount;
    }

    /// <summary>
    /// Gets the raw IL bytes.
    /// </summary>
    public byte[] ILBytes { get; }

    /// <summary>
    /// Gets the maximum stack size.
    /// </summary>
    public int MaxStack { get; }

    /// <summary>
    /// Gets a value indicating whether local variables are initialized to zero.
    /// </summary>
    public bool LocalsInitialized { get; }

    /// <summary>
    /// Gets the local variable signature handle.
    /// </summary>
    public StandaloneSignatureHandle LocalSignature { get; }

    /// <summary>
    /// Gets the number of exception handling regions.
    /// </summary>
    public int ExceptionRegionCount { get; }

    /// <summary>
    /// Gets the size of the IL code in bytes.
    /// </summary>
    public int ILSize => ILBytes.Length;

    /// <summary>
    /// Checks if the method has local variables.
    /// </summary>
    public bool HasLocals => !LocalSignature.IsNil;

    /// <summary>
    /// Checks if the method has exception handlers.
    /// </summary>
    public bool HasExceptionHandlers => ExceptionRegionCount > 0;
}
