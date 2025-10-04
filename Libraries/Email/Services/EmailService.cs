using Email.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Email.Services;

public class EmailService : IEmailService
{
    private readonly EmailOptions _emailOptions;

    public EmailService(
        EmailOptions emailOptions
    )
    {
        _emailOptions = emailOptions;
    }

    public async Task SendMailAsync(string subject, string body, List<string> toEmails, List<string>? ccEmails = null, List<EmailAttachment>? attachments = null)
    {
        await HandleSendMailAsync(subject, body, toEmails, ccEmails, attachments);
    }

    private async Task HandleSendMailAsync(string subject, string body, List<string> toEmails, List<string>? ccEmails = null, List<EmailAttachment>? attachments = null)
    {
        // create message
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_emailOptions.SenderName, _emailOptions.MailFrom ?? string.Empty));

        foreach (var toEmail in toEmails.Where(email => !string.IsNullOrEmpty(email)))
        {
            email.To.Add(MailboxAddress.Parse(toEmail));
        }

        //cc emails
        if (ccEmails?.Count > 0)
        {
            foreach (var ccEmail in ccEmails.Where(email => !toEmails.Contains(email) && !string.IsNullOrEmpty(email)))
            {
                email.Cc.Add(MailboxAddress.Parse(ccEmail));
            }
        }

        if (_emailOptions.Environment != "Production")
        {
            email.Subject = $"[{_emailOptions.Environment}] " + subject;
        }
        else
        {
            email.Subject = subject;
        }

        var builder = new BodyBuilder();
        builder.HtmlBody = body;

        if (attachments?.Count > 0)
        {
            // Add attachments
            foreach (var attachment in attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Attachment);
            }
        }

        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_emailOptions.SmtpServer, _emailOptions.SmtpPort, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_emailOptions.SmtpUser, _emailOptions.SmtpPassword);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}
