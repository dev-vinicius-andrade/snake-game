namespace Application.Manager.Api.Persistence;

/// <summary>An account as a row. The username is kept as typed (<see cref="Username"/>) and lowered (<see cref="UsernameKey"/>), which is what must be unique with the tag.</summary>
public sealed class AccountRow
{
    public Guid Id { get; set; }

    public string Username { get; set; } = "";

    public string UsernameKey { get; set; } = "";

    public int Tag { get; set; }

    public string PhraseHash { get; set; } = "";

    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<DeviceRow> Devices { get; set; } = [];

    public List<ExternalLoginRow> Logins { get; set; } = [];
}

/// <summary>A signed in browser. Only the hash of its token, which is unique over all accounts.</summary>
public sealed class DeviceRow
{
    public string Hash { get; set; } = "";

    public Guid AccountId { get; set; }

    public string? Label { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A linked social account: the provider (lower case) and the keyed hash of its id, which together belong to one account only.</summary>
public sealed class ExternalLoginRow
{
    public Guid AccountId { get; set; }

    public string Provider { get; set; } = "";

    public string IdHash { get; set; } = "";

    public DateTimeOffset LinkedAt { get; set; }
}

/// <summary>
/// One row per account that ever had a report. <see cref="Version"/> is bumped by every report and is a concurrency token, so two game servers
/// reporting for the same account at once cannot overwrite each other's numbers: the second one starts again from what the first saved.
/// </summary>
public sealed class StatsHeadRow
{
    public Guid AccountId { get; set; }

    public int Version { get; set; }
}

public enum StatKind
{
    Counter = 0,
    Maximum = 1,
}

/// <summary>A counter total or a highest maximum, by key.</summary>
public sealed class StatValueRow
{
    public Guid AccountId { get; set; }

    public StatKind Kind { get; set; }

    public string Key { get; set; } = "";

    public long Value { get; set; }
}

/// <summary>A report already applied. <see cref="Sequence"/> says which came last: only the newest 200 per account are kept.</summary>
public sealed class AppliedReportRow
{
    public Guid AccountId { get; set; }

    public Guid ReportId { get; set; }

    public long Sequence { get; set; }
}

public sealed class UnlockedAchievementRow
{
    public Guid AccountId { get; set; }

    public string AchievementId { get; set; } = "";

    public DateTimeOffset UnlockedAt { get; set; }
}
