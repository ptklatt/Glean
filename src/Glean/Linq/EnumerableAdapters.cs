using Glean.Enumerators;

namespace Glean.Linq;

// Single generic AsEnumerable() adapter for all struct enumerators implementing IStructEnumerator<T>.

/// <summary>
/// Allocating adapters that expose pattern based struct enumerators as <see cref="IEnumerable{T}"/> for LINQ.
/// </summary>
/// <remarks>
/// Allocates a state machine and one interface box per call. Prefer foreach on
/// the struct enumerators directly for zero allocation iteration.
/// <para/>
/// Add <c>using Glean.Linq;</c> to your file, then call <c>.AsEnumerable()</c> on any struct
/// enumerator that implements <see cref="IStructEnumerator{T}"/> to enable LINQ operators.
/// </remarks>
public static class EnumerableAdapters
{
    /// <summary>
    /// Wraps a struct enumerator as an <see cref="IEnumerable{T}"/> for LINQ.
    /// </summary>
    /// <typeparam name="T">The element type, inferred from the enumerator's <see cref="IStructEnumerator{T}"/> implementation.</typeparam>
    /// <param name="enumerator">The enumerator to wrap. Passed via interface to enable single method type inference.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> that iterates the enumerator and disposes it when done.</returns>
    public static IEnumerable<T> AsEnumerable<T>(this IStructEnumerator<T> enumerator)
    {
        try { while (enumerator.MoveNext()) yield return enumerator.Current; }
        finally { enumerator.Dispose(); }
    }
}