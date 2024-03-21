namespace Application.Manager.Api.Entities.Configurations;

public class RedisConfiguration
{
    public const string SectionName = nameof(RedisConfiguration);
    public string Host { get; set; } = null!;
    public int? Port { get; set; }
    public string? ChannelPrefix { get; set; }
    public string? Password { get; set; }
}