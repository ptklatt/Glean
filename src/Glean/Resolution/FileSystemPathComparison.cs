namespace Glean.Resolution;

internal static class FileSystemPathComparison
{
    internal static readonly StringComparer Comparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static readonly StringComparison Comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal static string NormalizeDirectory(string directory)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    }
}
