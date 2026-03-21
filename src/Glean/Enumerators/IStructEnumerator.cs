namespace Glean.Enumerators;

/// <summary>
/// Marker interface for struct enumerators that can be adapted to <see cref="System.Collections.Generic.IEnumerable{T}"/>
/// via <c>Glean.Linq.EnumerableAdapters.AsEnumerable</c>.
/// </summary>
/// <typeparam name="T">The element type yielded by the enumerator.</typeparam>
public interface IStructEnumerator<out T>
{
    T Current { get; }
    bool MoveNext();
    void Dispose();
}
