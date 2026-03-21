using System.Reflection;

namespace Glean.Tests.Utility;

public static class TestUtility
{
    public static MetadataScope OpenCoreLibMetadata()
    {
        return OpenMetadata(typeof(object).Assembly.Location);
    }

    public static MetadataScope OpenMetadata(string assemblyPath)
    {
        return new MetadataScope(assemblyPath);
    }

    public static MetadataScope OpenMetadata(Assembly assembly)
    {
        return OpenMetadata(assembly.Location);
    }

    public static MetadataScope BuildMetadata(params string[] sources)
    {
        var builder = new TestAssemblyBuilder();
        foreach (var source in sources)
        {
            builder.WithSource(source);
        }

        return builder.BuildMetadataScope();
    }
}
