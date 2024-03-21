using System.Text.Json.Serialization;

namespace Library.Commons.Eventbus.RabbitMq.Configurations;

public class EventbusConfiguration
{
    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; } = null!;
}