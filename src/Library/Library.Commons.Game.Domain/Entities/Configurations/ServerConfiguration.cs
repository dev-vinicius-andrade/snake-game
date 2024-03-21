using System.Text.Json.Serialization;

namespace Library.Commons.Game.Domain.Entities.Configurations;

public class ServerConfiguration
{
    public const string SectionName = "ServerConfiguration";

    [JsonPropertyName("roomsConfiguration")]
    public RoomsConfiguration RoomsConfiguration { get; set; }

    [JsonPropertyName("tickRateTimeSpan")]
    public TimeSpanConfiguration? TickRateTimeSpan { get; set; }
}