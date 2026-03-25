## This library is in an early alpha/design state. API/breaking changes are most likely to still occur.

# Glean

Glean is intended to be a usability layer over `System.Reflection.Metadata` without sacraficing performance.

The goal is not to hide `System.Reflection.Metadata`. The goal is to remove the repetitive parts that
most users end up writing anyway while keeping the raw reader, handles, and
metadata structs available whenever you want them.

## Choose a Lane

Glean is easiest to use if you pick the lane that matches the cost model you
want:

- Fast traversal: `AssemblyScope`, `AssemblyContext`, `TypeContext`,
  `MethodContext`, struct enumerators, and identity
  helpers like `NameIs`, `NamespaceIs`, and `TryFind*`.
- Rich decoding: `Decode*` helpers, `TypeSignature`,
  `CustomAttributeDecoder`, and formatting
  helpers that materialize richer object graphs.
- Cross assembly resolution: `AssemblyClosure` for resolving
  type and member references across a dependency closure.

Raw `System.Reflection.Metadata` is always still here:

- `AssemblyScope.Reader`
- `AssemblyClosure.EntryReader`
- `AssemblyContext.Reader`
- `TypeContext.Reader`
- `MethodContext.Reader`

## Primary Entry Points

Use `AssemblyScope` when you only need one assembly:

```csharp
using var scope = AssemblyScope.Open(path);
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
using var closure = AssemblyClosure.Load(path, searchPaths);

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

## Cost Model

The naming is intentional:

- `Enumerate*`, `TryFind*`, `NameIs`, `NamespaceIs`, `HasAttribute`,
  `Get*Handle`, `MetadataTraversal.*`: fast tier and intended for low
  allocation traversal.
- `Decode*`, `TypeSignature.Accept(ref visitor, state)`, `ToFullNameString`,
  `TypeSignature.ToString()`: richer helpers for already decoded metadata.
- `Resolve*`: cross assembly APIs that may build and reuse lookup caches.

If you are on a hot path, stay on contexts, enumerators, handles, and the raw
reader. If you need a friendlier shape for signatures, attributes, or
resolution, move up a tier only where it pays for itself.

## Samples

Runnable samples live under [`samples`](samples/README.md):

- [FastTraversalSample](samples/FastTraversalSample/Program.cs)
- [RichDecodingSample](samples/RichDecodingSample/Program.cs)
- [ResolutionSample](samples/ResolutionSample/Program.cs)
- [InspectorSample](samples/InspectorSample/Program.cs)

The first three are starter samples. `InspectorSample` is a larger demo tool
that shows how the tiers mix in one flow. All of them are path driven and work
against real assemblies rather than test fixtures.