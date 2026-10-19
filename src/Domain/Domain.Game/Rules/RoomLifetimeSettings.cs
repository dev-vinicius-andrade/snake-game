namespace Domain.Game.Rules;

/// <summary>How long private rooms live. See docs/ROOMS.md.</summary>
public sealed record RoomLifetimeOptions
{
    public const int MaxMinutes = 7 * 24 * 60;

    /// <summary>Minutes a private room may stay empty before it is closed.</summary>
    public int EmptyPrivateRoomMinutes { get; init; } = 10;

    /// <summary>Minutes after which a private room is closed whoever is in it. Zero: no limit.</summary>
    public int MaxPrivateRoomMinutes { get; init; }
}

/// <summary>The room lifetime limits in force, changeable while the server runs.</summary>
public sealed class RoomLifetimeSettings
{
    private volatile RoomLifetimeOptions _options;

    public RoomLifetimeSettings(RoomLifetimeOptions options)
    {
        _options = Validated(options);
    }

    public RoomLifetimeSettings() : this(new RoomLifetimeOptions()) { }

    public RoomLifetimeOptions Options => _options;

    public TimeSpan EmptyFor => TimeSpan.FromMinutes(_options.EmptyPrivateRoomMinutes);

    /// <summary>The age at which a private room is closed whoever is in it, or null.</summary>
    public TimeSpan? MaxAge => _options.MaxPrivateRoomMinutes > 0 ? TimeSpan.FromMinutes(_options.MaxPrivateRoomMinutes) : null;

    /// <summary>Switches to other limits. Limits that do not hold up are refused and the old ones stay.</summary>
    public void Reconfigure(RoomLifetimeOptions options) => _options = Validated(options);

    private static RoomLifetimeOptions Validated(RoomLifetimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.EmptyPrivateRoomMinutes is < 1 or > RoomLifetimeOptions.MaxMinutes)
            throw new ArgumentOutOfRangeException(nameof(options), options.EmptyPrivateRoomMinutes, $"A private room may stay empty for 1 to {RoomLifetimeOptions.MaxMinutes} minutes.");
        if (options.MaxPrivateRoomMinutes is < 0 or > RoomLifetimeOptions.MaxMinutes)
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxPrivateRoomMinutes, $"The longest a private room lives is 0 (no limit) to {RoomLifetimeOptions.MaxMinutes} minutes.");
        return options;
    }
}
