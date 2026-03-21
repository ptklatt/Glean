using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a function pointer type.
/// </summary>
public sealed class FunctionPointerSignature : TypeSignature
{
    /// <summary>
    /// Gets the method signature.
    /// </summary>
    public MethodSignature<TypeSignature> Signature { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionPointerSignature"/> class.
    /// </summary>
    public FunctionPointerSignature(MethodSignature<TypeSignature> signature)
    {
        Signature = signature;
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.FunctionPointer;

    public override bool? IsValueType => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false;
    }

    public override bool Equals(TypeSignature? other)
    {
        if (other is not FunctionPointerSignature f)                                { return false; }
        if (!Signature.ReturnType.Equals(f.Signature.ReturnType))                   { return false; }
        if (Signature.ParameterTypes.Length != f.Signature.ParameterTypes.Length)   { return false; }
        for (int i = 0; i < Signature.ParameterTypes.Length; i++)
        {
            if (!Signature.ParameterTypes[i].Equals(f.Signature.ParameterTypes[i])) { return false; }
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Kind);
        hc.Add(Signature.ReturnType);
        foreach (var p in Signature.ParameterTypes)
        {
            hc.Add(p);
        }
        return hc.ToHashCode();
    }

    public override void FormatTo(StringBuilder sb)
    {
        sb.Append("method ");
        Signature.ReturnType.FormatTo(sb);
        sb.Append('(');

        for (int i = 0; i < Signature.ParameterTypes.Length; i++)
        {
            if (i > 0) { sb.Append(", ");}
            Signature.ParameterTypes[i].FormatTo(sb);
        }

        sb.Append(')');
    }
}
