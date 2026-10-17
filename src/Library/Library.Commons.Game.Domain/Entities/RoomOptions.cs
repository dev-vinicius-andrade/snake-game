namespace Library.Commons.Game.Domain.Entities;

/// <summary>What the host asked for when creating a room. Everything is optional.</summary>
public sealed record RoomOptions
{
    public string? Name { get; init; }

    /// <summary>The invite code. Only rooms with a code can be joined by it.</summary>
    public string? Code { get; init; }

    /// <summary>Private rooms are never picked for players who just want to play.</summary>
    public bool IsPrivate { get; init; }

    /// <summary>Zero or less means the server default. It is capped to what the server allows.</summary>
    public int MaxPlayers { get; init; }
}
