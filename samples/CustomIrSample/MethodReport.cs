using System.Globalization;

using Glean.Contexts;

namespace CustomIrSample;

internal static class MethodReport
{
    public static void Write(MethodContext method, IrTypeProvider provider, TextWriter output)
    {
        var signature = method.DecodeSignature(provider, default(IrGenericContext));
        output.WriteLine($"Method: {method.Name} {IrDisplay.Method(signature)}");
        output.WriteLine($"  return: {IrDisplay.Type(signature.ReturnType)} [{Metadata(method.TryGetReturnParameter(out var result), result)}]");
        for (int i = 0; i < signature.ParameterTypes.Length; i++)
        {
            bool found = method.TryGetParameter(i, out var parameter);
            output.WriteLine($"  parameter[{i.ToString(CultureInfo.InvariantCulture)}]: {IrDisplay.Type(signature.ParameterTypes[i])} [{Metadata(found, parameter)}]");
        }
    }

    private static string Metadata(bool found, ParameterContext parameter)
    {
        if (!found)
        {
            return "no metadata row";
        }

        string name = parameter.Name.Length == 0 ? "<unnamed>" : parameter.Name;
        return $"seq={parameter.SequenceNumber.ToString(CultureInfo.InvariantCulture)}, name={name}, flags={parameter.Attributes}";
    }
}
