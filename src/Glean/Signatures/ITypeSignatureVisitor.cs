namespace Glean.Signatures;

/// <summary>
/// Visits decoded <see cref="TypeSignature"/> nodes.
/// </summary>
/// <typeparam name="TState">An optional caller supplied state value.</typeparam>
/// <remarks>
/// Use a local visitor variable and pass it to <see cref="TypeSignature.Accept{TVisitor, TState}(ref TVisitor, TState)"/>
/// by reference. Struct visitors stay box free in that shape.
/// </remarks>
public interface ITypeSignatureVisitor<in TState>
{
    void VisitArray(ArraySignature signature, TState state);

    void VisitByRef(ByRefSignature signature, TState state);

    void VisitFunctionPointer(FunctionPointerSignature signature, TState state);

    void VisitGenericInstance(GenericInstanceSignature signature, TState state);

    void VisitGenericMethodParameter(GenericMethodParameterSignature signature, TState state);

    void VisitGenericTypeParameter(GenericTypeParameterSignature signature, TState state);

    void VisitModifiedType(ModifiedTypeSignature signature, TState state);

    void VisitPinnedType(PinnedTypeSignature signature, TState state);

    void VisitPointer(PointerSignature signature, TState state);

    void VisitPrimitive(PrimitiveTypeSignature signature, TState state);

    void VisitSerializedTypeName(SerializedTypeNameSignature signature, TState state);

    void VisitSentinel(SentinelTypeSignature signature, TState state);

    void VisitSZArray(SZArraySignature signature, TState state);

    void VisitTypeDefinition(TypeDefinitionSignature signature, TState state);

    void VisitTypeReference(TypeReferenceSignature signature, TState state);
}
