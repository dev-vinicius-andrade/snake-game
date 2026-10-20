using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class LoginIdentifierTests
{
    [Fact]
    public void TheSameProviderIdAndSecretGiveTheSameHash()
        => Assert.Equal(LoginIdentifier.Hash("github", "583231", "pepper"), LoginIdentifier.Hash("github", "583231", "pepper"));

    [Fact]
    public void TheProviderCaseAndSurroundingSpacesDoNotChangeIt()
        => Assert.Equal(LoginIdentifier.Hash("github", "583231", "pepper"), LoginIdentifier.Hash(" GitHub ", " 583231 ", "pepper"));

    [Fact]
    public void AnotherIdAnotherProviderOrAnotherSecretGiveAnotherHash()
    {
        var hash = LoginIdentifier.Hash("github", "583231", "pepper");

        Assert.NotEqual(hash, LoginIdentifier.Hash("github", "583232", "pepper"));
        Assert.NotEqual(hash, LoginIdentifier.Hash("discord", "583231", "pepper"));
        Assert.NotEqual(hash, LoginIdentifier.Hash("github", "583231", "another pepper"));
    }

    [Fact]
    public void ItIs64LowercaseHexCharacters_AndShowsNothingOfTheId()
    {
        var hash = LoginIdentifier.Hash("github", "583231", "pepper");

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.DoesNotContain("583231", hash);
    }

    [Fact]
    public void ItIsNotThePlainHashOfTheId_SoItCannotBeFoundByTryingEveryId()
    {
        var plain = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("github:583231"))).ToLowerInvariant();

        Assert.NotEqual(plain, LoginIdentifier.Hash("github", "583231", "pepper"));
    }

    [Theory]
    [InlineData(null, "1", "p")]
    [InlineData("github", null, "p")]
    [InlineData("github", "1", null)]
    [InlineData("github", "1", "")]
    public void WithoutAProviderAnIdAndASecret_ItRefuses(string? provider, string? subject, string? pepper)
        => Assert.ThrowsAny<ArgumentException>(() => LoginIdentifier.Hash(provider!, subject!, pepper!));
}
