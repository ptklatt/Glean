using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography;

namespace Glean.Resolution;

/// <summary>
/// Canonical assembly identity key (name, version, culture, public key token).
/// </summary>
internal readonly struct AssemblyIdentityKey : IEquatable<AssemblyIdentityKey>
{
    private static readonly Version EmptyVersion = new(0, 0, 0, 0);

    // Public key tokens are 8 bytes. Full public keys are hashed with SHA1.
    private const int PublicKeyTokenSizeBytes = 8;
    private const int Sha1HashSizeBytes = 20;

    public AssemblyIdentityKey(string name, Version version, string culture, bool hasPublicKeyToken, ulong publicKeyToken)
    {
        Name = name;
        Version = version;
        Culture = culture;
        HasPublicKeyToken = hasPublicKeyToken;
        PublicKeyToken = publicKeyToken;
    }

    public string Name { get; }
    public Version Version { get; }
    public string Culture { get; }
    public bool HasPublicKeyToken { get; }
    public ulong PublicKeyToken { get; }

    public static AssemblyIdentityKey FromReference(AssemblyReference reference, MetadataReader reader)
    {
        var name = reader.GetString(reference.Name);
        var version = reference.Version ?? EmptyVersion;
        var culture = NormalizeCulture(reference.Culture, reader);

        var flags = (AssemblyFlags)reference.Flags;
        var token = ReadPublicKeyOrToken(reference.PublicKeyOrToken, flags, reader, out var hasToken);

        return new AssemblyIdentityKey(name, version, culture, hasToken, token);
    }

    public static AssemblyIdentityKey FromDefinition(AssemblyDefinition definition, MetadataReader reader)
    {
        var name = reader.GetString(definition.Name);
        var version = definition.Version ?? EmptyVersion;
        var culture = NormalizeCulture(definition.Culture, reader);

        var flags = (AssemblyFlags)definition.Flags;
        var token = ReadPublicKeyOrToken(definition.PublicKey, flags, reader, out var hasToken);

        return new AssemblyIdentityKey(name, version, culture, hasToken, token);
    }

    public bool Equals(AssemblyIdentityKey other)
    {
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
               Equals(Version, other.Version) &&
               string.Equals(Culture, other.Culture, StringComparison.OrdinalIgnoreCase) &&
               HasPublicKeyToken == other.HasPublicKeyToken &&
               (!HasPublicKeyToken || PublicKeyToken == other.PublicKeyToken);
    }

    public override bool Equals(object? obj)
    {
        return (obj is AssemblyIdentityKey other) && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name, StringComparer.OrdinalIgnoreCase);
        hash.Add(Version);
        hash.Add(Culture, StringComparer.OrdinalIgnoreCase);
        hash.Add(HasPublicKeyToken);
        if (HasPublicKeyToken)
        {
            hash.Add(PublicKeyToken);
        }
        return hash.ToHashCode();
    }

    public bool MatchesLoosely(AssemblyIdentityKey other)
    {
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
               HasPublicKeyToken == other.HasPublicKeyToken &&
               (!HasPublicKeyToken || (PublicKeyToken == other.PublicKeyToken));
    }

    private static string NormalizeCulture(StringHandle cultureHandle, MetadataReader reader)
    {
        if (cultureHandle.IsNil) { return string.Empty; }

        var culture = reader.GetString(cultureHandle);
        if (string.IsNullOrEmpty(culture) || string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return culture;
    }

    private static ulong ReadPublicKeyOrToken(
        BlobHandle blobHandle,
        AssemblyFlags flags,
        MetadataReader reader,
        out bool hasToken)
    {
        if (blobHandle.IsNil)
        {
            hasToken = false;
            return 0;
        }

        var blobReader = reader.GetBlobReader(blobHandle);
        if (blobReader.Length == 0)
        {
            hasToken = false;
            return 0;
        }

        unsafe
        {
            var bytes = new ReadOnlySpan<byte>(blobReader.StartPointer, blobReader.Length);
            bool isFullPublicKey = ((flags & AssemblyFlags.PublicKey) != 0) || (bytes.Length > PublicKeyTokenSizeBytes);

            // Token path (exact 8 bytes, not full key).
            if (!isFullPublicKey)
            {
                if (bytes.Length != PublicKeyTokenSizeBytes)
                {
                    hasToken = false;
                    return 0;
                }

                hasToken = true;
                return ToUInt64(bytes);
            }

            // Full public key path: compute token without allocating.
            if (bytes.Length == 0)
            {
                hasToken = false;
                return 0;
            }

            Span<byte> hash = stackalloc byte[Sha1HashSizeBytes];
            SHA1.HashData(bytes, hash);

            // Public key token is last 8 bytes of SHA1(publicKey), reversed.
            ulong token = 0;
            for (int i = 0; i < PublicKeyTokenSizeBytes; i++)
            {
                token = (token << 8) | hash[hash.Length - 1 - i];
            }

            hasToken = true;
            return token;
        }
    }

    private static ulong ToUInt64(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < PublicKeyTokenSizeBytes) { return 0; }

        ulong value = 0;
        for (int i = 0; i < PublicKeyTokenSizeBytes; i++)
        {
            value = (value << 8) | bytes[i];
        }
        return value;
    }
}
