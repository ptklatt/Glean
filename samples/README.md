# Samples

This folder contains four small starter samples plus one larger inspector demo.

For task focused snippets, see the [usage recipes](../docs/recipes.md).

## Run

Fast traversal:

```bash
dotnet run --project samples/FastTraversalSample -- <assembly-path>
```

Rich decoding:

```bash
dotnet run --project samples/RichDecodingSample -- <assembly-path> [full-type-name]
```

Cross assembly resolution:

```bash
dotnet run --project samples/ResolutionSample -- <entry-assembly-path> [search-path...]
```

Custom signature representation:

```bash
dotnet run --project samples/CustomIrSample -- <assembly-path> [full-type-name]
dotnet run --project samples/CustomIrSample -- <assembly-path> [full-type-name] --resolve --search <dependency-directory>
```

Full end to end inspection:

```bash
dotnet run --project samples/InspectorSample -- <assembly-path> [--type <full-type-name>] [--search <directory>]...
```

When using `InspectorSample`, pass real dependency directories with `--search`
if you want the resolution section to bind framework and external references
cleanly.

## Sample Map

- [FastTraversalSample](FastTraversalSample/Program.cs): stay on the fast tier
  and use contexts and struct enumerators for cheap traversal.
- [RichDecodingSample](RichDecodingSample/Program.cs): decode signatures and
  custom attributes into richer object graphs when readability matters more than
  raw traversal cost.
- [ResolutionSample](ResolutionSample/Program.cs): load a dependency closure and
  resolve type and member references across assemblies.
- [CustomIrSample](CustomIrSample/Program.cs): decode directly into an application
  type model, join parameter and return metadata, and optionally resolve named
  types while retaining unresolved identities.
- [InspectorSample](InspectorSample/Program.cs): a fuller tool style sample that
  mixes fast traversal, selective rich decoding, raw `PEReader` access, and
  optional cross assembly resolution in one flow. Treat this as a demo tool or
  reference sample, not as the first sample to read.

## Custom Signature Models

[IrTypeProvider](CustomIrSample/IrTypeProvider.cs) implements
`ISignatureTypeProvider<IrType, IrGenericContext>` directly. Use this approach when
your application already has its own type representation. For transformations
of Glean's standard signature tree, decode into `TypeSignature` first and use
`TypeSignatureVisitor` instead. Both approaches materialize objects; this sample
does not demonstrate the allocation-free traversal tier.

The immutable [model](CustomIrSample/IrType.cs) retains array rank and bounds,
generic arguments, modifier order, pointer/reference/pinned wrappers, and the
complete function-pointer signature. SRM reports a vararg sentinel through
`MethodSignature<T>.RequiredParameterCount`; there is no sentinel provider
callback. The display inserts `...` at that boundary.

[MethodReport](CustomIrSample/MethodReport.cs) decodes with
`MethodContext.DecodeSignature(provider, context)`. It joins each zero-based
signature position through `TryGetParameter`, and uses `TryGetReturnParameter`
for sequence zero. Missing or unnamed metadata rows do not shift parameters.
The provider can also decode field, local and type-specification signatures
through the corresponding raw SRM APIs.

`--resolve` opens an assembly closure; `--search` supplies dependency directories
and enables resolution. Missing dependencies remain named nodes containing the
original full scope, nested name, source module ID, token and failure reason.
Resolved nodes also retain the definition identity. These identities contain
copied values and remain usable after the metadata scopes close. The sample
keeps generic parameters formal unless you explicitly supply substitution
arguments in `IrGenericContext`.
