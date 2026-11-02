using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Glean.Benchmarks;

internal static class BenchmarkEnvironment
{
    internal static void WriteToConsole()
    {
        var metadata = typeof(MetadataReader).Assembly;
        string version = metadata.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        string path = metadata.Location;
        string hash = GetFileHash(path);

        Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"SRMInformationalVersion={version}");
        Console.WriteLine($"SRMAssembly={metadata.FullName}");
        Console.WriteLine($"SRMPath={path}");
        Console.WriteLine($"SRMSHA256={hash}");
    }

    private static string GetFileHash(string path)
    {
        if (path.Length == 0)
        {
            return "unavailable";
        }

        try
        {
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        }
        catch (IOException)
        {
            return "unavailable";
        }
        catch (UnauthorizedAccessException)
        {
            return "unavailable";
        }
    }
}
