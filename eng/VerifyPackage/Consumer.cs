using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using CustomIrSample;

using Glean;
using Glean.Contexts;
using Glean.Extensions;
using Glean.Providers;
using Glean.Resolution;

int expectedRuntime = int.Parse(args[0]);
string expectedAsset = ".NETCoreApp,Version=v" + args[1] + ".0";
var assembly = typeof(AssemblyScope).Assembly;
Require(Environment.Version.Major == expectedRuntime, "Consumer ran on the wrong runtime.");
Require(assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName == expectedAsset,
    "Consumer loaded the wrong Glean target.");
Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) == args[2],
    "Consumer loaded an assembly that differs from the produced package.");

using var scope = AssemblyScope.Open(assembly.Location);
int count = 0;
bool foundScope = false;
foreach (var type in scope.Context.EnumerateTypes())
{
    if (type.IsPublic)
    {
        count++;
        foundScope |= type.NameIs("AssemblyScope");
    }
}
Require((count > 0) && foundScope, "Package traversal did not find its public entry point.");

string consumerPath = typeof(PackageChecks.Fixture).Assembly.Location;
// The .NET 9 host uses the .NET 8 asset, so framework references may need version substitution.
using var closure = AssemblyClosure.Load(consumerPath, new AssemblyLoadOptions
{
    SearchPaths = new[] { Path.GetDirectoryName(consumerPath)!, Path.GetDirectoryName(typeof(object).Assembly.Location)! },
    BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible
}).ThrowIfPartial();

Require(closure.TryGetPeReader(closure.EntryReader, out var entryImage) && ReferenceEquals(entryImage, closure.EntryPeReader),
    "Closure did not return its borrowed entry PE reader.");
Require(closure.TryGetManagedEntryPoint(out var entryMethod) && ReferenceEquals(entryMethod.Reader, closure.EntryReader),
    "Consumer entry point did not retain its owning metadata reader.");
Require(!entryMethod.Definition.GetILSpan(entryImage!).IsEmpty, "Consumer entry point IL was not available.");

using var entryScope = AssemblyScope.Open(consumerPath);
Require(entryScope.TryGetManagedEntryPoint(out var scopedMethod) && ReferenceEquals(scopedMethod.Reader, entryScope.Reader),
    "Scoped entry point did not retain its owning reader.");
using var loader = new AssemblyLoader();
var loadedReader = loader.LoadFile(consumerPath);
Require(loader.TryGetPeReader(loadedReader, out var loadedImage) && loadedImage.HasMetadata,
    "Loader did not expose its borrowed PE image.");
using var resolver = new DirectoryAssemblyResolver(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
var runtimeReference = closure.EntryReader.AssemblyReferences.Single(handle =>
    closure.EntryReader.GetString(closure.EntryReader.GetAssemblyReference(handle).Name) == "System.Runtime");
Require(resolver.TryResolve(runtimeReference, closure.EntryReader, out var runtimeReader) &&
    resolver.TryGetPeReader(runtimeReader!, out var runtimeImage) && runtimeImage.HasMetadata,
    "Directory resolver did not expose its resolved PE image.");

bool resolvedDisposable = false;
foreach (var reference in closure.EntryContext.Module.EnumerateTypeReferences())
{
    if (!reference.NamespaceIs("System") || !reference.NameIs("IDisposable"))
    {
        continue;
    }

    Require(closure.Set.TryResolveType(closure.EntryReader, reference.Handle, out var reader, out var handle),
        "Closure resolution could not find IDisposable.");
    var resolved = TypeContext.Create(reader, handle);
    Require(resolved.NamespaceIs("System") && resolved.NameIs("IDisposable"), "Closure resolved the wrong type.");
    resolvedDisposable = true;
    break;
}
Require(resolvedDisposable, "Consumer fixture did not expose a reference to resolve.");

Require(closure.EntryContext.TryFindType("PackageChecks", "Fixture", out var fixture), "Consumer fixture was not found.");
MethodContext method = default;
foreach (var candidate in fixture.EnumerateMethods())
{
    if (candidate.NameIs("Inspect"))
    {
        method = candidate;
        break;
    }
}
Require(method.IsValid, "Consumer method was not found.");
var signature = method.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
Require((signature.ParameterTypes.Length == 4) && (signature.GenericParameterCount == 1), "Signature shape differs.");
Require(method.TryGetParameter(0, out var first) && (first.Name == "count") && (first.SequenceNumber == 1),
    "First parameter did not map to signature position zero.");
Require(method.TryGetParameter(2, out var optional) && (optional.Name == "label") && (optional.SequenceNumber == 3),
    "Optional parameter did not map to signature position two.");
Require(optional.IsOptional && optional.HasDefault, "Optional parameter metadata differs.");
Require(first.DeclaringMethod.Equals(method), "Parameter mapping lost the declaring method.");
Require(method.TryGetReturnParameter(out var result) && (result.SequenceNumber == 0), "Return parameter metadata was not found.");
Require(!method.TryGetParameter(4, out _), "Out of range parameter mapping succeeded.");

var custom = method.DecodeSignature(new IrTypeProvider(closure.Set), default(IrGenericContext));
Require(custom.ReturnType is IrType.Vector { Element: IrType.GenericParameter { Index: 0, IsMethod: true } },
    "Custom IR lost the generic array return type.");
Require(custom.ParameterTypes[0] is IrType.Primitive { Code: PrimitiveTypeCode.Int32 }, "Custom IR lost the primitive parameter.");
Require(custom.ParameterTypes[1] is IrType.Wrapped { Kind: Wrapper.ByReference, Element: IrType.GenericParameter { Index: 0, IsMethod: true } },
    "Custom IR lost the generic by reference parameter.");
Require(custom.ParameterTypes[3] is IrType.Named named &&
    (named.Identity.FullName == "System.IDisposable") &&
    (named.Definition?.FullName == "System.IDisposable") &&
    (named.Failure == ResolutionFailureReason.None),
    "Custom IR did not retain and resolve the named parameter type.");

Require(closure.EntryContext.TryFindType("PackageChecks", "Constrained`1", out var constrained), "Constraint fixture was not found.");
var genericParameters = constrained.EnumerateGenericParameters();
Require(genericParameters.MoveNext(), "Generic parameter was not found.");
var genericParameter = genericParameters.Current;
Require(genericParameter.ConstraintCount == 1, "Constraint row count differs.");
var constraints = genericParameter.EnumerateConstraints();
Require(constraints.MoveNext(), "Constraint row was not found.");
var constraint = constraints.Current;
Require(ReferenceEquals(constraint.Reader, closure.EntryReader) && (constraint.Parameter == genericParameter.Handle),
    "Constraint context lost its owner or parameter.");
Require(constraint.Type.Kind == HandleKind.TypeSpecification, "Constraint type is not a TypeSpec.");
Require(constraint.DecodeType() is Glean.Signatures.GenericInstanceSignature, "Constraint signature was not decoded.");
var irProvider = new IrTypeProvider();
var irContext = new IrGenericContext(System.Collections.Immutable.ImmutableArray.Create<IrType>(
    new IrType.Primitive(PrimitiveTypeCode.String)));
var decodedConstraint = constraint.DecodeType(irProvider, irContext);
var decodedSpecification = ((TypeSpecificationHandle)constraint.Type).DecodeTypeSpecification(closure.EntryReader, irProvider, irContext);
Require(decodedConstraint is IrType.GenericInstance constraintInstance &&
    (constraintInstance.Arguments[0] == irContext.TypeArguments[0]) &&
    decodedSpecification is IrType.GenericInstance specificationInstance &&
    (specificationInstance.Arguments[0] == irContext.TypeArguments[0]),
    "Constraint or TypeSpec custom-provider decoding lost its generic context.");

Console.WriteLine($"ConsumerRuntime={RuntimeInformation.FrameworkDescription} GleanTarget={expectedAsset} PublicTypes={count} SRMPath={typeof(MetadataReader).Assembly.Location} Checks=traversal,closure,parameters,custom-ir,pe-access,entry-point,constraints,typespec");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

namespace PackageChecks
{
    public sealed class Constrained<T> where T : IComparable<T>
    {
    }

    public sealed class Fixture : IDisposable
    {
        [return: MaybeNull]
        public static T[] Inspect<T>(int count, ref T value, string label = "guest", IDisposable? resource = null)
        {
            return new[] { value };
        }

        public void Dispose()
        {
        }
    }
}
