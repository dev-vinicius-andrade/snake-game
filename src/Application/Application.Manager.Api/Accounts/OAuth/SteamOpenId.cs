using System.Text.RegularExpressions;

namespace Application.Manager.Api.Accounts.OAuth;

/// <summary>
/// Steam does not do OAuth: it signs players in with OpenID 2.0, and says who they are only as a Steam id. The browser goes to Steam with a
/// request, Steam sends it back with a signed answer, and the game checks the answer with Steam itself before believing it.
/// </summary>
public static partial class SteamOpenId
{
    public const string Endpoint = "https://steamcommunity.com/openid/login";

    private const string Ns = "http://specs.openid.net/auth/2.0";
    private const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";

    /// <summary>The address to send the browser to (<paramref name="endpoint"/> is Steam's login address, or another one for a test server). The state travels in the return address, which Steam signs and checks when it is asked about the answer.</summary>
    public static string StartUrl(string endpoint, string returnUrl, string realm, string state)
    {
        var parameters = new (string Name, string Value)[]
        {
            ("openid.ns", Ns),
            ("openid.mode", "checkid_setup"),
            ("openid.return_to", ReturnTo(returnUrl, state)),
            ("openid.realm", realm),
            ("openid.identity", IdentifierSelect),
            ("openid.claimed_id", IdentifierSelect),
        };
        return endpoint + "?" + string.Join('&', parameters.Select(pair => $"{Uri.EscapeDataString(pair.Name)}={Uri.EscapeDataString(pair.Value)}"));
    }

    /// <summary>The address Steam is told to return to, and has to return to exactly.</summary>
    public static string ReturnTo(string returnUrl, string state) => $"{returnUrl}?state={Uri.EscapeDataString(state)}";

    /// <summary>The 17 digit Steam id in an identity Steam gave, or null for anything that is not Steam's own address for a player.</summary>
    public static string? ReadSteamId(string? claimedIdentity)
    {
        var match = claimedIdentity is null ? null : ClaimedIdentity().Match(claimedIdentity);
        return match is { Success: true } ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^https?://steamcommunity\.com/openid/id/(\d{17})\z", RegexOptions.CultureInvariant)]
    private static partial Regex ClaimedIdentity();
}
