using System.Collections.Generic;
using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class ReferenceTableExtensionsTests
{
    private const string ReferenceTableSource = """
        using System;
        using System.Collections.Generic;

        namespace Glean.Tests.Extensions;

        internal sealed class ReferenceTableFixture : IDisposable
        {
            private readonly List<int> _items = new();

            internal void UseReferences()
            {
                Console.WriteLine(string.Empty);
                _items.Add(1);
            }

            public void Dispose()
            {
            }
        }
        """;

    // == Assembly References ==================================================

    [Fact]
    public void EnumerateAssemblyReferences_ReturnsSameSequenceAsRawTable()
    {
        using var metadata = TestUtility.BuildMetadata(ReferenceTableSource);
        var reader = metadata.Reader;

        var expected = new List<(string Name, Version Version)>();
        foreach (var handle in reader.AssemblyReferences)
        {
            var reference = reader.GetAssemblyReference(handle);
            expected.Add((reader.GetString(reference.Name), reference.Version));
        }

        var actual = new List<(string Name, Version Version)>();
        var enumerator = reader.EnumerateAssemblyReferences();
        while (enumerator.MoveNext())
        {
            var reference = enumerator.Current;
            actual.Add((reference.Name, reference.Version));
        }

        Assert.Equal(expected, actual);
    }

    // == Type References ======================================================

    [Fact]
    public void EnumerateTypeReferences_ReturnsSameSequenceAsRawTable()
    {
        using var metadata = TestUtility.BuildMetadata(ReferenceTableSource);
        var reader = metadata.Reader;

        var expected = new List<(string Namespace, string Name)>();
        foreach (var handle in reader.TypeReferences)
        {
            var reference = reader.GetTypeReference(handle);
            expected.Add((reader.GetString(reference.Namespace), reader.GetString(reference.Name)));
        }

        var actual = new List<(string Namespace, string Name)>();
        var enumerator = reader.EnumerateTypeReferences();
        while (enumerator.MoveNext())
        {
            var reference = enumerator.Current;
            actual.Add((reference.Namespace, reference.Name));
        }

        Assert.Equal(expected, actual);
    }

    // == Member References ====================================================

    [Fact]
    public void EnumerateMemberReferences_ReturnsSameSequenceAsRawTable()
    {
        using var metadata = TestUtility.BuildMetadata(ReferenceTableSource);
        var reader = metadata.Reader;

        var expected = new List<(string Name, HandleKind ParentKind, MemberReferenceKind Kind)>();
        foreach (var handle in reader.MemberReferences)
        {
            var reference = reader.GetMemberReference(handle);
            expected.Add((reader.GetString(reference.Name), reference.Parent.Kind, reference.GetKind()));
        }

        var actual = new List<(string Name, HandleKind ParentKind, MemberReferenceKind Kind)>();
        var enumerator = reader.EnumerateMemberReferences();
        while (enumerator.MoveNext())
        {
            var reference = enumerator.Current;
            actual.Add((reference.Name, reference.Parent.Kind, reference.Kind));
        }

        Assert.Equal(expected, actual);
    }
}
