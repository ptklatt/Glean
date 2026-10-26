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
Warm resolution lookups reuse caches; their allocation behavior does not describe
the first lookup or the cost of opening dependencies. Allocation checks do not
establish execution-time parity with raw SRM.

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
- warm cached type resolution stays allocation free on repeated lookups
- warm cached member resolution with `MemberResolutionIndex` stays allocation
  free on repeated lookups
- generic `Parameter.TryGetDefaultValue<T>` decoding stays allocation free for
  each supported primitive type (`bool`, `char`, signed and unsigned integers,
  `float`, and `double`) in repeated successful calls on the tested Release runtime

The tests exclude fixture creation, reader setup, delegate construction, and
cache warmup from the measured interval. They use thread-local allocation counts;
some checks allow a small fixed tolerance across thousands of operations.

These are allocation regression tests, not timing benchmarks. Compare equivalent
work when measuring time: enumerating raw handles does less work than creating
contexts that also read their definitions. Report rich decoding and cold
resolution separately from warmed traversal.

## Equivalent SRM Benchmarks

[Glean.Benchmarks](../bench/Glean.Benchmarks) targets .NET 9 and uses
[BenchmarkDotNet 0.15.8](https://www.nuget.org/packages/BenchmarkDotNet/0.15.8).
Every pair returns the same checksum. Global setup builds deterministic metadata
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
dotnet run --project bench/Glean.Benchmarks -c Release -- --validate
```

Run a smoke check to verify generated benchmark processes and exporters:

```sh
dotnet run --project bench/Glean.Benchmarks -c Release -- --filter '*' --job Dry --artifacts /tmp/glean-benchmarks-smoke --stopOnFirstError
```

Dry runs include startup effects and do not establish steady state timing or
allocation behavior. Failed builds or workloads can leave empty reports; check
that all 18 benchmarks have measurements.

Run measurements on an otherwise idle machine:

```sh
dotnet run --project bench/Glean.Benchmarks -c Release -- --filter '*' --job Short --warmupCount 5 --iterationCount 15 --launchCount 2 --outliers DontRemove --artifacts /tmp/glean-benchmarks-results --stopOnFirstError
```

This collects 15 measured iterations per launch across two launches, each after
five warmup iterations, retaining outliers. `MemoryDiagnoser`, the `Median`
column, and full JSON export are enabled. Markdown, CSV, HTML, and JSON reports
are written under the artifact directory's `results/` folder; full JSON includes
individual measurements. See the
official [command line guide](https://benchmarkdotnet.org/articles/guides/console-args.html)
for filtering and longer jobs. CI builds the project and validates workload
outputs.

To compare medians, divide Glean's `Median` by SRM's `Median` from the table or
JSON statistics. BenchmarkDotNet's default `Ratio` column instead reports a ratio
distribution based on means. Inspect individual measurements, errors, and
allocations alongside either comparison.

Results describe this fixture on the recorded runtime and hardware. Keep fast
traversal, warm cache hits, rich decoding, and cold resolution results separate:
their work and allocation behavior differ. Preserve the command, revision, and
environment information with the reports so comparisons remain reproducible.
