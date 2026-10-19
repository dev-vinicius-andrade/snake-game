using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class SecretHasherTests
{
    private const string Secret = "amber-otter-nine-cable";
    private static readonly string Salt = Convert.ToBase64String(new byte[16]);
    private static readonly string Hash = Convert.ToBase64String(new byte[32]);
    private readonly SecretHasher _hasher = new(iterations: 1000);

    [Fact]
    public void Hash_DescribesItsParameters()
    {
        var hashed = _hasher.Hash(Secret);

        var fields = hashed.Split('$');
        Assert.Equal(4, fields.Length);
        Assert.Equal("pbkdf2-sha256", fields[0]);
        Assert.Equal("1000", fields[1]);
        Assert.Equal(16, Convert.FromBase64String(fields[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(fields[3]).Length);
    }

    [Fact]
    public void Hash_ByDefault_Uses210000Iterations()
    {
        var hashed = new SecretHasher().Hash(Secret);

        Assert.StartsWith("pbkdf2-sha256$210000$", hashed);
    }

    [Fact]
    public void Hash_SameSecretTwice_UsesAFreshSalt()
    {
        Assert.NotEqual(_hasher.Hash(Secret), _hasher.Hash(Secret));
    }

    [Fact]
    public void Verify_TheSameSecret_IsTrue()
    {
        var hashed = _hasher.Hash(Secret);

        Assert.True(_hasher.Verify(Secret, hashed));
    }

    [Theory]
    [InlineData("amber-otter-nine-cabl")]
    [InlineData("Amber-otter-nine-cable")]
    [InlineData("")]
    [InlineData(null)]
    public void Verify_AnotherSecret_IsFalse(string? other)
    {
        var hashed = _hasher.Hash(Secret);

        Assert.False(_hasher.Verify(other, hashed));
    }

    [Fact]
    public void Verify_AHashMadeWithOtherIterations_UsesTheIterationsOfTheHash()
    {
        var hashed = new SecretHasher(iterations: 500).Hash(Secret);

        Assert.True(_hasher.Verify(Secret, hashed));
    }

    [Fact]
    public void Verify_BrokenUnicode_DoesNotThrow()
    {
        var hashed = _hasher.Hash("amber\uD800otter");

        Assert.True(_hasher.Verify("amber\uD800otter", hashed));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Verify_TamperedField_IsFalse(int field)
    {
        var fields = _hasher.Hash(Secret).Split('$');
        fields[field] = field == 1 ? "1001" : Tampered(fields[field]);

        Assert.False(_hasher.Verify(Secret, string.Join('$', fields)));
    }

    public static TheoryData<string?> MalformedHashes => new()
    {
        null,
        "",
        "garbage",
        "$$$",
        $"pbkdf2-sha256$1000${Salt}",
        $"pbkdf2-sha256$1000${Salt}${Hash}$extra",
        $"pbkdf2-sha1$1000${Salt}${Hash}",
        $"PBKDF2-SHA256$1000${Salt}${Hash}",
        $"pbkdf2-sha256$${Salt}${Hash}",
        $"pbkdf2-sha256$abc${Salt}${Hash}",
        $"pbkdf2-sha256$0${Salt}${Hash}",
        $"pbkdf2-sha256$-5${Salt}${Hash}",
        $"pbkdf2-sha256$+5${Salt}${Hash}",
        $"pbkdf2-sha256$ 1000${Salt}${Hash}",
        $"pbkdf2-sha256$99999999999${Salt}${Hash}",
        $"pbkdf2-sha256$2147483647${Salt}${Hash}",
        $"pbkdf2-sha256$1000$!!notbase64!!${Hash}",
        $"pbkdf2-sha256$1000${Salt}$!!notbase64!!",
        $"pbkdf2-sha256$1000${Convert.ToBase64String(new byte[15])}${Hash}",
        $"pbkdf2-sha256$1000${Convert.ToBase64String(new byte[17])}${Hash}",
        $"pbkdf2-sha256$1000${Salt}${Convert.ToBase64String(new byte[31])}",
        $"pbkdf2-sha256$1000${Salt}${Convert.ToBase64String(new byte[64])}",
        $"pbkdf2-sha256$1000$${Hash}",
    };

    [Theory]
    [MemberData(nameof(MalformedHashes))]
    public void Verify_MalformedHash_IsFalse(string? malformed)
    {
        Assert.False(_hasher.Verify(Secret, malformed));
    }

    [Fact]
    public void NeedsRehash_FewerIterationsThanConfigured_IsTrue()
    {
        var hashed = new SecretHasher(iterations: 500).Hash(Secret);

        Assert.True(_hasher.NeedsRehash(hashed));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(2000)]
    public void NeedsRehash_AsManyIterationsOrMore_IsFalse(int iterations)
    {
        var hashed = new SecretHasher(iterations).Hash(Secret);

        Assert.False(_hasher.NeedsRehash(hashed));
    }

    [Theory]
    [MemberData(nameof(MalformedHashes))]
    public void NeedsRehash_MalformedHash_IsTrue(string? malformed)
    {
        Assert.True(_hasher.NeedsRehash(malformed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(SecretHasher.MaxIterations + 1)]
    public void Constructor_IterationsOutOfRange_Throws(int iterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SecretHasher(iterations));
    }

    private static string Tampered(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        bytes[0] ^= 0x01;
        return Convert.ToBase64String(bytes);
    }
}
