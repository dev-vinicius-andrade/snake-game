using System.Reflection;
using System.Text.Json;
using Application.Manager.Api.Controllers;
using Application.Manager.Api.Rooms;
using Application.Manager.Api.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class TelemetryControllerTests
{
    private readonly PerfReportStore _store = new(new FakeTimeProvider(), capacity: 10);

    private TelemetryController Controller() => new(_store, NullLogger<TelemetryController>.Instance);

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static int Status(IActionResult result) => Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode!.Value;

    [Fact]
    public void Receive_KeepsTheReportOfASession()
    {
        var result = Controller().Receive(Body("""{"session":"abc","fps":12}"""));

        Assert.Equal(StatusCodes.Status204NoContent, Status(result));
        Assert.Equal("abc", Assert.Single(_store.Latest(null, 10)).Session);
    }

    [Theory]
    [InlineData("""{"fps":12}""")]
    [InlineData("""{"session":""}""")]
    [InlineData("""{"session":5}""")]
    [InlineData("""[1,2]""")]
    public void Receive_RefusesAReportWithoutASession(string json)
    {
        var result = Controller().Receive(Body(json));

        Assert.Equal(StatusCodes.Status400BadRequest, Status(result));
        Assert.Empty(_store.Latest(null, 10));
    }

    [Fact]
    public void Receive_RefusesASessionNameThatIsTooLong()
    {
        var result = Controller().Receive(Body($$"""{"session":"{{new string('a', 65)}}"}"""));

        Assert.Equal(StatusCodes.Status400BadRequest, Status(result));
    }

    [Fact]
    public void Receive_RefusesAReportThatIsTooBig()
    {
        var result = Controller().Receive(Body($$"""{"session":"abc","filler":"{{new string('x', 9000)}}"}"""));

        Assert.Equal(StatusCodes.Status400BadRequest, Status(result));
    }

    [Fact]
    public void Reports_GivesWhatWasKept()
    {
        Controller().Receive(Body("""{"session":"abc","fps":12}"""));

        var result = Controller().Reports(session: null, take: 10);

        var kept = Assert.IsAssignableFrom<IEnumerable<PerfReport>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("abc", Assert.Single(kept).Session);
    }

    [Fact]
    public void Reports_AreForAdministratorsOnly()
    {
        var method = typeof(TelemetryController).GetMethod(nameof(TelemetryController.Reports))!;

        Assert.NotNull(method.GetCustomAttribute<AdminAuth>());
    }

    [Fact]
    public void Receive_IsOpenToGuests()
    {
        var method = typeof(TelemetryController).GetMethod(nameof(TelemetryController.Receive))!;

        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
