namespace GRID.Services
{

    using Microsoft.AspNetCore.Identity.UI.Services;
    using Microsoft.Extensions.Configuration;
    using System.Net.Http.Headers;
    using System.Text;

    public class MailgunApiEmailSender : IEmailSender
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public MailgunApiEmailSender(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var mailgun = _config.GetSection("Mailgun");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.mailgun.net/v3/{mailgun["Domain"]}/messages"
            );

            var authToken = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"api:{mailgun["ApiKey"]}")
            );

            var apiKey = _config["Mailgun:ApiKey"];
            Console.WriteLine($"API key length: {apiKey}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", authToken);

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["from"] = $"{mailgun["FromName"]} <{mailgun["FromEmail"]}>",
                ["to"] = email,
                ["subject"] = subject,
                ["html"] = htmlMessage
            });

            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();
        }
    }
}
