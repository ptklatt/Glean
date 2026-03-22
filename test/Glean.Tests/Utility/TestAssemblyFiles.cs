namespace Glean.Tests.Utility;

internal static class TestAssemblyFiles
{
    internal static string BuildAssemblyFile(
        string directory,
        string assemblyName,
        string source,
        params string[] referencePaths)
    {
        var builder = new TestAssemblyBuilder(assemblyName)
            .WithSource(source);

        foreach (var referencePath in referencePaths)
        {
            builder.WithReference(referencePath);
        }

        using var peStream = builder.BuildPEStream();

        var outputPath = Path.Combine(directory, assemblyName + ".dll");
        File.WriteAllBytes(outputPath, peStream.ToArray());
        return outputPath;
    }

    internal static string BuildModuleFile(
        string directory,
        string moduleName,
        string source,
        params string[] referencePaths)
    {
        var builder = new TestAssemblyBuilder(moduleName)
            .WithOutputKind(Microsoft.CodeAnalysis.OutputKind.NetModule)
            .WithSource(source);

        foreach (var referencePath in referencePaths)
        {
            builder.WithReference(referencePath);
        }

        using var peStream = builder.BuildPEStream();

        var outputPath = Path.Combine(directory, moduleName + ".netmodule");
        File.WriteAllBytes(outputPath, peStream.ToArray());
        return outputPath;
    }
}
