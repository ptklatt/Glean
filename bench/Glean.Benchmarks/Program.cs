using BenchmarkDotNet.Running;

using Glean.Benchmarks;

if (args.SequenceEqual(new[] { "--validate" }))
{
    using var fast = new FastBenchmarks();
    fast.Setup();
    using var rich = new RichBenchmarks();
    rich.Setup();
    using var resolution = new ResolutionBenchmarks();
    resolution.Setup();
    Console.WriteLine("All benchmark workload checks passed.");
    return 0;
}

var summaries = BenchmarkSwitcher.FromAssembly(typeof(FastBenchmarks).Assembly).Run(args).ToArray();
bool isInformational = args.SequenceEqual(new[] { "--help" }) ||
    args.SequenceEqual(new[] { "--version" }) || args.SequenceEqual(new[] { "--info" }) ||
    ((args.Length == 2) && (args[0] == "--list") &&
        (string.Equals(args[1], "flat", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[1], "tree", StringComparison.OrdinalIgnoreCase)));
if ((summaries.Length == 0) && !isInformational)
{
    return 1;
}

return summaries.Any(summary => summary.HasCriticalValidationErrors ||
    ((!isInformational) && (summary.Reports.Length == 0)) ||
    summary.Reports.Any(report => !report.Success || (report.ResultStatistics == null))) ? 1 : 0;
