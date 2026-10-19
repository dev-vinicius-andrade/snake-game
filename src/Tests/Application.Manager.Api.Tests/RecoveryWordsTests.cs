using System.Text.RegularExpressions;
using Application.Manager.Api.Accounts;
using Xunit;

namespace Application.Manager.Api.Tests;

public class RecoveryWordsTests
{
    private static readonly Regex WordPattern = new("^[a-z]{3,8}$", RegexOptions.CultureInvariant);

    [Fact]
    public void All_HasExactly2048Words()
    {
        Assert.Equal(2048, RecoveryWords.All.Count);
    }

    [Fact]
    public void All_ContainsOnlyDistinctWords()
    {
        Assert.Equal(RecoveryWords.All.Count, RecoveryWords.All.Distinct().Count());
    }

    [Fact]
    public void All_EveryWordMatchesPattern()
    {
        var invalid = RecoveryWords.All.Where(w => !WordPattern.IsMatch(w)).ToList();

        Assert.Empty(invalid);
    }
}
