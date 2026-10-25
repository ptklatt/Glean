using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Glean.Resolution;

namespace CustomIrSample;

internal sealed class IrTypeProvider(AssemblySet? assemblies = null) : ISignatureTypeProvider<IrType, IrGenericContext>
{
    public IrType GetPrimitiveType(PrimitiveTypeCode typeCode) => new IrType.Primitive(typeCode);

    public IrType GetSZArrayType(IrType elementType) => new IrType.Vector(elementType);

    public IrType GetArrayType(IrType elementType, ArrayShape shape) => new IrType.Array(elementType, shape);

    public IrType GetPointerType(IrType elementType) => new IrType.Wrapped(elementType, Wrapper.Pointer);

    public IrType GetByReferenceType(IrType elementType) => new IrType.Wrapped(elementType, Wrapper.ByReference);

    public IrType GetPinnedType(IrType elementType) => new IrType.Wrapped(elementType, Wrapper.Pinned);

    public IrType GetGenericInstantiation(IrType genericType, ImmutableArray<IrType> typeArguments)
        => new IrType.GenericInstance(genericType, typeArguments);

    public IrType GetModifiedType(IrType modifier, IrType unmodifiedType, bool isRequired)
        => new IrType.Modified(modifier, unmodifiedType, isRequired);

    public IrType GetFunctionPointerType(MethodSignature<IrType> signature) => new IrType.FunctionPointer(signature);

    public IrType GetGenericTypeParameter(IrGenericContext genericContext, int index)
    {
        return (!genericContext.TypeArguments.IsDefaultOrEmpty) && (index >= 0) && (index < genericContext.TypeArguments.Length)
            ? genericContext.TypeArguments[index]
            : new IrType.GenericParameter(index, IsMethod: false);
    }

    public IrType GetGenericMethodParameter(IrGenericContext genericContext, int index)
    {
        return (!genericContext.MethodArguments.IsDefaultOrEmpty) && (index >= 0) && (index < genericContext.MethodArguments.Length)
            ? genericContext.MethodArguments[index]
            : new IrType.GenericParameter(index, IsMethod: true);
    }

    public IrType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var identity = DefinitionIdentity(reader, handle);
        return new IrType.Named(identity, rawTypeKind, identity);
    }

    public IrType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var identity = ReferenceIdentity(reader, handle);
        if (assemblies == null)
        {
            return new IrType.Named(identity, rawTypeKind);
        }

        return assemblies.TryResolveType(reader, handle, out var targetReader, out var targetHandle, out var reason)
            ? new IrType.Named(identity, rawTypeKind, DefinitionIdentity(targetReader, targetHandle))
            : new IrType.Named(identity, rawTypeKind, Failure: reason);
    }

    public IrType GetTypeFromSpecification(MetadataReader reader, IrGenericContext genericContext,
        TypeSpecificationHandle handle, byte rawTypeKind)
    {
        // Reuse this provider and context when a signature refers to another signature.
        return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }

    internal static NamedTypeIdentity DefinitionIdentity(MetadataReader reader, TypeDefinitionHandle handle)
    {
        int token = MetadataTokens.GetToken(handle);
        var names = new List<string>();
        string nameSpace = string.Empty;
        while (!handle.IsNil)
        {
            if (names.Count >= reader.TypeDefinitions.Count)
            {
                throw new BadImageFormatException("Cyclic declaring type metadata.");
            }

            var definition = reader.GetTypeDefinition(handle);
            names.Add(reader.GetString(definition.Name));
            nameSpace = reader.GetString(definition.Namespace);
            handle = definition.GetDeclaringType();
        }

        return Identity(reader, token, nameSpace, names, DefinitionScope(reader));
    }

    private static NamedTypeIdentity ReferenceIdentity(MetadataReader reader, TypeReferenceHandle handle)
    {
        int token = MetadataTokens.GetToken(handle);
        var names = new List<string>();
        string nameSpace;
        EntityHandle scope;
        while (true)
        {
            if (names.Count >= reader.TypeReferences.Count)
            {
                throw new BadImageFormatException("Cyclic type reference metadata.");
            }

            var reference = reader.GetTypeReference(handle);
            names.Add(reader.GetString(reference.Name));
            nameSpace = reader.GetString(reference.Namespace);
            scope = reference.ResolutionScope;
            if (scope.Kind != HandleKind.TypeReference)
            {
                break;
            }

            handle = (TypeReferenceHandle)scope;
        }

        string scopeName = scope.Kind switch
        {
            HandleKind.AssemblyReference => reader.GetAssemblyReference((AssemblyReferenceHandle)scope).GetAssemblyName().FullName!,
            HandleKind.ModuleReference => "module:" + reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)scope).Name),
            HandleKind.ModuleDefinition => DefinitionScope(reader),
            _ => "unknown:" + scope.Kind
        };
        return Identity(reader, token, nameSpace, names, scopeName);
    }

    private static string DefinitionScope(MetadataReader reader)
    {
        return reader.IsAssembly
            ? reader.GetAssemblyDefinition().GetAssemblyName().FullName!
            : "module:" + reader.GetString(reader.GetModuleDefinition().Name);
    }

    private static NamedTypeIdentity Identity(MetadataReader reader, int token, string nameSpace,
        List<string> names, string scope)
    {
        names.Reverse();
        string name = string.Join("+", names);
        string fullName = nameSpace.Length == 0 ? name : nameSpace + "." + name;
        var module = reader.GetModuleDefinition();
        return new NamedTypeIdentity(fullName, scope, reader.GetGuid(module.Mvid), token);
    }
}
