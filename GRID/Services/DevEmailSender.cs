namespace GRID.Services
{
    using Microsoft.Extensions.Configuration;
    using System.Net.Http.Headers;
    using System.Text;

    public class DevEmailSender(IConfiguration config, IHttpClientFactory httpClientFactory) : IExtendedEmailSender
    {
        private readonly string _emailsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Emails");

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => SendEmailAsync(email, subject, htmlMessage, replyTo: null);

        public async Task SendEmailAsync(string email, string subject, string htmlMessage, string? replyTo)
        {
            // Ensure folder exists
            Directory.CreateDirectory(_emailsFolder);

            // Write to file
            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.html";
            var filePath = Path.Combine(_emailsFolder, fileName);
            var fileContent = $"""
                <!DOCTYPE html>
                <html>
                <head><meta charset='utf-8'><title>{subject}</title></head>
                <body>
                    <h2>To: {email}</h2>
                    <h3>Subject: {subject}</h3>
                    <hr />
                    {htmlMessage}
                </body>
                </html>
                """;
            await File.WriteAllTextAsync(filePath, fileContent);
            Console.WriteLine($"[DevEmailSender] Email saved to {filePath}");

            // Forward to dev notify address via Mailgun if configured
            var notifyEmail = config["Dev:NotifyEmail"];
            var mailgun = config.GetSection("Mailgun");
            var apiKey = mailgun["ApiKey"];

            if (string.IsNullOrWhiteSpace(notifyEmail) || string.IsNullOrWhiteSpace(apiKey))
                return;

            var footer = $"""
                <hr style="margin-top:2rem;">
                <p style="color:#888;font-size:11px;font-family:monospace;">
                    [DEV] This email would have been sent to: <strong>{System.Net.WebUtility.HtmlEncode(email)}</strong>
                </p>
                """;

            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://api.mailgun.net/v3/{mailgun["Domain"]}/messages");

                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{apiKey}")));

                var fields = new Dictionary<string, string>
                {
                    ["from"]    = $"{mailgun["FromName"]} <{mailgun["FromEmail"]}>",
                    ["to"]      = notifyEmail,
                    ["subject"] = $"[DEV] {subject}",
                    ["html"]    = htmlMessage + footer,
                };

                if (!string.IsNullOrWhiteSpace(replyTo))
                    fields["h:Reply-To"] = replyTo;

                request.Content = new FormUrlEncodedContent(fields);

                var client = httpClientFactory.CreateClient();
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                    Console.WriteLine($"[DevEmailSender] Forwarded to {notifyEmail} via Mailgun");
                else
                    Console.WriteLine($"[DevEmailSender] Mailgun forward failed: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DevEmailSender] Mailgun forward error: {ex.Message}");
            }
        }
    }
}
