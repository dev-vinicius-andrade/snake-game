using System.Text.Json;
using Application.Manager.Api.Accounts.OAuth;

namespace Application.Manager.Api.Tests;

public sealed class OAuthProvidersTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    [Fact]
    public void GitHub_UsesTheLoginAsUsernameAndTheNameAsDisplayName_NeverTheEmail()
    {
        var profile = OAuthProviders.Get("github")!.ReadProfile(Json("""{ "id": 583231, "login": "octocat", "name": "The Octocat", "email": "octo@example.com" }"""));

        Assert.Equal(new OAuthProfile("583231", "octocat", "The Octocat"), profile);
    }

    [Fact]
    public void GitHub_ANameLessAccountShowsTheLogin()
    {
        var profile = OAuthProviders.Get("github")!.ReadProfile(Json("""{ "id": 1, "login": "octocat", "name": null }"""));

        Assert.Equal(new OAuthProfile("1", "octocat", "octocat"), profile);
    }

    [Fact]
    public void Discord_UsesTheUsernameAndTheGlobalName()
    {
        var profile = OAuthProviders.Get("discord")!.ReadProfile(Json("""{ "id": "80351110224678912", "username": "nelly", "global_name": "Nelly", "email": "n@example.com" }"""));

        Assert.Equal(new OAuthProfile("80351110224678912", "nelly", "Nelly"), profile);
    }

    [Fact]
    public void Google_HasNoUsername_SoTheGivenNameIsUsed()
    {
        var profile = OAuthProviders.Get("google")!.ReadProfile(Json("""{ "sub": "110169484474386276334", "name": "Ana Maria Souza", "given_name": "Ana", "email": "a@example.com" }"""));

        Assert.Equal(new OAuthProfile("110169484474386276334", "Ana", "Ana Maria Souza"), profile);
    }

    [Theory]
    [InlineData("github", """{ "login": "x" }""")]
    [InlineData("discord", """{ "username": "x" }""")]
    [InlineData("google", """{ "name": "x" }""")]
    [InlineData("github", """{ "id": null }""")]
    [InlineData("github", """[1,2]""")]
    public void AnAnswerWithoutAnId_IsNoProfile(string provider, string json)
        => Assert.Null(OAuthProviders.Get(provider)!.ReadProfile(Json(json)));

    [Fact]
    public void TheIdIsKeptAsText_WhateverItsType()
    {
        Assert.Equal("42", OAuthProviders.Get("github")!.ReadProfile(Json("""{ "id": 42, "login": "x" }"""))!.Subject);
        Assert.Equal("42", OAuthProviders.Get("discord")!.ReadProfile(Json("""{ "id": "42", "username": "x" }"""))!.Subject);
    }

    [Fact]
    public void KnowsItsProviders_AnyCase_AndNothingElse()
    {
        Assert.NotNull(OAuthProviders.Get("GitHub"));
        Assert.Null(OAuthProviders.Get("myspace"));
        Assert.Equal(new[] { "github", "discord", "google", "steam" }, OAuthProviders.All.Select(provider => provider.Id));
    }

    [Fact]
    public void NoProviderAsksForTheEmail()
    {
        foreach (var provider in OAuthProviders.All)
            Assert.DoesNotContain("email", provider.Scope, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Steam_IsSignedInWithOpenId_NotOAuth_AndTheOthersWithOAuth()
    {
        Assert.Equal(OAuthKind.OpenId, OAuthProviders.Get("steam")!.Kind);
        Assert.All(new[] { "github", "discord", "google" }, id => Assert.Equal(OAuthKind.OAuth, OAuthProviders.Get(id)!.Kind));
    }

    [Theory]
    [InlineData("https://steamcommunity.com/openid/id/76561198000000001", "76561198000000001")]
    [InlineData("http://steamcommunity.com/openid/id/76561198000000001", "76561198000000001")]
    public void SteamId_IsReadFromTheClaimedIdentity(string claimed, string expected)
        => Assert.Equal(expected, SteamOpenId.ReadSteamId(claimed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example.com/openid/id/76561198000000001")]
    [InlineData("https://steamcommunity.com.evil.example.com/openid/id/76561198000000001")]
    [InlineData("https://steamcommunity.com/openid/id/123")]
    [InlineData("https://steamcommunity.com/openid/id/76561198000000001/extra")]
    [InlineData("https://steamcommunity.com/openid/id/7656119800000000a")]
    public void SteamId_IsNothingForAnythingElse(string? claimed)
        => Assert.Null(SteamOpenId.ReadSteamId(claimed));

    [Fact]
    public void SteamOpenId_StartsWithTheIdentifierSelectRequest_AndTheStateInTheReturnAddress()
    {
        var url = SteamOpenId.StartUrl(SteamOpenId.Endpoint, "https://snake.example.com/api/accounts/oauth/steam/callback", "https://snake.example.com/", "the-state");

        Assert.StartsWith("https://steamcommunity.com/openid/login?", url);
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.Equal("http://specs.openid.net/auth/2.0", query["openid.ns"]);
        Assert.Equal("checkid_setup", query["openid.mode"]);
        Assert.Equal("https://snake.example.com/api/accounts/oauth/steam/callback?state=the-state", query["openid.return_to"]);
        Assert.Equal("https://snake.example.com/", query["openid.realm"]);
        Assert.Equal("http://specs.openid.net/auth/2.0/identifier_select", query["openid.identity"]);
        Assert.Equal("http://specs.openid.net/auth/2.0/identifier_select", query["openid.claimed_id"]);
    }
}
