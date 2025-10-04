using Common.Shared.Models.Emails;

namespace Common.Interfaces.Messaging;

public interface ISendMailPublisher
{
    Task SendMailAsync(SendEmailMessage message);
}