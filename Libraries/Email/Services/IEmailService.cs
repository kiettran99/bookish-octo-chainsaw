using Email.Models;

namespace Email.Services;

public interface IEmailService
{
    Task SendMailAsync(string subject, string body, List<string> toEmails, List<string>? ccEmails = null, List<EmailAttachment>? attachments = null);
}