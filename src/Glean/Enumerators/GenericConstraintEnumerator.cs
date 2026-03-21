using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Signatures;
using Glean.Providers;
using Glean.Extensions;

namespace Glean.Enumerators;

/// <summary>
/// Struct enumerator for generic constraints decoded as <see cref="TypeSignature"/>.
/// </summary>
public struct GenericConstraintEnumerator : IStructEnumerator<TypeSignature>
{
    private readonly MetadataReader _reader;
    private readonly ISignatureTypeProvider<TypeSignature, SignatureDecodeContext> _provider;
    private readonly SignatureDecodeContext _genericContext;
    private GenericParameterConstraintHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GenericConstraintEnumerator Create(
        MetadataReader reader,
        GenericParameterConstraintHandleCollection collection,
        ISignatureTypeProvider<TypeSignature, SignatureDecodeContext> provider,
        SignatureDecodeContext genericContext)
    {
        return new GenericConstraintEnumerator(reader, collection, provider, genericContext);
    }

    private GenericConstraintEnumerator(
        MetadataReader reader,
        GenericParameterConstraintHandleCollection collection,
        ISignatureTypeProvider<TypeSignature, SignatureDecodeContext> provider,
        SignatureDecodeContext genericContext)
    {
        _reader = reader;
        _provider = provider;
        _genericContext = genericContext;
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current constraint TypeSignature.
    /// </summary>
    public TypeSignature Current
    {
        get
        {
            var constraint = _reader.GetGenericParameterConstraint(_enumerator.Current);
            return constraint.Type.DecodeTypeSignature(_reader, _provider, _genericContext);
        }
    }

    /// <summary>
    /// Moves to the next constraint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GenericConstraintEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}
