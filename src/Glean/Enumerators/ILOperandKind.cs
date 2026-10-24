namespace Glean.Enumerators;

/// <summary>
/// Classifies the operand shape in the experimental IL decoder.
/// </summary>
internal enum ILOperandKind : byte
{
    None = 0,
    MetadataToken = 1,
    BranchTarget = 2,
    Int32 = 3,
    Int64 = 4,
    Float32 = 5,
    Float64 = 6,
    LocalOrArg = 7,
    SwitchCount = 8,
}
