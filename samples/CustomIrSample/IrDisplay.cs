using System.Collections.Immutable;
using System.Globalization;
using System.Reflection.Metadata;

using Glean.Resolution;

namespace CustomIrSample;

internal static class IrDisplay
{
    public static string Type(IrType type)
    {
        return type switch
        {
            IrType.Primitive primitive => primitive.Code.ToString(),
            IrType.Named named => Named(named),
            IrType.Vector vector => Type(vector.Element) + "[]",
            IrType.Array array => $"{Type(array.Element)}[rank={Number(array.Shape.Rank)}; sizes={Numbers(array.Shape.Sizes)}; lower={Numbers(array.Shape.LowerBounds)}]",
            IrType.Wrapped { Kind: Wrapper.Pointer } pointer => Type(pointer.Element) + "*",
            IrType.Wrapped { Kind: Wrapper.ByReference } reference => Type(reference.Element) + "&",
            IrType.Wrapped { Kind: Wrapper.Pinned } pinned => "pinned " + Type(pinned.Element),
            IrType.GenericParameter parameter => (parameter.IsMethod ? "!!" : "!") + Number(parameter.Index),
            IrType.GenericInstance instance => Type(instance.Definition) + "<" + string.Join(", ", instance.Arguments.Select(Type)) + ">",
            IrType.Modified modifier => (modifier.Required ? "modreq(" : "modopt(") + Type(modifier.Modifier) + ") " + Type(modifier.Unmodified),
            IrType.FunctionPointer pointer => "fnptr " + Method(pointer.Signature),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    public static string Method(MethodSignature<IrType> signature)
    {
        var parameters = new List<string>();
        for (int i = 0; i <= signature.ParameterTypes.Length; i++)
        {
            if ((signature.Header.CallingConvention == SignatureCallingConvention.VarArgs) && (i == signature.RequiredParameterCount))
            {
                parameters.Add("...");
            }

            if (i < signature.ParameterTypes.Length)
            {
                parameters.Add(Type(signature.ParameterTypes[i]));
            }
        }

        var instance = signature.Header.IsInstance ? " instance" : string.Empty;
        var explicitThis = signature.Header.HasExplicitThis ? " explicitthis" : string.Empty;
        return $"{signature.Header.CallingConvention}{instance}{explicitThis}<{Number(signature.GenericParameterCount)}> {Type(signature.ReturnType)}({string.Join(", ", parameters)})";
    }

    private static string Named(IrType.Named type)
    {
        string name = (type.RawTypeKind == 0x11 ? "valuetype " : string.Empty) + type.Identity.FullName;
        if (type.Failure != ResolutionFailureReason.None)
        {
            return $"{name} [unresolved: {type.Identity.Scope}; {type.Failure}]";
        }

        if ((type.Definition != null) && (type.Definition.Scope != type.Identity.Scope))
        {
            return $"{name} [resolved: {type.Definition.Scope}]";
        }

        return name;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Numbers(ImmutableArray<int> values) => string.Join(",", values.Select(Number));
}
