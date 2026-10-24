# Glean Recipes

These recipes are task focused snippets that pair with the runnable samples
under [`samples`](../samples/README.md).

Use a console project that references Glean. Each example takes an assembly path
as its first command-line argument. Closure examples accept dependency directories
after that path; the PDB example takes the matching portable PDB as its second
argument.

The intent is simple:

- Start on the fast tier.
- Move up to rich decoding only where the richer shape pays for itself.
- Use resolution only when you need to cross assembly boundaries.

## Open One Assembly and Enumerate Public Top Level Types

Use `AssemblyScope` when you only need one assembly. Stay on contexts and
struct enumerators when you want low overhead traversal.

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

Why this shape:

- `AssemblyScope` owns the lifetime of the underlying file handle.
- `AssemblyContext` is the primary fast tier entry point.
- `EnumerateTypes()` returns a struct enumerator.
- `TypeContext.FullName` allocates, so keep it for output paths rather than
  hot filtering logic.

## Use ModuleContext For Module Wide Reference Tables

When you need `TypeRef`, `MemberRef`, `AssemblyRef`, or `ModuleRef` access,
start from `AssemblyContext.Module` instead of dropping straight to raw SRM.

```csharp
using Glean;

using var scope = AssemblyScope.Open(args[0]);
var module = scope.Context.Module;

foreach (var typeReference in module.EnumerateTypeReferences())
{
    if (!typeReference.Is("System", "Console"))
    {
        continue;
    }

    Console.WriteLine(typeReference.FullName);
}
```

Why this shape:

- `ModuleContext` keeps the low level reader visible but removes the common
  `reader.GetModuleDefinition()` boilerplate.
- The reference-table enumerators stay on the fast tier and do not decode
  signatures by themselves.
- `AssemblyContext` still remains the right entry point for assembly identity,
  manifest resources, and exported types.

## Traverse Metadata With a Profiled Visitor

Use `MetadataTraversal` when you want one pass over a reader with explicit
coverage and no custom outer traversal loops.

```csharp
using Glean;
using Glean.Contexts;
using Glean.Visitors;

using var scope = AssemblyScope.Open(args[0]);

var visitor = new PublicMethodCounter();
MetadataTraversal.TraverseMethodsOnly(scope.Reader, ref visitor);

Console.WriteLine(visitor.Count);

internal struct PublicMethodCounter : IMetadataVisitor
{
    public int Count;

    public bool VisitType(in TypeContext context)
    {
        return true;
    }

    public void VisitMethod(in MethodContext context)
    {
        if (context.IsPublic)
        {
            Count++;
        }
    }

    public void VisitField(in FieldContext context)
    {
    }

    public void VisitProperty(in PropertyContext context)
    {
    }

    public void VisitEvent(in EventContext context)
    {
    }

    public void VisitParameter(in ParameterContext context)
    {
    }

    public void VisitGenericParameter(in GenericParameterContext context)
    {
    }

    public void VisitCustomAttribute(in CustomAttributeContext context)
    {
    }

    public void VisitTypeReference(in TypeReferenceContext context)
    {
    }

    public void VisitMemberReference(in MemberReferenceContext context)
    {
    }

    public void VisitMethodImplementation(in MethodImplementationContext context)
    {
    }

    public void VisitExportedType(in ExportedTypeContext context)
    {
    }

    public void VisitManifestResource(in ManifestResourceContext context)
    {
    }
}
```

Implement every callback explicitly, using empty bodies for notifications you do
not need. Choose a narrower profile to avoid reading unwanted tables. Return
false from `VisitType` to skip that type's members and nested types.

Use the narrower `TraverseMethodsOnly` or `TraverseMethodAttributesOnly`
profiles when you do not need fields, properties, events, or reference tables.
Each of these profiles also has a stateful overload when you want the visitor
logic separated from caller owned aggregation state.

When you need rarer tables too, move up explicitly:

- `TraverseWithAssemblyReferences` adds `AssemblyRef`, `ModuleRef`, and `File`.
- `TraverseWithSignatureTables` adds `TypeSpec`, `MethodSpec`, and `StandAloneSig`.
- `TraverseWithConstantAndDebugTables` adds `Constant`, `DeclSecurity`, and `CustomDebugInformation`.
- `TraverseAllTables` combines the standard profile with all of the advanced ones.

## Find a Specific Type Without Materializing Strings

If you are looking for a known type, use `TypeContext.Is`, `NameIs`, and
`NamespaceIs` rather than comparing `Name` or `Namespace`.

```csharp
using Glean;

using var scope = AssemblyScope.Open(args[0]);

if (scope.Context.TryFindType("MyApp.Models", "Order", out var orderType))
{
    Console.WriteLine(orderType.FullName);
}
```

Why this shape:

- `candidate.Is(ns, name)` uses SRM string comparer identity checks.
- You only pay for `FullName` when you actually print or persist the result.
- There is no separate mandatory lookup abstraction here; the context plus
  comparer helpers are enough for many scans.

## Inspect a Base Type Without Starting From EntityHandle

`TypeContext.BaseType` stays available, but the context now also exposes typed
helpers so callers do not need to switch on `HandleKind` immediately.

```csharp
using Glean;

using var scope = AssemblyScope.Open(args[0]);

if (scope.Context.TryFindType("MyApp.Models", "Order", out var orderType))
{
    if (orderType.TryGetBaseTypeDefinition(out var localBase))
    {
        Console.WriteLine($"local base: {localBase.FullName}");
    }
    else if (orderType.TryGetBaseTypeReference(out var referencedBase))
    {
        Console.WriteLine($"referenced base: {referencedBase.FullName}");
    }
    else
    {
        Console.WriteLine($"decoded base: {orderType.DecodeBaseType()}");
    }
}
```

Use `DecodeBaseType()` when the base type is encoded as a type specification,
for example a generic instantiation such as `List<int>`.

## Walk Only Local Base Types

If you only care about base types defined in the same module, stay on the fast
tier and avoid `AssemblySet` entirely.

```csharp
using Glean;

using var scope = AssemblyScope.Open(args[0]);

if (scope.Context.TryFindType("MyApp.Models", "DerivedOrder", out var derivedType))
{
    foreach (var baseType in derivedType.EnumerateBaseTypes())
    {
        Console.WriteLine(baseType.FullName);
    }
}
```

Notes:

- This path follows only local `TypeDefinition` base types.
- It stops when the next base type is external or encoded as a type
  specification.
- Use `EnumerateBaseTypes(assemblySet)` when the walk needs to cross assembly
  boundaries.

## Walk Base Types And Interfaces Across Assemblies

Use the overloads that take an `AssemblySet` when references cross module or
assembly boundaries.

```csharp
using Glean;
using Glean.Extensions;

using var closure = AssemblyClosure.Load(args[0], args.Skip(1).ToArray());

if (closure.EntryContext.TryFindType("MyApp.Models", "DerivedOrder", out var type))
{
    foreach (var baseType in type.EnumerateBaseTypes(closure.Set))
    {
        Console.WriteLine($"base: {baseType.FullName}");
    }

    foreach (var interfaceType in type.EnumerateAllInterfaces(closure.Set))
    {
        Console.WriteLine($"interface: {interfaceType.FullName}");
    }
}
```

Base walks stop on resolution failure or detected cycles. Interface walks follow
direct interfaces, parent interfaces, and base classes, returning each resolved
interface definition once. Generic instances are represented by their definitions.
Interface traversal allocates a queue and visited set; the local-only base walk
above does not.

## Read Portable PDB Sequence Points For A Method

Open the assembly and its matching portable PDB separately. The readers and
contexts remain valid until their owning scopes are disposed.

```csharp
using Glean;
using Glean.Contexts;

using var scope = AssemblyScope.Open(args[0]);
using var pdb = MetadataFactory.CreateFromPdb(args[1]);

if (scope.Context.TryFindType("MyApp", "Program", out var programType))
{
    foreach (var method in programType.EnumerateMethods())
    {
        foreach (var sequencePoint in method.EnumerateSequencePoints(pdb.Reader))
        {
            if (sequencePoint.IsHidden || sequencePoint.Document.IsNil)
            {
                continue;
            }

            var document = DocumentContext.Create(pdb.Reader, sequencePoint.Document);
            Console.WriteLine($"{document.Name}:{sequencePoint.StartLine}");
        }
    }
}
```

Sequence points require the PDB reader for the same module as the method.
`DocumentContext` wraps the PDB's source document metadata. Reading its `Name`
decodes a string, so defer it until a source location is needed.

## Check for an Attribute Without Decoding It

Most attribute checks do not need full decoding.

```csharp
using Glean;

using var scope = AssemblyScope.Open(args[0]);

foreach (var type in scope.Context.EnumerateTypes())
{
    if (!type.HasAttribute("System", "ObsoleteAttribute"))
    {
        continue;
    }

    Console.WriteLine(type.FullName);
}
```

If you need the handle for follow up work, use `TryFindAttribute(...)` instead
of decoding every attribute upfront.

## Decode a Custom Attribute Only When You Need Its Arguments

`CustomAttributeDecoder` is the rich tier entry point for attributes. Use it
after a cheap fast tier filter.

```csharp
using Glean;
using Glean.Decoding;

using var scope = AssemblyScope.Open(args[0]);

foreach (var type in scope.Context.EnumerateTypes())
{
    if (!type.TryFindAttribute("System", "ObsoleteAttribute", out var attribute))
    {
        continue;
    }

    var decoded = CustomAttributeDecoder.Decode(scope.Reader, attribute.Handle);
    Console.WriteLine(decoded.AttributeType);

    foreach (var argument in decoded.FixedArguments)
    {
        Console.WriteLine($"  ctor: {argument.Type} = {argument.Value}");
    }
}
```

Notes:

- `CustomAttributeDecoder.Decode(...)` allocates decoded object graphs.
- External enum values may require an `IEnumResolver` if the underlying type is
  not available locally.
- If all you need is presence or a handle, stay on `HasAttribute` or
  `TryFindAttribute`.

## Decode a Full Method Signature

For common cases, `MethodContext.DecodeReturnType()` is enough. When you need
parameters and generic arity too, use the lower level signature decode APIs.

```csharp
using Glean;
using Glean.Providers;

using var scope = AssemblyScope.Open(args[0]);

foreach (var type in scope.Context.EnumerateTypes())
{
    if (!type.Is("MyApp.Services", "OrderService"))
    {
        continue;
    }

    foreach (var method in type.EnumerateMethods())
    {
        if (!method.NameIs("Create"))
        {
            continue;
        }

        var signature = method.DecodeSignature(
            SignatureTypeProvider.Instance,
            SignatureDecodeContext.Empty);

        Console.WriteLine($"{method.Name} returns {signature.ReturnType}");

        foreach (var parameterType in signature.ParameterTypes)
        {
            Console.WriteLine($"  param: {parameterType}");
        }
    }
}
```

`ParameterTypes` describes signature positions. `EnumerateParameters()` reads
optional Param metadata rows, including return metadata when present; those rows
can be sparse and should not be matched to signature positions by list index.

## Walk a Decoded Signature With a Visitor

When you already have a decoded `TypeSignature`, use a visitor when you want to
project it into your own model without reopening `switch` blocks throughout
your codebase.

```csharp
using Glean;
using Glean.Signatures;

using var scope = AssemblyScope.Open(args[0]);

if (scope.Context.TryFindType("MyApp.Services", "OrderService", out var serviceType))
{
    foreach (var method in serviceType.EnumerateMethods())
    {
        if (!method.NameIs("Create"))
        {
            continue;
        }

        var signature = method.DecodeReturnType();
        var visitor = new NamedTypeCounter();
        signature.Accept(ref visitor, state: (object?)null);

        Console.WriteLine($"{method.Name} return type mentions {visitor.Count} named type nodes");
    }
}

internal sealed class NamedTypeCounter : ITypeSignatureVisitor<object?>
{
    public int Count { get; private set; }

    public void VisitArray(ArraySignature signature, object? state)
    {
        VisitChild(signature.ElementType);
    }

    public void VisitByRef(ByRefSignature signature, object? state)
    {
        VisitChild(signature.ElementType);
    }

    public void VisitFunctionPointer(FunctionPointerSignature signature, object? state)
    {
        VisitChild(signature.Signature.ReturnType);
        foreach (var parameterType in signature.Signature.ParameterTypes)
        {
            VisitChild(parameterType);
        }
    }

    public void VisitGenericInstance(GenericInstanceSignature signature, object? state)
    {
        VisitChild(signature.GenericType);
        foreach (var argument in signature.Arguments)
        {
            VisitChild(argument);
        }
    }

    public void VisitGenericMethodParameter(GenericMethodParameterSignature signature, object? state)
    {
    }

    public void VisitGenericTypeParameter(GenericTypeParameterSignature signature, object? state)
    {
    }

    public void VisitModifiedType(ModifiedTypeSignature signature, object? state)
    {
        VisitChild(signature.UnmodifiedType);
        foreach (var modifier in signature.RequiredModifiers)
        {
            VisitChild(modifier);
        }

        foreach (var modifier in signature.OptionalModifiers)
        {
            VisitChild(modifier);
        }
    }

    public void VisitPinnedType(PinnedTypeSignature signature, object? state)
    {
        VisitChild(signature.ElementType);
    }

    public void VisitPointer(PointerSignature signature, object? state)
    {
        VisitChild(signature.ElementType);
    }

    public void VisitPrimitive(PrimitiveTypeSignature signature, object? state)
    {
    }

    public void VisitSerializedTypeName(SerializedTypeNameSignature signature, object? state)
    {
        Count++;
    }

    public void VisitSentinel(SentinelTypeSignature signature, object? state)
    {
    }

    public void VisitSZArray(SZArraySignature signature, object? state)
    {
        VisitChild(signature.ElementType);
    }

    public void VisitTypeDefinition(TypeDefinitionSignature signature, object? state)
    {
        Count++;
    }

    public void VisitTypeReference(TypeReferenceSignature signature, object? state)
    {
        Count++;
    }

    private void VisitChild(TypeSignature signature)
    {
        var self = this;
        signature.Accept(ref self, state: (object?)null);
    }
}
```

Notes:

- Visitor dispatch itself is cheap on an already decoded signature graph.
- `Accept` dispatches the current node only. The visitor chooses which child
  signatures to visit.
- The graph still comes from `Decode*` APIs, so decoding cost does not
  disappear.
- Passing a struct visitor by `ref` avoids boxing.

## Drop to Raw SRM or PEReader From a Context

Glean is intended to be additive, not a wall around SRM. The contexts keep the
raw metadata objects visible.

```csharp
using System.Reflection.Metadata;

using Glean;

using var scope = AssemblyScope.Open(args[0]);

foreach (var type in scope.Context.EnumerateTypes())
{
    if (!type.Is("MyApp", "Widget"))
    {
        continue;
    }

    var definition = type.Definition;
    var reader = type.Reader;

    foreach (var methodHandle in definition.GetMethods())
    {
        var method = reader.GetMethodDefinition(methodHandle);
        if (method.RelativeVirtualAddress == 0)
        {
            continue;
        }

        var body = scope.PeReader.GetMethodBody(method.RelativeVirtualAddress);
        Console.WriteLine($"{reader.GetString(method.Name)}: {body.GetILContent().Length} bytes of IL");
    }
}
```

Use this pattern when SRM or `PEReader` already exposes exactly what you need.

## Load a Dependency Closure and Require Completeness

Use `AssemblyClosure` when you want a single owned lifetime for an entry
assembly and everything it references.

```csharp
using Glean;

using var closure = AssemblyClosure.Load(args[0], args.Skip(1).ToArray());
closure.ThrowIfPartial();

Console.WriteLine(closure.EntryContext.Name);
Console.WriteLine($"Loaded: {closure.LoadedAssemblies.Count}");
```

If partial closures are acceptable, inspect `SkippedDependencies` instead of
throwing.

```csharp
using Glean;

using var closure = AssemblyClosure.Load(args[0], args.Skip(1).ToArray());

foreach (var skipped in closure.SkippedDependencies)
{
    Console.WriteLine($"{skipped.AssemblySimpleName}: {skipped.Kind}");
}
```

`ThrowIfPartial()` reports skipped dependency loads. It does not guarantee that
every type or member reference resolves; check the result of each resolution
request.

## Resolve Type References Across Assemblies

Once you have an `AssemblyClosure`, use `AssemblySet` for explicit type
resolution.

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
        out var targetHandle,
        out var reason))
    {
        Console.WriteLine($"{typeReference.FullName}: {reason}");
        continue;
    }

    var targetType = targetReader.GetTypeDefinition(targetHandle);
    Console.WriteLine($"{typeReference.FullName} -> {targetType.ToFullNameString(targetReader)}");
}
```

Notes:

- `TryResolveType(...)` is explicit by design. Resolution is a richer,
  cross assembly concern.
- The failure reason is useful for tools that need to report why a reference
  did not bind.
- `ResolutionDiagnostics.GetActionableMessage(reason)` turns the enum into a
  user-facing explanation without hard-coding your own message table.

The same operation is available on each `TypeReferenceContext`:

```csharp
using Glean;
using Glean.Extensions;
using Glean.Resolution;

using var closure = AssemblyClosure.Load(args[0], args.Skip(1).ToArray());

foreach (var typeReference in closure.EntryReader.EnumerateTypeReferences())
{
    if (typeReference.TryResolve(closure.Set, out var resolvedType, out var reason))
    {
        Console.WriteLine(resolvedType.FullName);
    }
    else
    {
        Console.WriteLine(ResolutionDiagnostics.GetActionableMessage(reason));
    }
}
```

## Follow Exported Type Forwarders To Their Final Scope

Exported types are mostly relevant for forwarding and niche resolution work, so
the helper stays explicit.

```csharp
using Glean;

using var scope = AssemblyScope.Open(args[0]);

foreach (var exportedType in scope.Context.EnumerateExportedTypes())
{
    if (!exportedType.TryGetForwardedResolutionScope(out var finalScope))
    {
        continue;
    }

    Console.WriteLine($"{exportedType.Name} -> {finalScope.Kind}");
}
```

Notes:

- This follows nested `ExportedType` links until it reaches the final scope.
- The final scope is usually an `AssemblyReference`, but it can also be a
  `File` for multi-module assemblies.
- If you only need the raw metadata, `ExportedTypeContext.Implementation`
  remains available.

## Resolve Member References, and Add an Index Only When It Gets Hot

Start with `AssemblySet.TryResolveMember(...)` directly.

```csharp
using Glean;
using Glean.Extensions;

using var closure = AssemblyClosure.Load(args[0], args.Skip(1).ToArray());

foreach (var memberReference in closure.EntryReader.EnumerateMemberReferences())
{
    if (!closure.Set.TryResolveMember(
        closure.EntryReader,
        memberReference.Handle,
        out var targetReader,
        out var targetHandle,
        out var reason))
    {
        Console.WriteLine($"{memberReference.Name}: {reason}");
        continue;
    }

    Console.WriteLine($"{memberReference.Name} -> {targetHandle.Kind}");
}
```

If that path becomes hot, add a `MemberResolutionIndex`.

```csharp
using Glean;
using Glean.Extensions;
using Glean.Resolution;

using var closure = AssemblyClosure.Load(args[0], args.Skip(1).ToArray());
var index = new MemberResolutionIndex(cacheSemanticSignatures: false);

foreach (var memberReference in closure.EntryReader.EnumerateMemberReferences())
{
    if (!closure.Set.TryResolveMember(
        closure.EntryReader,
        memberReference.Handle,
        index,
        out var targetReader,
        out var targetHandle,
        out _))
    {
        continue;
    }

    Console.WriteLine($"{memberReference.Name} -> {targetHandle.Kind}");
}
```

The matching `MemberReferenceContext.TryResolve` overloads accept the same
assembly set and optional member index, with an optional failure reason.

For base-chain and inherited-interface walks, `TypeContext` also exposes
resolution helpers like `TryGetBaseType(...)`, `EnumerateBaseTypes(...)`, and
`EnumerateAllInterfaces(...)` on top of the same `AssemblySet`.

Rule of thumb:

- Use plain `TryResolveMember(...)` first.
- Add `MemberResolutionIndex` only for repeated resolution workloads where the
  same parent types are hit often enough to justify extra cache state.
