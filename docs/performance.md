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

The tests exclude fixture creation, reader setup, delegate construction, and
cache warmup from the measured interval. They use thread-local allocation counts;
some checks allow a small fixed tolerance across thousands of operations.

These are allocation regression tests, not timing benchmarks. Compare equivalent
work when measuring time: enumerating raw handles does less work than creating
contexts that also read their definitions. Report rich decoding and cold
resolution separately from warmed traversal.
