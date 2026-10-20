using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

public sealed class UsernameSanitizerTests
{
    [Theory]
    [InlineData("octocat", "octocat")]
    [InlineData("  Mona   Lisa ", "Mona Lisa")]
    [InlineData("João da Silva", "João da Silva")]
    [InlineData("name@example.com", "nameexample.com")]
    [InlineData("Cool 😎 Snake", "Cool Snake")]
    [InlineData("a#12345", "a12345")]
    public void Clean_KeepsWhatAUsernameMayHold_AndDropsTheRest(string raw, string expected)
        => Assert.Equal(expected, UsernameSanitizer.Clean(raw));

    [Fact]
    public void Clean_CutsALongNameToTheLimit_WithoutBreakingACharacter()
    {
        var cleaned = UsernameSanitizer.Clean(new string('a', 19) + "ééé");

        Assert.NotNull(cleaned);
        Assert.Equal(UsernameRules.MaxLength, cleaned!.Length);
        Assert.True(UsernameRules.Validate(cleaned).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("😎😎")]
    [InlineData("x")]
    [InlineData("admin")]
    public void Clean_GivesNothingWhenNothingUsableIsLeft(string? raw)
        => Assert.Null(UsernameSanitizer.Clean(raw));

    [Fact]
    public void Clean_AlwaysGivesAValidUsernameOrNothing()
    {
        foreach (var raw in new[] { "Vini#00001", "Ana_Maria-2", "J.R.R. Tolkien", "ｆｕｌｌｗｉｄｔｈ", "line\nbreak" })
        {
            var cleaned = UsernameSanitizer.Clean(raw);
            if (cleaned is not null)
                Assert.True(UsernameRules.Validate(cleaned).IsValid, cleaned);
        }
    }
}
