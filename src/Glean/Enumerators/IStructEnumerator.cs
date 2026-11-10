namespace Glean.Enumerators;

/// <summary>
/// Marker interface for struct enumerators that can be adapted to <see cref="System.Collections.Generic.IEnumerable{T}"/>
/// via <c>Glean.Linq.EnumerableAdapters.AsEnumerable</c>.
/// </summary>
/// <typeparam name="T">The element type yielded by the enumerator.</typeparam>
public interface IStructEnumerator<out T>
{
    /// <summary>Gets the current element. Read only after a successful <see cref="MoveNext"/> call.</summary>
    T Current { get; }
    /// <summary>Advances to the next element, returning false when no element is available.</summary>
    bool MoveNext();
    /// <summary>Releases resources used by the enumerator.</summary>
    void Dispose();
}
