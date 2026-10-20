using System.Net;
using System.Text;
using Application.Manager.Api.Accounts.OAuth;
using Microsoft.Extensions.Logging;

namespace Application.Manager.Api.Tests;

public sealed class HttpOAuthClientTests
{
    private sealed class Reply(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(Uri? Address, string Form)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Read now: the client disposes the request when it is done.
            Requests.Add((request.RequestUri, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ListLogger : ILogger<HttpOAuthClient>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    private static readonly OAuthProviderInfo GitHub = OAuthProviders.Get("github")!;
    private static readonly OAuthCredentials Credentials = new() { ClientId = "the-id", ClientSecret = "the-secret" };

    private static Task<string?> Exchange(HttpOAuthClient client) => client.ExchangeCodeAsync(GitHub, Credentials, "code", "https://x/cb", "verifier", CancellationToken.None);

    [Fact]
    public async Task ExchangeCode_GivesTheAccessToken()
    {
        var client = new HttpOAuthClient(new HttpClient(new Reply(HttpStatusCode.OK, """{ "access_token": "gho_abc", "token_type": "bearer" }""")));

        Assert.Equal("gho_abc", await Exchange(client));
    }

    [Fact]
    public async Task ExchangeCode_SendsTheCredentialsAsAFormToTheTokenAddress_NeverInTheAddress()
    {
        var handler = new Reply(HttpStatusCode.OK, """{ "access_token": "t" }""");

        await Exchange(new HttpOAuthClient(new HttpClient(handler)));

        var (address, form) = Assert.Single(handler.Requests);
        Assert.Equal(GitHub.TokenUrl, address!.ToString());
        Assert.DoesNotContain("the-secret", address.Query);
        Assert.Contains("client_secret=the-secret", form);
        Assert.Contains("code_verifier=verifier", form);
    }

    [Fact]
    public async Task ExchangeCode_AnErrorTheProviderSaysIsLogged_WithItsCodeAndNothingSecret()
    {
        var logger = new ListLogger();
        var client = new HttpOAuthClient(new HttpClient(new Reply(HttpStatusCode.OK, """{ "error": "incorrect_client_credentials", "error_description": "The client_id and/or client_secret passed are incorrect." }""")), logger);

        var token = await Exchange(client);

        Assert.Null(token);
        var line = Assert.Single(logger.Lines);
        Assert.Contains("github", line);
        Assert.Contains("incorrect_client_credentials", line);
        Assert.DoesNotContain("the-secret", line);
        Assert.DoesNotContain("the-id", line);
    }

    [Fact]
    public async Task ExchangeCode_ABadStatusIsLoggedAndIsNoToken()
    {
        var logger = new ListLogger();
        var client = new HttpOAuthClient(new HttpClient(new Reply(HttpStatusCode.BadRequest, "oops")), logger);

        Assert.Null(await Exchange(client));

        Assert.Contains("400", Assert.Single(logger.Lines));
    }

    [Fact]
    public async Task ExchangeCode_AnAnswerThatIsNotJsonIsNoTokenAndNoCrash()
    {
        var client = new HttpOAuthClient(new HttpClient(new Reply(HttpStatusCode.OK, "<html>")), new ListLogger());

        Assert.Null(await Exchange(client));
    }
}
