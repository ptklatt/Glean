## This library is in an early alpha/design state. API/breaking changes are most likely to still occur.

# Glean

Glean is a usability layer over `System.Reflection.Metadata` that keeps traversal costs explicit.

The goal is not to hide `System.Reflection.Metadata`. The goal is to remove the repetitive parts that
most users end up writing anyway while keeping the raw reader, handles, and
metadata structs available whenever you want them.

## Choose a Lane

Glean is easiest to use if you pick the lane that matches the cost model you
want:

- Fast traversal: `AssemblyScope`, `AssemblyContext`, `TypeContext`,
  `ModuleContext`, `MethodContext`, `MetadataTraversal`, struct enumerators, and identity
  helpers like `NameIs`, `NamespaceIs`, and `TryFind*`.
- Rich decoding: `Decode*` helpers, `TypeSignature`,
  `ITypeSignatureVisitor<TState>`, `CustomAttributeDecoder`, and formatting
  helpers that materialize richer object graphs.
- Cross assembly resolution: `AssemblyClosure` and `AssemblySet` for resolving
  type and member references across a dependency closure.

Raw `System.Reflection.Metadata` is always still here:

- `AssemblyScope.Reader`
- `AssemblyClosure.EntryReader`
- `AssemblyContext.Reader`
- `TypeContext.Reader`
- `MethodContext.Reader`

## Primary Entry Points

The examples run in a console project that references Glean. Pass an assembly
path as the first argument, followed by any dependency directories needed for
closure loading.

Use `AssemblyScope` when you only need one assembly:

```csharp
using Glean;

using var scope = AssemblyScope.Open(args[0]);
var assembly = scope.Context;

foreach (var type in assembly.EnumerateTypes())
{
    if (type.IsNested || !type.IsPublic)
    {
        continue;
    }

    Console.WriteLine(type.FullName);
}
```

Use `AssemblyClosure` when you need transitive dependencies and explicit
resolution:

```csharp
using Glean;
using Glean.Extensions;

using var closure = AssemblyClosure.Load(args[0], args.Skip(1).ToArray());

foreach (var typeReference in closure.EntryReader.EnumerateTypeReferences())
{
    if (!closure.Set.TryResolveType(
        closure.EntryReader,
        typeReference.Handle,
        out var targetReader,
        out var targetHandle))
    {
        continue;
    }

    var targetType = targetReader.GetTypeDefinition(targetHandle);
    Console.WriteLine(targetType.ToFullNameString(targetReader));
}
```

Assembly binding requires an exact name, version, normalized culture, and public
key token by default. To allow version substitutions, pass `AssemblyLoadOptions`
with `BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible` (both in
`Glean.Resolution`). Exact candidates always take priority; fallback uses search
path order and changes only the version. Inspect `closure.BindingSubstitutions`
for the requested identity, resolved identity, and file path. See the
[binding recipe](docs/recipes.md#configure-assembly-binding) for an example.

## Cost Model

Context enumerators, identity checks such as `NameIs` and `NamespaceIs`, and
`MetadataTraversal` use raw metadata without building signature graphs. Struct
visitors implement every callback explicitly; unused callbacks can have empty bodies.

`Decode*` and formatting helpers materialize richer values and can allocate.
`TypeSignature.Accept(ref visitor, state)` dispatches over an already decoded
signature; passing a struct visitor by reference avoids boxing.

Overloads that take an `AssemblySet` perform explicit resolution and may populate
caches. `EnumerateAllInterfaces(assemblySet)` also builds a queue and visited set.

If you are on a hot path, stay on contexts, enumerators, handles, and the raw
reader. If you need a friendlier shape for signatures, attributes, or
resolution, move up a tier only where it pays for itself.

See [performance notes](docs/performance.md) for allocation boundaries and the
scope of the regression tests.

## Samples

Runnable samples live under [`samples`](samples/README.md):

- [FastTraversalSample](samples/FastTraversalSample/Program.cs)
- [RichDecodingSample](samples/RichDecodingSample/Program.cs)
- [ResolutionSample](samples/ResolutionSample/Program.cs)
- [InspectorSample](samples/InspectorSample/Program.cs)

The first three are starter samples. `InspectorSample` is a larger demo tool
that shows how the tiers mix in one flow. All of them are path driven and work
against real assemblies rather than test fixtures.

## Recipes

[Usage recipes](docs/recipes.md) cover module metadata, visitors, portable PDBs,
signature decoding, and cross assembly resolution.
