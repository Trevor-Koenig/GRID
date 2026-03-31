using Microsoft.AspNetCore.Identity.UI.Services;

namespace GRID.Services
{
    public interface IExtendedEmailSender : IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage, string? replyTo);
    }
}
