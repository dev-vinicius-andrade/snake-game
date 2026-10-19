namespace Application.Manager.Api.Accounts;

/// <summary>
/// The throttles of the accounts api, each with its own tuning (docs/ACCOUNTS.md):
/// <see cref="SignIn"/> counts wrong phrases per account and per IP; <see cref="Registration"/> counts every valid registration per IP;
/// <see cref="Lookup"/> counts every availability check per IP.
/// </summary>
public sealed record AccountThrottles(AttemptThrottle SignIn, AttemptThrottle Registration, AttemptThrottle Lookup)
{
    /// <summary>5 free failures, then 2 s doubling up to 15 min; forgotten after 1 h.</summary>
    public static ThrottleOptions SignInOptions { get; } = new();

    /// <summary>10 free registrations, then 1 min doubling up to 15 min; forgotten after 1 h without one.</summary>
    public static ThrottleOptions RegistrationOptions { get; } = new() { FreeAttempts = 10, BaseDelay = TimeSpan.FromMinutes(1) };

    /// <summary>60 free checks, then 1 s doubling up to 1 min; forgotten after 10 min without one.</summary>
    public static ThrottleOptions LookupOptions { get; } = new()
    {
        FreeAttempts = 60,
        BaseDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromMinutes(1),
        ForgetAfter = TimeSpan.FromMinutes(10),
    };

    public static AccountThrottles Create(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        return new(new AttemptThrottle(time, SignInOptions), new AttemptThrottle(time, RegistrationOptions), new AttemptThrottle(time, LookupOptions));
    }
}
