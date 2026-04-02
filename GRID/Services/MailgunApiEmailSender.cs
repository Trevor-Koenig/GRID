namespace GRID.Services
{
    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.Extensions.Configuration;
    using System.Net.Http.Headers;
    using System.Text;

    public class MailgunApiEmailSender(IHttpClientFactory httpClientFactory, IConfiguration config) : IExtendedEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => SendEmailAsync(email, subject, htmlMessage, replyTo: null);

        public async Task SendEmailAsync(string email, string subject, string htmlMessage, string? replyTo)
        {
            var mailgun = config.GetSection("Mailgun");

            using var httpClient = httpClientFactory.CreateClient();

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.mailgun.net/v3/{mailgun["Domain"]}/messages"
            );

            var authToken = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"api:{mailgun["ApiKey"]}")
            );

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", authToken);

            var fields = new Dictionary<string, string>
            {
                ["from"] = $"{mailgun["FromName"]} <{mailgun["FromEmail"]}>",
                ["to"] = email,
                ["subject"] = subject,
                ["html"] = htmlMessage
            };

            if (!string.IsNullOrWhiteSpace(replyTo))
                fields["h:Reply-To"] = replyTo;

            request.Content = new FormUrlEncodedContent(fields);

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
