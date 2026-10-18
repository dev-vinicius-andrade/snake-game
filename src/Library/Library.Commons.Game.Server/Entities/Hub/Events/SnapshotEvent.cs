using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

/// <summary>
/// What the game server sends players about 30 times a second, so it is built to be small: coordinates are one flat
/// list, plain values are left out, names are short where there are many of a thing. See SnapshotBuilder.
/// </summary>
public record SnapshotEvent
{
    [JsonPropertyName("room")]
    public SnapshotRoom Room { get; set; } = new();

    [JsonPropertyName("snakes")]
    public List<SnapshotSnake> Snakes { get; set; } = new();

    [JsonPropertyName("foods")]
    public List<SnapshotFood> Foods { get; set; } = new();

    [JsonPropertyName("shots")]
    public List<SnapshotShot> Shots { get; set; } = new();
}

public record SnapshotRoom
{
    [JsonPropertyName("guid")]
    public Guid Guid { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public record SnapshotSnake
{
    [JsonPropertyName("guid")]
    public Guid Guid { get; set; }

    /// <summary>Connection id of the owner.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("dead"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Dead { get; set; }

    [JsonPropertyName("boost"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Boost { get; set; }

    [JsonPropertyName("autoFire"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoFire { get; set; }

    [JsonPropertyName("score")]
    public long Score { get; set; }

    [JsonPropertyName("foodsEaten"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FoodsEaten { get; set; }

    /// <summary>Fill colour, for example #aa5500.</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("perks")]
    public List<string>? Perks { get; set; }

    /// <summary>x0, y0, x1, y1, ... of the body segments, tail first and the head last, to a tenth of a unit.</summary>
    [JsonPropertyName("path")]
    public List<double> Path { get; set; } = new();

    /// <summary>HP per segment in whole percents, aligned with the path. Left out while nothing is hurt.</summary>
    [JsonPropertyName("health")]
    public List<int>? Health { get; set; }
}

public record SnapshotFood
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>Mass it gives; left out when it is 1.</summary>
    [JsonPropertyName("v")]
    public long? Value { get; set; }

    /// <summary>1 for the prize; left out for ordinary food.</summary>
    [JsonPropertyName("k")]
    public int? Kind { get; set; }

    [JsonPropertyName("c")]
    public string Color { get; set; } = string.Empty;
}

public record SnapshotShot
{
    [JsonPropertyName("guid")]
    public Guid Guid { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Guid of the snake that shot it.</summary>
    [JsonPropertyName("owner")]
    public Guid Owner { get; set; }
}
