using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a type with custom modifiers (modopt/modreq).
/// </summary>
public sealed class ModifiedTypeSignature : TypeSignature
{
    /// <summary>
    /// Gets the unmodified type.
    /// </summary>
    public TypeSignature UnmodifiedType { get; }

    /// <summary>
    /// Gets the required custom modifiers.
    /// </summary>
    public ImmutableArray<TypeSignature> RequiredModifiers { get; }

    /// <summary>
    /// Gets the optional custom modifiers.
    /// </summary>
    public ImmutableArray<TypeSignature> OptionalModifiers { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModifiedTypeSignature"/> class.
    /// </summary>
    public ModifiedTypeSignature(
        TypeSignature unmodifiedType,
        ImmutableArray<TypeSignature> requiredModifiers,
        ImmutableArray<TypeSignature> optionalModifiers)
    {
        UnmodifiedType    = unmodifiedType ?? throw new ArgumentNullException(nameof(unmodifiedType));
        RequiredModifiers = requiredModifiers;
        OptionalModifiers = optionalModifiers;
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.Modified;

    public override bool? IsValueType => UnmodifiedType.IsValueType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return UnmodifiedType.Is(ns, name, scope);
    }

    public override bool Equals(TypeSignature? other)
    {
        if (other is not ModifiedTypeSignature m)                     { return false; }
        if (!UnmodifiedType.Equals(m.UnmodifiedType))                 { return false; }
        if (RequiredModifiers.Length != m.RequiredModifiers.Length)   { return false; }
        if (OptionalModifiers.Length != m.OptionalModifiers.Length)   { return false; }
        
        for (int i = 0; i < RequiredModifiers.Length; i++)
        {
            if (!RequiredModifiers[i].Equals(m.RequiredModifiers[i])) { return false; }
        }
        
        for (int i = 0; i < OptionalModifiers.Length; i++)
        {
            if (!OptionalModifiers[i].Equals(m.OptionalModifiers[i])) { return false; }
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Kind);
        hc.Add(UnmodifiedType);
        foreach (var mod in RequiredModifiers)
        {
            hc.Add(mod);
        }
        
        foreach (var mod in OptionalModifiers)
        {
            hc.Add(mod);
        }
        return hc.ToHashCode();
    }

    public override void FormatTo(StringBuilder sb)
    {
        UnmodifiedType.FormatTo(sb);

        foreach (var mod in RequiredModifiers)
        {
            sb.Append(" modreq(");
            mod.FormatTo(sb);
            sb.Append(')');
        }

        foreach (var mod in OptionalModifiers)
        {
            sb.Append(" modopt(");
            mod.FormatTo(sb);
            sb.Append(')');
        }
    }
}
