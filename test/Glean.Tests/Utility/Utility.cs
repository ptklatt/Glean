using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Glean.Tests.Utility;

internal static class TestUtility
{
    internal static MetadataScope OpenCoreLibMetadata()
    {
        return OpenMetadata(typeof(object).Assembly.Location);
    }

    internal static MetadataScope OpenMetadata(string assemblyPath)
    {
        return new MetadataScope(assemblyPath);
    }

    internal static MetadataScope OpenMetadata(Assembly assembly)
    {
        return OpenMetadata(assembly.Location);
    }
}

internal sealed class MetadataScope : IDisposable
{
    private readonly FileStream _stream;
    private readonly PEReader _peReader;

    internal MetadataScope(string assemblyPath)
    {
        _stream = File.OpenRead(assemblyPath);
        _peReader = new PEReader(_stream);
        Reader = _peReader.GetMetadataReader();
    }

    internal MetadataReader Reader { get; }

    public void Dispose()
    {
        _peReader.Dispose();
        _stream.Dispose();
    }
}
