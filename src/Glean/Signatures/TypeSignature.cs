using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

public abstract class TypeSignature : IEquatable<TypeSignature>
{
    /// <summary>
    /// Gets the kind of this type signature.
    /// </summary>
    public abstract TypeSignatureKind Kind { get; }

    /// <summary>
    /// Gets whether this type is a value type.
    /// Returns null if the answer cannot be determined without external resolution.
    /// </summary>
    public abstract bool? IsValueType { get; }

    /// <summary>
    /// Checks if this type signature matches the specified namespace and name.
    /// Zero allocation identity check.
    /// </summary>
    /// <param name="ns">The namespace to match (use empty string for no namespace).</param>
    /// <param name="name">The type name to match.</param>
    /// <param name="scope">Optional resolution scope name (assembly/module name) to match.</param>
    /// <returns>True if the type matches; otherwise, false.</returns>
    public abstract bool Is(string ns, string name, string? scope = null);

    /// <summary>
    /// Checks if this type signature is of the specified kind.
    /// </summary>
    /// <typeparam name="T">The type signature kind to check.</typeparam>
    /// <returns>True if this signature is of type T; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Is<T>() where T : TypeSignature => this is T;

    /// <summary>
    /// Formats this type signature into a string builder.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    public abstract void FormatTo(StringBuilder sb);

    /// <summary>
    /// Returns a string representation of this type signature.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        FormatTo(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Determines whether this signature is structurally equal to another.
    /// </summary>
    public abstract bool Equals(TypeSignature? other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => (obj is TypeSignature other) && Equals(other);

    /// <inheritdoc/>
    public abstract override int GetHashCode();

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TypeSignature? left, TypeSignature? right)
    {
        if (ReferenceEquals(left, right)) { return true; }
        if ((left == null) || (right == null)) { return false; }
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TypeSignature? left, TypeSignature? right) => !(left == right);
}