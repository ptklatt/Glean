# Samples

This folder contains three small starter samples plus one larger inspector demo.

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
- [InspectorSample](InspectorSample/Program.cs): a fuller tool style sample that
  mixes fast traversal, selective rich decoding, raw `PEReader` access, and
  optional cross assembly resolution in one flow. Treat this as a demo tool or
  reference sample, not as the first sample to read.
