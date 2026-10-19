using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Application.Manager.Api.Accounts;

/// <summary>Who asks to join a game: a signed-in account (<see cref="AccountId"/> set, the nickname is its display name) or a guest.</summary>
public sealed record JoinIdentity(Guid? AccountId, string Nickname);

/// <summary>A guest may not pose as an account: no nickname ending in '#' and digits (as the client's isGuestNicknameAllowed).</summary>
public static partial class GuestNicknames
{
    public const string InvalidNickname = "invalidNickname";

    public static bool IsAllowed(string? nickname) => !LooksLikeAnAccount().IsMatch((nickname ?? string.Empty).Trim());

    [GeneratedRegex("#[0-9]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex LooksLikeAnAccount();
}

public static class JoinIdentities
{
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// No Authorization header: a guest with its nickname (<see cref="GuestNicknames.InvalidNickname"/> when it looks like an account). A header
    /// that is not the bearer token of an account: <see cref="AccountErrors.InvalidToken"/>. Otherwise the account, with its display name.
    /// </summary>
    public static AccountResult<JoinIdentity> Resolve(AccountService accounts, string? authorization, string nickname)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        if (string.IsNullOrWhiteSpace(authorization))
            return Guest(nickname);
        var account = accounts.Authenticate(BearerToken(authorization));
        return account is null
            ? AccountResult<JoinIdentity>.Failure(AccountErrors.InvalidToken)
            : AccountResult<JoinIdentity>.Success(new JoinIdentity(account.Id, account.Display));
    }

    private static AccountResult<JoinIdentity> Guest(string nickname)
        => GuestNicknames.IsAllowed(nickname)
            ? AccountResult<JoinIdentity>.Success(new JoinIdentity(null, nickname))
            : AccountResult<JoinIdentity>.Failure(GuestNicknames.InvalidNickname);

    private static string? BearerToken(string authorization)
        => AuthenticationHeaderValue.TryParse(authorization, out var value) && string.Equals(value.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase)
            ? value.Parameter
            : null;
}
