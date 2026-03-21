using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a generic type instantiation (e.g., List{int}).
/// </summary>
public sealed class GenericInstanceSignature : TypeSignature
{
    /// <summary>
    /// Gets the generic type definition.
    /// </summary>
    public TypeSignature GenericType { get; }

    /// <summary>
    /// Gets the type arguments.
    /// </summary>
    public ImmutableArray<TypeSignature> Arguments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericInstanceSignature"/> class.
    /// </summary>
    public GenericInstanceSignature(TypeSignature genericType, ImmutableArray<TypeSignature> arguments)
    {
        GenericType = genericType ?? throw new ArgumentNullException(nameof(genericType));
        Arguments = arguments;
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.GenericInstance;

    public override bool? IsValueType => GenericType.IsValueType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return GenericType.Is(ns, name, scope);
    }

    public override bool Equals(TypeSignature? other)
    {
        if (other is not GenericInstanceSignature g)  { return false; }
        if (!GenericType.Equals(g.GenericType))       { return false; }
        if (Arguments.Length != g.Arguments.Length)   { return false; }
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (!Arguments[i].Equals(g.Arguments[i])) { return false; }
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Kind);
        hc.Add(GenericType);
        foreach (var arg in Arguments)
        {
            hc.Add(arg);
        }
        return hc.ToHashCode();
    }

    public override void FormatTo(StringBuilder sb)
    {
        GenericType.FormatTo(sb);
        sb.Append('<');
        for (int i = 0; i < Arguments.Length; i++)
        {
            if (i > 0) { sb.Append(", "); }
            Arguments[i].FormatTo(sb);
        }
        sb.Append('>');
    }
}
