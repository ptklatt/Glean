using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Signatures;
using Glean.Providers;
using Glean.Extensions;

namespace Glean.Enumerators;

/// <summary>
/// Struct enumerator for implemented interfaces decoded as <see cref="TypeSignature"/>.
/// </summary>
public struct InterfaceEnumerator : IStructEnumerator<Signatures.TypeSignature>
{
    private readonly MetadataReader _reader;
    private readonly ISignatureTypeProvider<TypeSignature, SignatureDecodeContext> _provider;
    private readonly SignatureDecodeContext _genericContext;
    private InterfaceImplementationHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InterfaceEnumerator Create(
        MetadataReader reader,
        InterfaceImplementationHandleCollection collection,
        ISignatureTypeProvider<TypeSignature, SignatureDecodeContext> provider,
        SignatureDecodeContext genericContext)
    {
        return new InterfaceEnumerator(reader, collection, provider, genericContext);
    }

    private InterfaceEnumerator(
        MetadataReader reader,
        InterfaceImplementationHandleCollection collection,
        ISignatureTypeProvider<TypeSignature, SignatureDecodeContext> provider,
        SignatureDecodeContext genericContext)
    {
        _reader = reader;
        _provider = provider;
        _genericContext = genericContext;
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current interface TypeSignature.
    /// </summary>
    public TypeSignature Current
    {
        get
        {
            var interfaceImpl = _reader.GetInterfaceImplementation(_enumerator.Current);
            return interfaceImpl.Interface.DecodeTypeSignature(_reader, _provider, _genericContext);
        }
    }

    /// <summary>
    /// Moves to the next interface.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InterfaceEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}
