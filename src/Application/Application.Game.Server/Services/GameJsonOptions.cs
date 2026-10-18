using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Game.Server.Services;

/// <summary>
/// JSON for everything the game server sends. A snapshot goes out about 30 times a second to every player, so it is
/// kept small: no indentation and numbers rounded to two decimals, which is finer than a pixel of the screen.
/// </summary>
public static class GameJsonOptions
{
    public static JsonSerializerOptions Create() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new RoundedDoubleConverter() }
    };
}

/// <summary>Writes a double with at most two decimals; reads like any other double.</summary>
public sealed class RoundedDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        if (double.IsFinite(value)) writer.WriteNumberValue(Math.Round(value, 2, MidpointRounding.AwayFromZero));
        else writer.WriteNumberValue(value);
    }
}
