using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Extensions;

namespace Glean.Benchmarks;

public enum ConstantWorkload { Values, Missing, TypeMismatch }

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.Constants")]
public class ConstantBenchmarks : IDisposable
{
    private MetadataReaderProvider _provider = null!;
    private MetadataReader _reader = null!;
    private Parameter[] _parameters = null!;

    [ParamsAllValues]
    public ConstantWorkload Workload { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("constants"), default, default, default);
        object[] values = { true, 'A', (sbyte)-1, (byte)2, (short)-3, (ushort)4, -5, 6U, -7L, 8UL, 1.5f, 2.5 };
        var handles = new ParameterHandle[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var attributes = ParameterAttributes.Optional;
            if (Workload != ConstantWorkload.Missing)
            {
                attributes |= ParameterAttributes.HasDefault;
            }
            handles[i] = metadata.AddParameter(attributes, metadata.GetOrAddString("p" + i), i + 1);
            if (Workload != ConstantWorkload.Missing)
            {
                metadata.AddConstant(handles[i], values[i]);
            }
        }
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        _provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
        _reader = _provider.GetMetadataReader();
        _parameters = handles.Select(_reader.GetParameter).ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            object? actual = _parameters[i].GetDefaultValue(_reader);
            MetadataFixture.Require(Workload == ConstantWorkload.Missing ? actual == null : values[i].Equals(actual), "Constant value");
            bool success = _parameters[i].TryGetDefaultValue<int>(_reader, out int integer);
            bool expected = (Workload != ConstantWorkload.Missing) && (i == 6);
            MetadataFixture.Require((success == expected) && (integer == (expected ? -5 : 0)), "Constant mismatch result/out value");
        }
        MetadataFixture.Require(Srm() == Glean(), "Primitive constant checksum");
    }

    [Benchmark(Baseline = true)]
    public long Srm()
    {
        long sum = 0;
        if (Workload == ConstantWorkload.TypeMismatch)
        {
            for (int i = 0; i < _parameters.Length; i++)
            {
                sum += Read(i, ConstantTypeCode.Int32, out var blob) ? blob.ReadInt32() : 1;
            }
            return sum;
        }
        sum += Read(0, ConstantTypeCode.Boolean, out var value) ? (value.ReadBoolean() ? 1 : 0) : 1;
        sum += Read(1, ConstantTypeCode.Char, out value) ? value.ReadChar() : 1;
        sum += Read(2, ConstantTypeCode.SByte, out value) ? value.ReadSByte() : 1;
        sum += Read(3, ConstantTypeCode.Byte, out value) ? value.ReadByte() : 1;
        sum += Read(4, ConstantTypeCode.Int16, out value) ? value.ReadInt16() : 1;
        sum += Read(5, ConstantTypeCode.UInt16, out value) ? value.ReadUInt16() : 1;
        sum += Read(6, ConstantTypeCode.Int32, out value) ? value.ReadInt32() : 1;
        sum += Read(7, ConstantTypeCode.UInt32, out value) ? value.ReadUInt32() : 1;
        sum += Read(8, ConstantTypeCode.Int64, out value) ? value.ReadInt64() : 1;
        sum += Read(9, ConstantTypeCode.UInt64, out value) ? (long)value.ReadUInt64() : 1;
        sum += Read(10, ConstantTypeCode.Single, out value) ? BitConverter.SingleToInt32Bits(value.ReadSingle()) : 1;
        sum += Read(11, ConstantTypeCode.Double, out value) ? BitConverter.DoubleToInt64Bits(value.ReadDouble()) : 1;
        return sum;
    }

    [Benchmark]
    public long Glean()
    {
        long sum = 0;
        if (Workload == ConstantWorkload.TypeMismatch)
        {
            foreach (var parameter in _parameters)
            {
                sum += parameter.TryGetDefaultValue<int>(_reader, out int integer) ? integer : 1;
            }
            return sum;
        }
        sum += _parameters[0].TryGetDefaultValue<bool>(_reader, out var boolean) ? (boolean ? 1 : 0) : 1;
        sum += _parameters[1].TryGetDefaultValue<char>(_reader, out var character) ? character : 1;
        sum += _parameters[2].TryGetDefaultValue<sbyte>(_reader, out var signedByte) ? signedByte : 1;
        sum += _parameters[3].TryGetDefaultValue<byte>(_reader, out var unsignedByte) ? unsignedByte : 1;
        sum += _parameters[4].TryGetDefaultValue<short>(_reader, out var signedShort) ? signedShort : 1;
        sum += _parameters[5].TryGetDefaultValue<ushort>(_reader, out var unsignedShort) ? unsignedShort : 1;
        sum += _parameters[6].TryGetDefaultValue<int>(_reader, out var signedInt) ? signedInt : 1;
        sum += _parameters[7].TryGetDefaultValue<uint>(_reader, out var unsignedInt) ? unsignedInt : 1;
        sum += _parameters[8].TryGetDefaultValue<long>(_reader, out var signedLong) ? signedLong : 1;
        sum += _parameters[9].TryGetDefaultValue<ulong>(_reader, out var unsignedLong) ? (long)unsignedLong : 1;
        sum += _parameters[10].TryGetDefaultValue<float>(_reader, out var single) ? BitConverter.SingleToInt32Bits(single) : 1;
        sum += _parameters[11].TryGetDefaultValue<double>(_reader, out var number) ? BitConverter.DoubleToInt64Bits(number) : 1;
        return sum;
    }

    private bool Read(int index, ConstantTypeCode expected, out BlobReader blob)
    {
        blob = default;
        var parameter = _parameters[index];
        if ((parameter.Attributes & ParameterAttributes.HasDefault) == 0)
        {
            return false;
        }
        var handle = parameter.GetDefaultValue();
        if (handle.IsNil)
        {
            return false;
        }
        var constant = _reader.GetConstant(handle);
        blob = _reader.GetBlobReader(constant.Value);
        return constant.TypeCode == expected;
    }

    public void Dispose() => _provider.Dispose();
}
