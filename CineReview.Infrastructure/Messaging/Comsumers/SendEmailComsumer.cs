using Common.Shared.Models.Emails;
using Email.Services;
using MassTransit;

namespace CineReview.Infrastructure.Messaging.Comsumers;

public class SendEmailComsumer : IConsumer<SendEmailMessage>
{
    private readonly IEmailService _emailService;

    public SendEmailComsumer(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Consume(ConsumeContext<SendEmailMessage> context)
    {
        var sendMailMessage = context.Message;

        await _emailService.SendMailAsync(sendMailMessage.Subject, sendMailMessage.Body,
            sendMailMessage.ToEmails, sendMailMessage.CcEmails,
            sendMailMessage.Attachments?.Select(x => new Email.Models.EmailAttachment
            {
                FileName = x.FileName,
                Attachment = x.Attachment
            }).ToList());
    }
}