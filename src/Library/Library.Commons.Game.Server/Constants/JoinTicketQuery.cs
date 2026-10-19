using System;

namespace Library.Commons.Game.Server.Constants;

/// <summary>
/// A signed-in player proves to the game hub which account it joined as with a one-time ticket that the game server made when it
/// accepted the join. The ticket rides in the query of the hub address the player is given (<c>gameServerUrl</c>), so a client
/// that connects to the address it was given sends it without knowing about it (docs/ACHIEVEMENTS.md).
/// </summary>
public static class JoinTicketQuery
{
    public const string Name = "joinTicket";

    /// <summary>The hub address with the ticket in its query; the address unchanged when there is no ticket (a guest).</summary>
    public static string AppendTo(string hubUrl, string? ticket)
    {
        ArgumentNullException.ThrowIfNull(hubUrl);
        if (string.IsNullOrWhiteSpace(ticket))
            return hubUrl;
        var separator = hubUrl.Contains('?') ? '&' : '?';
        return $"{hubUrl}{separator}{Name}={Uri.EscapeDataString(ticket)}";
    }
}
