using System.Diagnostics.CodeAnalysis;

namespace Application.Manager.Api.Accounts;

/// <summary>The error codes of the accounts api (docs/ACCOUNTS.md), as they appear in <c>{ "error": "&lt;code&gt;" }</c>.</summary>
public static class AccountErrors
{
    public const string InvalidUsername = UsernameRules.InvalidUsername;
    public const string InvalidDisplayName = "invalidDisplayName";
    public const string InvalidTag = "invalidTag";
    public const string WeakPhrase = RecoveryPhrase.WeakPhrase;
    public const string NoTagsLeft = "noTagsLeft";
    /// <summary>The username and tag asked for belong to another account.</summary>
    public const string NameTaken = "nameTaken";
    public const string InvalidCredentials = "invalidCredentials";
    public const string InvalidToken = "invalidToken";
    public const string Throttled = "throttled";
    public const string UnknownProvider = "unknownProvider";
    public const string Cancelled = "cancelled";
    /// <summary>The social account is already linked to another player's account.</summary>
    public const string AlreadyLinked = "alreadyLinked";
    /// <summary>The account already has another account of that provider linked.</summary>
    public const string ProviderInUse = "providerInUse";
    public const string InvalidState = "invalidState";
    public const string ProviderFailed = "providerFailed";
    public const string InvalidTicket = "invalidTicket";
}

/// <summary>What an <see cref="AccountService"/> call gave: a value, or an error code (and, when throttled, how long to wait).</summary>
public sealed record AccountResult<T> where T : class
{
    private AccountResult(T? value, string? error, int? retryAfterSeconds)
    {
        Value = value;
        Error = error;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>The value; null on failure.</summary>
    public T? Value { get; }

    /// <summary>One of <see cref="AccountErrors"/>; null on success.</summary>
    public string? Error { get; }

    /// <summary>Whole seconds to wait, rounded up, at least 1; only when <see cref="Error"/> is <see cref="AccountErrors.Throttled"/>.</summary>
    public int? RetryAfterSeconds { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    public static AccountResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, null, null);
    }

    public static AccountResult<T> Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(null, error, null);
    }

    public static AccountResult<T> Throttled(TimeSpan retryAfter)
        => new(null, AccountErrors.Throttled, Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)));
}

/// <summary>A new account, the token of its first device, and the recovery phrase to show the player once.</summary>
public sealed record Registration(Account Account, string Token, string RecoveryPhrase);

/// <summary>An account signed in on a new device, and that device's token.</summary>
public sealed record SignIn(Account Account, string Token);

/// <summary>A username as it would be kept, and whether it can be registered (with the tag asked about, or with any tag).</summary>
public sealed record UsernameAvailability(string Username, bool Available);
