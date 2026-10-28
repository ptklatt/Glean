namespace Glean.Benchmarks;

internal static class SpectrumValidation
{
    public static void Validate()
    {
        int checks = 0;
        foreach (string corpus in new[] { "Fixture", "Linq", "CoreLib" })
        {
            using var focused = new FocusedTraversalBenchmarks { Corpus = corpus };
            focused.Setup();
            RequireAllocationFree(focused.ContextFlags, $"{corpus}/isolated flags");
            RequireAllocationFree(focused.ContextCachedFlags, $"{corpus}/cached attribute flags");
            RequireAllocationFree(focused.ContextParameters, $"{corpus}/isolated parameters");
            checks += 3;
            using var accessors = new FocusedAccessorBenchmarks { Corpus = corpus };
            accessors.Setup();
            RequireAllocationFree(accessors.ContextAccessors, $"{corpus}/accessor convenience properties");
            RequireAllocationFree(accessors.GroupedAccessors, $"{corpus}/grouped accessors");
            checks += 2;
            using var semantic = new SemanticInspectionBenchmarks { Corpus = corpus };
            semantic.Setup();
            RequireAllocationFree(semantic.ContextTypeChecks, $"{corpus}/type classification");
            RequireAllocationFree(semantic.GleanConstructors, $"{corpus}/constructors");
            RequireAllocationFree(semantic.ContextAttributeChecks, $"{corpus}/attribute presence");
            checks += 3;
            foreach (var workload in Enum.GetValues<MetadataWorkload>())
            {
                using var benchmark = new CorpusTraversalBenchmarks { Corpus = corpus, Workload = workload };
                benchmark.Setup();
                if (corpus == "Fixture")
                {
                    MetadataFixture.Require(benchmark.Srm() > 0, $"Nonempty fixture/{workload}");
                }
                if ((workload != MetadataWorkload.Names) && (workload != MetadataWorkload.RichSignatures))
                {
                    RequireAllocationFree(benchmark.Glean, $"{corpus}/{workload}");
                }
                checks++;
            }
        }
        foreach (string corpus in new[] { "Synthetic", "Fixture", "Linq", "CoreLib" })
        {
            using var benchmark = new CompoundFilterBenchmarks { Corpus = corpus };
            benchmark.Setup();
            RequireAllocationFree(() => benchmark.GleanPublic() + (long)benchmark.GleanPrivate(), $"{corpus}/compound filters");
            checks += 2;
        }
        foreach (bool generic in new[] { false, true })
        {
            foreach (bool malformed in new[] { false, true })
            {
                using var benchmark = new SignatureShapeBenchmarks { GenericInstance = generic, Malformed = malformed };
                benchmark.Setup();
                if (!malformed)
                {
                    RequireAllocationFree(() => benchmark.Glean(), $"Signatures/generic={generic}");
                }
                checks++;
            }
        }
        foreach (string corpus in new[] { "Fixture", "Linq" })
        {
            foreach (bool cache in new[] { false, true })
            {
                using var resolution = new ResolutionCorpusBenchmarks { Corpus = corpus, CacheSignatures = cache };
                resolution.Setup();
                RequireAllocationFree(resolution.WarmMemberHits, $"{corpus}/warm member hits/cache={cache}");
                ReportAllocations(resolution.WarmMemberMisses, $"{corpus}/warm member misses/cache={cache}");
                ReportAllocations(resolution.WarmMixedMembers, $"{corpus}/warm mixed members/cache={cache}");
                checks += 6;
            }
            var loading = new LoadingBenchmarks { Corpus = corpus };
            loading.Setup();
            checks += 3;
        }
        foreach (string corpus in new[] { "Fixture", "Linq", "CoreLib" })
        {
            using var hierarchy = new HierarchyBenchmarks { Corpus = corpus };
            hierarchy.Setup();
            RequireAllocationFree(hierarchy.LocalBases, $"{corpus}/local bases");
            checks += 3;
            using var attributes = new AttributeDecodingBenchmarks { Corpus = corpus };
            attributes.Setup();
            checks += 3;
            foreach (var profile in Enum.GetValues<TraversalProfile>())
            {
                using var visitor = new TraversalProfileBenchmarks { Corpus = corpus, Profile = profile };
                visitor.Setup();
                RequireAllocationFree(visitor.Traverse, $"{corpus}/visitor/{profile}");
                checks++;
            }
        }
        foreach (int names in new[] { 1, 32 })
        {
            foreach (int overloads in new[] { 1, 64 })
            {
                using var index = new IndexShapeBenchmarks { Names = names, Overloads = overloads };
                index.Setup();
                RequireAllocationFree(index.WarmCandidates, $"Index/names={names}/overloads={overloads}");
                checks += 3;
            }
        }
        using var debug = new DebugMetadataBenchmarks();
        debug.Setup();
        RequireAllocationFree(debug.GleanSequencePoints, "Sequence points");
        RequireAllocationFree(debug.GleanDocuments, "Documents");
        checks += 2;
        foreach (var workload in Enum.GetValues<ConstantWorkload>())
        {
            using var constants = new ConstantBenchmarks { Workload = workload };
            constants.Setup();
            RequireAllocationFree(constants.Glean, $"Constants/{workload}");
            checks++;
        }
        using var graphs = new SignatureGraphBenchmarks();
        graphs.Setup();
        RequireAllocationFree(graphs.Dispatch, "All signature kind dispatch");
        RequireAllocationFree(graphs.StructuralEquality, "Signature graph equality");
        checks += 4;
        foreach (string corpus in new[] { "Fixture", "Linq", "CoreLib" })
        {
            using var formatting = new RichFormattingBenchmarks { Corpus = corpus };
            formatting.Setup();
            checks++;
        }
        foreach (var policy in Enum.GetValues<Glean.Resolution.AssemblyBindingPolicy>())
        {
            foreach (bool resolver in new[] { false, true })
            {
                using var miss = new BindingMissBenchmarks { WithResolver = resolver, Policy = policy };
                miss.Setup();
                ReportAllocations(() => miss.MissingAssembly(), $"Binding misses/{policy}/resolver={resolver}");
                checks++;
            }
            foreach (bool fallback in new[] { false, true })
            {
                using var binding = new BindingPolicyBenchmarks { Policy = policy, FallbackOnly = fallback };
                binding.Setup();
                string name = $"Registered binding/{policy}/fallback={fallback}";
                if ((policy == Glean.Resolution.AssemblyBindingPolicy.Exact) && (!fallback))
                {
                    RequireAllocationFree(() => binding.ResolveRegistered(), name);
                }
                else
                {
                    ReportAllocations(() => binding.ResolveRegistered(), name);
                }
                checks++;
            }
        }
        Console.WriteLine($"Validated {checks} spectrum workload configurations, fast allocation contracts and allocating resolution paths.");
    }

    private static void RequireAllocationFree(Func<long> operation, string name)
    {
        long allocated = MeasureAllocations(operation, name);
        MetadataFixture.Require(allocated == 0, $"{name} allocated {allocated} bytes during 32 warmed scans");
    }

    private static void ReportAllocations(Func<long> operation, string name)
    {
        long allocated = MeasureAllocations(operation, name);
        Console.WriteLine($"{name}: {allocated} allocated bytes during 32 warmed scans");
    }

    private static long MeasureAllocations(Func<long> operation, string name)
    {
        long expected = operation();
        for (int i = 0; i < 32; i++)
        {
            MetadataFixture.Require(operation() == expected, name + " warmup result");
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (int i = 0; i < 32; i++)
        {
            sum += operation();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        MetadataFixture.Require(sum == 32 * expected, name + " repeated result");
        return allocated;
    }
}
