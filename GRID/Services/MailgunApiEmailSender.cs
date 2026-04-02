namespace GRID.Services
{
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.Extensions.Configuration;
    using System.Net.Http.Headers;
    using System.Text;

    public class MailgunApiEmailSender(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<MailgunApiEmailSender> logger) : IExtendedEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => SendEmailAsync(email, subject, htmlMessage, replyTo: null);

        public async Task SendEmailAsync(string email, string subject, string htmlMessage, string? replyTo)
        {
            var mailgun = config.GetSection("Mailgun");
            var domain  = mailgun["Domain"];
            var apiKey  = mailgun["ApiKey"];

            logger.LogInformation("Sending email via Mailgun to {Email} (domain: {Domain})", email, domain);

            using var httpClient = httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.mailgun.net/v3/{domain}/messages"
            );

            var authToken = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"api:{apiKey}")
            );

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", authToken);

            var fields = new Dictionary<string, string>
            {
                ["from"]    = $"{mailgun["FromName"]} <{mailgun["FromEmail"]}>",
                ["to"]      = email,
                ["subject"] = subject,
                ["html"]    = htmlMessage
            };

            if (!string.IsNullOrWhiteSpace(replyTo))
                fields["h:Reply-To"] = replyTo;

            request.Content = new FormUrlEncodedContent(fields);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogError("Mailgun returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            }

            response.EnsureSuccessStatusCode();
        }
    }
}
