using FluentAssertions;
using GRID.Services;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;

namespace GRID.Tests.Services;

/// <summary>
/// Tests for MailgunApiEmailSender.
/// Uses a capturing HttpMessageHandler stub so no real HTTP calls are made.
/// </summary>
public class MailgunApiEmailSenderTests
{
    // ── Stubs ─────────────────────────────────────────────────────────────────

    /// <summary>Captures the outgoing request and returns a configurable status code.</summary>
    private sealed class CapturingHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Buffer the content so callers can read it after the send.
            if (request.Content != null)
                await request.Content.LoadIntoBufferAsync();

            LastRequest = request;
            return new HttpResponseMessage(status);
        }
    }

    /// <summary>Returns a new HttpClient backed by the given handler on every call.</summary>
    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(
        string domain   = "mg.example.com",
        string apiKey   = "key-test123",
        string fromEmail = "no-reply@example.com",
        string fromName  = "GRID")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mailgun:Domain"]    = domain,
                ["Mailgun:ApiKey"]    = apiKey,
                ["Mailgun:FromEmail"] = fromEmail,
                ["Mailgun:FromName"]  = fromName,
            })
            .Build();
    }

    private static (MailgunApiEmailSender Sender, CapturingHandler Handler) Build(
        HttpStatusCode status = HttpStatusCode.OK,
        IConfiguration? config = null)
    {
        var handler = new CapturingHandler(status);
        var factory = new StubHttpClientFactory(handler);
        var sender  = new MailgunApiEmailSender(factory, config ?? BuildConfig());
        return (sender, handler);
    }

    /// <summary>Parses application/x-www-form-urlencoded body into a dictionary.</summary>
    private static Dictionary<string, string> ParseForm(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(
                p => Uri.UnescapeDataString(p[0].Replace('+', ' ')),
                p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));

    // ── URL / endpoint ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendEmailAsync_PostsToCorrectMailgunEndpoint()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://api.mailgun.net/v3/mg.example.com/messages");
    }

    [Fact]
    public async Task SendEmailAsync_UsesPostMethod()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
    }

    // ── Auth header ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendEmailAsync_SetsBasicAuthHeader()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        var auth = handler.LastRequest!.Headers.Authorization!;
        auth.Scheme.Should().Be("Basic");

        var decoded = Encoding.ASCII.GetString(Convert.FromBase64String(auth.Parameter!));
        decoded.Should().Be("api:key-test123");
    }

    // ── Form fields ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendEmailAsync_SendsCorrectToField()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("recipient@example.com", "Subject", "<p>Body</p>");

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form["to"].Should().Be("recipient@example.com");
    }

    [Fact]
    public async Task SendEmailAsync_SendsCorrectFromField()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form["from"].Should().Be("GRID <no-reply@example.com>");
    }

    [Fact]
    public async Task SendEmailAsync_SendsCorrectSubjectField()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Reset your password", "<p>Body</p>");

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form["subject"].Should().Be("Reset your password");
    }

    [Fact]
    public async Task SendEmailAsync_SendsCorrectHtmlField()
    {
        var (sender, handler) = Build();
        const string html = "<p>Click <a href='https://example.com'>here</a></p>";

        await sender.SendEmailAsync("user@example.com", "Subject", html);

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form["html"].Should().Be(html);
    }

    // ── Reply-To ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendEmailAsync_WithReplyTo_IncludesReplyToHeader()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>", replyTo: "admin@example.com");

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form["h:Reply-To"].Should().Be("admin@example.com");
    }

    [Fact]
    public async Task SendEmailAsync_WithNullReplyTo_OmitsReplyToHeader()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>", replyTo: null);

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form.Should().NotContainKey("h:Reply-To");
    }

    [Fact]
    public async Task SendEmailAsync_WithWhitespaceReplyTo_OmitsReplyToHeader()
    {
        var (sender, handler) = Build();

        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>", replyTo: "   ");

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form.Should().NotContainKey("h:Reply-To");
    }

    // ── Base overload delegates ───────────────────────────────────────────────

    [Fact]
    public async Task SendEmailAsync_ThreeParamOverload_OmitsReplyTo()
    {
        var (sender, handler) = Build();

        // The three-param overload should delegate to SendEmailAsync with replyTo: null
        await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        var form = ParseForm(await handler.LastRequest!.Content!.ReadAsStringAsync());
        form.Should().NotContainKey("h:Reply-To");
        form["to"].Should().Be("user@example.com");
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendEmailAsync_OnMailgunError_ThrowsHttpRequestException()
    {
        var (sender, _) = Build(status: HttpStatusCode.Unauthorized);

        var act = async () => await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendEmailAsync_OnServerError_ThrowsHttpRequestException()
    {
        var (sender, _) = Build(status: HttpStatusCode.InternalServerError);

        var act = async () => await sender.SendEmailAsync("user@example.com", "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
