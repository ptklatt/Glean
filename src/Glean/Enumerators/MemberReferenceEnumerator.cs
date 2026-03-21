using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for member references.
/// </summary>
public struct MemberReferenceEnumerator : IStructEnumerator<MemberReferenceContext>
{
    private readonly MetadataReader _reader;
    private MemberReferenceHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MemberReferenceEnumerator Create(MetadataReader reader, MemberReferenceHandleCollection collection)
    {
        return new MemberReferenceEnumerator(reader, collection);
    }

    private MemberReferenceEnumerator(MetadataReader reader, MemberReferenceHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current member reference context.
    /// </summary>
    public MemberReferenceContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemberReferenceContext.Create(_reader, _enumerator.Current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MemberReferenceEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}
