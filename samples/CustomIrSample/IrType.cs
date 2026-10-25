using System.Collections.Immutable;
using System.Reflection.Metadata;

using Glean.Resolution;

namespace CustomIrSample;

internal abstract record IrType
{
    public sealed record Primitive(PrimitiveTypeCode Code) : IrType;
    public sealed record Named(NamedTypeIdentity Identity, byte RawTypeKind,
        NamedTypeIdentity? Definition = null, ResolutionFailureReason Failure = ResolutionFailureReason.None) : IrType;
    public sealed record Vector(IrType Element) : IrType;
    public sealed record Array(IrType Element, ArrayShape Shape) : IrType;
    public sealed record Wrapped(IrType Element, Wrapper Kind) : IrType;
    public sealed record GenericParameter(int Index, bool IsMethod) : IrType;
    public sealed record GenericInstance(IrType Definition, ImmutableArray<IrType> Arguments) : IrType;
    public sealed record Modified(IrType Modifier, IrType Unmodified, bool Required) : IrType;
    public sealed record FunctionPointer(MethodSignature<IrType> Signature) : IrType;
}

internal enum Wrapper
{
    Pointer,
    ByReference,
    Pinned
}

// Strings, tokens and module IDs survive disposal of the metadata scopes.
internal sealed record NamedTypeIdentity(string FullName, string Scope, Guid OriginModule, int Token);

internal readonly record struct IrGenericContext(
    ImmutableArray<IrType> TypeArguments = default,
    ImmutableArray<IrType> MethodArguments = default);
