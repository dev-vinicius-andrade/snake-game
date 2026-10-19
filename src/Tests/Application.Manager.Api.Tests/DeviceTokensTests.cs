using System.Text.RegularExpressions;
using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class DeviceTokensTests
{
    [Fact]
    public void Create_TokenIsBase64UrlWithoutPadding()
    {
        var (token, _) = DeviceTokens.Create();

        Assert.Matches(new Regex("^[A-Za-z0-9_-]{43}$"), token);
    }

    [Fact]
    public void Create_TokenCarries32RandomBytes()
    {
        var (token, _) = DeviceTokens.Create();

        var base64 = token.Replace('-', '+').Replace('_', '/') + "=";
        Assert.Equal(32, Convert.FromBase64String(base64).Length);
    }

    [Fact]
    public void Create_HashIsTheHashOfTheToken()
    {
        var (token, hash) = DeviceTokens.Create();

        Assert.Equal(DeviceTokens.Hash(token), hash);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), hash);
    }

    [Fact]
    public void Create_ManyTimes_GivesUniqueTokensAndHashes()
    {
        var created = Enumerable.Range(0, 1000).Select(_ => DeviceTokens.Create()).ToList();

        Assert.Equal(1000, created.Select(pair => pair.Token).Distinct().Count());
        Assert.Equal(1000, created.Select(pair => pair.Hash).Distinct().Count());
    }

    [Fact]
    public void Hash_IsLowercaseHexSha256OfTheTokenText()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DeviceTokens.Hash("abc"));
    }

    [Fact]
    public void LooksLikeToken_ACreatedToken_IsTrue()
    {
        var (token, _) = DeviceTokens.Create();

        Assert.True(DeviceTokens.LooksLikeToken(token));
    }

    [Fact]
    public void LooksLikeToken_AnyBase64UrlTextOfTheRightLength_IsTrue()
    {
        Assert.True(DeviceTokens.LooksLikeToken(new string('A', 40) + "-_9"));
    }

    public static TheoryData<string?> Garbage => new()
    {
        null,
        "",
        "short",
        new string('A', 42),
        new string('A', 44),
        new string('A', 42) + "=",
        new string('A', 42) + "+",
        new string('A', 42) + "/",
        new string('A', 42) + " ",
        new string('A', 42) + "é",
        "Bearer " + new string('A', 36),
        new string('A', 4096),
    };

    [Theory]
    [MemberData(nameof(Garbage))]
    public void LooksLikeToken_Garbage_IsFalse(string? candidate)
    {
        Assert.False(DeviceTokens.LooksLikeToken(candidate));
    }
}
