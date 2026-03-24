
namespace GRID.Services
{
    using Microsoft.AspNetCore.Identity.UI.Services;
    public class DevEmailSender : IEmailSender
    {
        private readonly string _emailsFolder;

        public DevEmailSender()
        {
            // Create a folder in the project root called "Emails" to save test emails
            _emailsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Emails");
            if (!Directory.Exists(_emailsFolder))
            {
                Directory.CreateDirectory(_emailsFolder);
            }
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Create a filename using timestamp
            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.html";
            var filePath = Path.Combine(_emailsFolder, fileName);

            // Build the content
            var content = $@"
                            <!DOCTYPE html>
                            <html>
                            <head>
                                <meta charset='utf-8'>
                                <title>{subject}</title>
                            </head>
                            <body>
                                <h2>To: {email}</h2>
                                <h3>Subject: {subject}</h3>
                                <hr />
                                {htmlMessage}
                            </body>
                            </html>";

            // Write the file
            await File.WriteAllTextAsync(filePath, content);

            Console.WriteLine($"[DevEmailSender] Email saved to {filePath}");
        }
    }
}
