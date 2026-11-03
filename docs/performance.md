# Performance Notes

Glean is built around a simple rule:

- Keep the fast tier close to raw `System.Reflection.Metadata`.
- Make richer decoding and resolution explicit, not implicit.

## What Should Stay Cheap

These APIs operate directly on metadata and are intended for repeated scans:

- `AssemblyScope`, `AssemblyContext`, `TypeContext`, `MethodContext`
- struct enumerators such as `EnumerateTypes()`
- `MetadataTraversal` standard and narrow profiles
- identity helpers such as `Is`, `NameIs`, `NamespaceIs`, `HasAttribute`
- `TypeSignature.Accept(ref visitor, state)` on already decoded signatures
- warm cache resolution lookups

The traversal APIs do not force signature decoding or assembly closure loading.
Struct metadata visitors must implement every callback, including empty callbacks
for unused tables. Passing visitors by reference preserves their state without
boxing.

`AssemblyScope` opens and owns readers, so creating a scope has setup costs.
Successful exact-binding resolution lookups reuse caches; their allocation
behavior does not describe the first lookup or the cost of opening dependencies.
Repeated misses and compatible binding can allocate while retrying resolution.
Allocation checks do not establish execution-time parity with raw SRM.

## What Intentionally Allocates

These APIs are richer by design and are expected to allocate:

- `TypeContext.FullName`
- `TypeContext.GetBaseTypeName()`
- `TypeContext.DecodeBaseType()`
- `TypeSignature.ToString()`
- `DecodeSignature(...)`
- `DecodeReturnType()`
- `CustomAttributeDecoder.Decode(...)`
- cold cache resolution setup
- `EnumerateAllInterfaces(assemblySet)`, which builds a queue and visited set

Generic interface instances are reported as resolved type definitions and are
deduplicated by reader and type handle. The local `EnumerateBaseTypes()` overload
uses no visited collection and stays allocation free.

## Verification

Allocation focused tests live in
[AllocationBehaviorTests.cs](../test/Glean.Tests/Performance/AllocationBehaviorTests.cs).

Metadata traversal allocation checks live in
[AdvancedMetadataTraversalTests.cs](../test/Glean.Tests/Visitors/AdvancedMetadataTraversalTests.cs),
and local base traversal is covered by
[ResolutionExtensionTests.cs](../test/Glean.Tests/Resolution/ResolutionExtensionTests.cs).

They currently verify:

- fast tier name checks stay near raw SRM allocation behavior
- fast tier type enumeration stays near raw SRM allocation behavior
- rich formatting helpers allocate as documented
- custom attribute decoding allocates as documented
- signature visitor dispatch with a struct visitor stays allocation free
- all seven metadata traversal profiles stay allocation free after warmup with
  struct visitors and value state
- long local base chains remain complete without allocating a visited collection
- successful warm cached type resolution stays allocation free on repeated lookups
- successful warm cached member resolution with `MemberResolutionIndex` stays
  allocation free on repeated lookups
- generic `Parameter.TryGetDefaultValue<T>` decoding stays allocation free for
  each supported primitive type (`bool`, `char`, signed and unsigned integers,
  `float`, and `double`) in repeated successful calls on the tested Release runtime

The tests exclude fixture creation, reader setup, delegate construction, and
cache warmup from the measured interval. They use thread-local allocation counts;
some checks allow a small fixed tolerance across thousands of operations.

Custom attribute parent reads can allocate during early calls on .NET 8, in both
raw SRM and Glean. On the tested .NET 8 runtimes, these allocations disappear
after JIT warmup. Benchmark workload validation adds 1.5 seconds of warmup for
attribute scans before enforcing zero steady state allocations. This check does
not describe cold-load allocations; measure first-pass workloads separately.

These are allocation regression tests, not timing benchmarks. Compare equivalent
work when measuring time: enumerating raw handles does less work than creating
contexts that also read their definitions. Report rich decoding and cold
resolution separately from warmed traversal.

## Equivalent SRM Benchmarks

[Glean.Benchmarks](../bench/Glean.Benchmarks) targets .NET 8, .NET 9 and .NET 10 and uses
[BenchmarkDotNet 0.15.8](https://www.nuget.org/packages/BenchmarkDotNet/0.15.8).
The original nine benchmark pairs return the same checksums. Their setup builds deterministic metadata
images in memory, opens their readers, checks table counts, and validates workload
outputs before timing. No source compilation, file I/O, reader opening, or
fixture generation is timed. Both sides read the same metadata images.

The fixture contains 128 top level types plus the module row, eight methods per
type, and two properties per type. Half the types are public in a selected
namespace. Each type has two `Echo` overloads (`int` and `long`) and six methods
with array return and parameter types. A second metadata image references every
type and both overloads. A benchmark operation processes the entire relevant
table or reference array; the allocation column is bytes per complete operation.

| Category | Equivalent work |
| --- | --- |
| `Fast.Types` | Enumerate type definitions, read their attributes, and sum attributes and row numbers. The SRM baseline reads definitions, rather than only enumerating handles. |
| `Fast.Methods` | Enumerate types and their methods, read method definitions, and sum attributes and row numbers. |
| `Fast.Filter` | Select public types by namespace, then public methods named `Echo`, using handle based string comparisons; sum matching method rows. |
| `Fast.SignatureInspection` | Read every property signature header and compressed parameter count; sum counts for ordinary properties and indexers. |
| `Rich.SignatureDecoding` | Decode the same complete method signatures and consume parameter counts and return type presence. Both sides use Glean's `SignatureTypeProvider`; `SrmDecodeWithGleanProvider` measures the SRM entry point with the same graph representation. This is not an independent decoder comparison. |
| `Resolution.WarmTypes` | Resolve all type references through populated caches, returning target readers and definition handles. |
| `Resolution.WarmMembers` | Resolve all member references through populated final answer caches, returning target readers and method handles. Both sides validate identity and overload signatures before caching each answer during setup; timed calls are answer cache hits. |
| `Resolution.ColdTypes` | Construct and register a new resolver/index over already open readers, then resolve all type references. Derived indexes and caches are included. |
| `Resolution.ColdMembers` | Construct a new resolver and member index over already open readers, then resolve every member reference. Derived indexes, name caches, signature checks, and type resolution are included. |

The raw SRM resolution helper is deliberately limited to this fixture: exact
assembly name/version/culture binding for unsigned assemblies, top level types,
and non generic method signatures containing primitive types. It constructs
assembly/type indexes, caches assembly reference bindings, resolved types,
reference names, and uniquely resolved member answers. Cold member operations
build candidate indexes on demand and compare both overload signatures for each
distinct reference; warm member operations hit the final answer cache. Byte
equality is sufficient for these primitive signatures, which contain no reader
relative type tokens. Glean uses its public `AssemblySet` and
`MemberResolutionIndex(cacheSemanticSignatures: false)` APIs with their broader
production semantics. Setup checks each resolved reader and exact row against
the generated fixture, including both overloads on cold and warm resolution paths.
These pairs describe this successful binding workload; they do not measure
dependency loading, forwarding, generic substitution, failed resolution, or
semantic signature fallback.

### Running Measurements

Validate all workload outputs without collecting timing results:

```sh
dotnet run --project bench/Glean.Benchmarks -c Release -f net10.0 -- --validate
```

Validation reports the current process runtime and the loaded SRM assembly's
informational version, identity, path, and SHA-256 hash. Record these alongside
measurement results: a fixed NuGet package reference does not guarantee the same
loaded assembly across runtimes. A newer runtime can use its shared-framework
copy of SRM, as described in the
[.NET package compatibility rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget-package-compatibility-rules).

This output describes the validation process. For measurements that launch
multiple runtimes, validate under each selected runtime; the parent process's
dependency information does not describe every benchmark child. Comparisons
within one runtime and comparisons between runtimes answer different questions.

Run a smoke check to verify generated benchmark processes and exporters:

```sh
dotnet run --project bench/Glean.Benchmarks -c Release -f net10.0 -- --filter '*' --job Dry --artifacts /tmp/glean-benchmarks-smoke --stopOnFirstError
```

Dry runs include startup effects and do not establish steady state timing or
allocation behavior. Failed builds or workloads can leave empty reports; check
that every selected benchmark has measurements. The original nine pairs comprise
18 benchmarks; the spectrum workloads below add parameterized cases.

Run measurements on an otherwise idle machine:

```sh
dotnet run --project bench/Glean.Benchmarks -c Release -f net10.0 -- --filter '*' --outliers DontRemove --artifacts /tmp/glean-benchmarks-results --stopOnFirstError
```

This lets BenchmarkDotNet choose warmup and measurement counts adaptively while
retaining outliers. Use fixed counts only after checking that the workload has
settled. `MemoryDiagnoser`, the `Median` column, and full JSON export are enabled.
Markdown, CSV, HTML, and JSON reports
are written under the artifact directory's `results/` folder; full JSON includes
individual measurements. See the
official [command line guide](https://benchmarkdotnet.org/articles/guides/console-args.html)
for filtering and longer jobs. CI builds the project and validates workload
outputs.

Inspect the individual iterations for a sustained change in timing. If early
measurements are much slower than later ones, increase warmup and rerun both
sides; keep the original samples rather than dropping them from the report.
A cold resolver case creates fresh lookup state for each operation, but runs
inside a warmed process. It does not measure process startup. See the official
[job configuration guide](https://benchmarkdotnet.org/articles/configs/jobs.html)
for adaptive warmup and accuracy settings.

The original pairs use generated in-memory metadata; the broader `Fixture`
corpus is a compiled test assembly. LINQ and CoreLib provide real assembly inputs.
These all exercise library code, but results describe the selected inputs and
the work inside each measured method. For a load-once application, measure file
opening, dependency loading and the first traversal/resolution pass together.
The file-open and closure-loading cases below measure those phases separately;
repeated runs can still benefit from the operating system's file cache.

To compare medians, divide Glean's `Median` by SRM's `Median` from the table or
JSON statistics. BenchmarkDotNet's default `Ratio` column instead reports a ratio
distribution based on means. Inspect individual measurements, errors, and
allocations alongside either comparison.

Results describe this fixture on the recorded runtime and hardware. Keep fast
traversal, warm cache hits, rich decoding, and cold resolution results separate:
their work and allocation behavior differ. Preserve the command, revision, and
environment information with the reports so comparisons remain reproducible.

## Broader Workloads

The `Spectrum` categories cover additional API families and input shapes. They
use a compiled fixture with fields, properties, events, attributes, generics,
constraints, inheritance, optional parameters, arrays, byrefs and a function
pointer, plus `System.Linq` and `System.Private.CoreLib` metadata. Setup logs
input paths and SHA-256 hashes. The compiled fixture provides nonempty cases
for tables absent from some real assemblies, such as events in LINQ.

| Categories | Work measured |
| --- | --- |
| `Spectrum.Metadata` | Paired SRM/context scans for fields, properties, events, parameters, generics/constraints, nested types/interfaces/method implementations, attributes, references, accessors, flags, name comparisons, materialized names and rich signatures. Rich signatures share Glean's provider. |
| `Spectrum.Filter.Public`, `Spectrum.Filter.Private` | The same compound visibility/namespace/method-name filter with each method visibility predicate, on synthetic and real metadata. Both predicates have matching results. |
| `Spectrum.FocusedFlags`, `Spectrum.FocusedParameters` | Dedicated flag and parameter loops, alongside the larger metadata driver. Flag cases include individual convenience getters and reading `Attributes` once for multiple masks. These isolate the calling pattern so inlining choices in the shared driver are not mistaken for an intrinsic API cost. |
| `Spectrum.Accessors` | Dedicated property/event accessor loops, comparing convenience properties with retrieving `Definition.GetAccessors()` once and constructing contexts for its handles. All cases consume the same method rows and attributes. |
| `Spectrum.SignatureShapes` | Property counts and generic-instance arities with one-, two- and four-byte compressed values, plus mixed nil, truncated, invalid-header and invalid-heap inputs. Failure booleans and out values are checked individually. |
| `Spectrum.Resolution` | All member references, with successes and failures retained: cold setup, warm successful lookups, warm failures, mixed warm lookups, reader refresh and index rebuild. Both signature-cache settings are measured; false is the default. Indexed results are validated individually against unindexed resolution. |
| `Spectrum.BindingMisses`, `Spectrum.BindingPolicy` | Missing-assembly retries with and without callbacks, exact identity matches, and version mismatch/fallback under both binding policies. Compatible binding rechecks candidates and is separate from exact-policy answer-cache hits. |
| `Spectrum.IndexShapes` | Singleton and 64-entry method/field buckets, one or 32 names, cold construction, lazy handle-array projection and warm candidate lookup. Setup verifies exact candidate handles and order. |
| `Spectrum.LocalHierarchy`, `Spectrum.Hierarchy` | Paired local base chains with cycle detection, plus standalone resolved base chains and transitive interfaces over already open readers. |
| `Spectrum.Visitors` | All seven traversal profiles with a struct visitor that consumes visited row numbers. These standalone cases have no independent SRM timing baseline. |
| `Spectrum.AttributeValues`, `Spectrum.MaterializedAttributes` | Paired raw/context attribute value decoding with the same provider, and separate materialized attribute graphs. Decode failures are counted and retained. Materialized graphs perform additional work and have no timing-parity baseline. |
| `Spectrum.FileOpen`, `Spectrum.ClosureLoad` | Paired file open/metadata access and separate transitive dependency loading, including disposal. These include setup and I/O; repeated runs may use the operating system's file cache and are not cold-disk measurements. |
| `Spectrum.SequencePoints`, `Spectrum.Documents` | Paired portable PDB sequence-point and document scans using the compiled fixture's PDB. |
| `Spectrum.Constants` | Paired reads of all twelve supported primitive default-value types, missing defaults and type mismatches. The typed Glean path avoids boxing; boxed values are validated only in setup. |
| `Spectrum.SignatureGraphs` | Dispatch over all fifteen decoded signature kinds, formatting and same-reader/cross-reader equality on prebuilt graphs. These are standalone graph operations with no SRM timing baseline. Formatting and cross-reader name comparisons allocate; type-definition equality retains reader identity. |
| `Spectrum.FullNames` | Paired full-name materialization over the same type definitions. Both sides allocate strings and include their formatting cost. |
| `Spectrum.TypeClassification`, `Spectrum.Constructors`, `Spectrum.AttributePresence` | Value-type/enum/delegate checks, constructor detection and present/absent attribute queries. Type classification and attribute presence compare context calls with definition calls using the same Glean SRM extension helpers, measuring wrapper overhead rather than an independent implementation. Constructor detection has a direct SRM baseline. |

`--validate` also exercises these workloads and checks warmed allocation-free
scans directly. It reports allocations for repeated resolution misses and
compatible binding while still checking their outcomes. Intentionally allocating
operations, failures and rich decoding are measured separately. Empty real-table
cases remain identifiable through the logged checksums; they do not establish
performance for populated tables.

For a focused getter comparison, use `--filter '*CompoundFilterBenchmarks*'`.
For cross-runtime measurements, add `--runtimes net8.0 net9.0 net10.0` when those runtimes
are installed. This does not change the library's target framework.

By default, real corpus files come from the executing runtime. Set
`GLEAN_BENCHMARK_RUNTIME_DIRECTORY` to a fixed runtime directory to use identical
LINQ, CoreLib and facade inputs across runtime jobs. Set
`GLEAN_BENCHMARK_FIXTURE_PATH` to a fixed fixture DLL for the same reason; its
matching `.pdb` is used by the debug workloads. Preserve the logged hashes when
comparing runs. Otherwise a runtime comparison can also change the input data.

When checking several flags on the same context, reading `Attributes` once and
applying the masks locally avoids repeated metadata access. The focused flag
cases retain both usage patterns so that this difference stays visible.

Reading several accessors has the same consideration: `Getter` and `Setter`
each query the property's accessor table, and event accessor properties do so
independently. When all accessors are needed together, retrieve
`property.Definition.GetAccessors()` or `eventContext.Definition.GetAccessors()`
once. The accessor benchmarks retain both forms and include context construction
when consuming grouped handles.

The resolution corpus registers only the source assembly, CoreLib and the
System.Runtime facade. It intentionally retains unresolved references rather
than silently loading more dependencies or dropping failures. The closure
loading workload is separate. These cases broaden coverage without establishing
exhaustive behavior or performance across all metadata, machines or platforms.
