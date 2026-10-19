using Application.Manager.Api.Gameplay;

namespace Application.Manager.Api.Tests;

public sealed class PerkScriptCheckerTests
{
    [Fact]
    public void Check_AGoodScriptIsOk()
    {
        var result = PerkScriptChecker.Check("function modify(snake) { return { extraShots: 1 }; }");

        Assert.True(result.Ok);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Check_ASyntaxErrorSaysWhy()
    {
        var result = PerkScriptChecker.Check("function modify( {");

        Assert.False(result.Ok);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Check_AScriptThatFailsWhenRunIsNotOk()
    {
        var result = PerkScriptChecker.Check("function modify(snake) { return snake.nothing.deeper; }");

        Assert.False(result.Ok);
        Assert.Contains("modify", result.Error);
    }

    [Fact]
    public void Check_ALoopThatNeverEndsIsNotOk()
    {
        Assert.False(PerkScriptChecker.Check("function modify() { while (true) {} }").Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Check_NothingIsNotOk(string? script)
    {
        Assert.False(PerkScriptChecker.Check(script).Ok);
    }
}
