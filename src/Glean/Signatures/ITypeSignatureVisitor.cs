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
    /// <summary>Visits a <see cref="ArraySignature"/> node with the caller supplied state.</summary>
    void VisitArray(ArraySignature signature, TState state);

    /// <summary>Visits a <see cref="ByRefSignature"/> node with the caller supplied state.</summary>
    void VisitByRef(ByRefSignature signature, TState state);

    /// <summary>Visits a <see cref="FunctionPointerSignature"/> node with the caller supplied state.</summary>
    void VisitFunctionPointer(FunctionPointerSignature signature, TState state);

    /// <summary>Visits a <see cref="GenericInstanceSignature"/> node with the caller supplied state.</summary>
    void VisitGenericInstance(GenericInstanceSignature signature, TState state);

    /// <summary>Visits a <see cref="GenericMethodParameterSignature"/> node with the caller supplied state.</summary>
    void VisitGenericMethodParameter(GenericMethodParameterSignature signature, TState state);

    /// <summary>Visits a <see cref="GenericTypeParameterSignature"/> node with the caller supplied state.</summary>
    void VisitGenericTypeParameter(GenericTypeParameterSignature signature, TState state);

    /// <summary>Visits a <see cref="ModifiedTypeSignature"/> node with the caller supplied state.</summary>
    void VisitModifiedType(ModifiedTypeSignature signature, TState state);

    /// <summary>Visits a <see cref="PinnedTypeSignature"/> node with the caller supplied state.</summary>
    void VisitPinnedType(PinnedTypeSignature signature, TState state);

    /// <summary>Visits a <see cref="PointerSignature"/> node with the caller supplied state.</summary>
    void VisitPointer(PointerSignature signature, TState state);

    /// <summary>Visits a <see cref="PrimitiveTypeSignature"/> node with the caller supplied state.</summary>
    void VisitPrimitive(PrimitiveTypeSignature signature, TState state);

    /// <summary>Visits a <see cref="SerializedTypeNameSignature"/> node with the caller supplied state.</summary>
    void VisitSerializedTypeName(SerializedTypeNameSignature signature, TState state);

    /// <summary>Visits a <see cref="SentinelTypeSignature"/> node with the caller supplied state.</summary>
    void VisitSentinel(SentinelTypeSignature signature, TState state);

    /// <summary>Visits a <see cref="SZArraySignature"/> node with the caller supplied state.</summary>
    void VisitSZArray(SZArraySignature signature, TState state);

    /// <summary>Visits a <see cref="TypeDefinitionSignature"/> node with the caller supplied state.</summary>
    void VisitTypeDefinition(TypeDefinitionSignature signature, TState state);

    /// <summary>Visits a <see cref="TypeReferenceSignature"/> node with the caller supplied state.</summary>
    void VisitTypeReference(TypeReferenceSignature signature, TState state);
}
