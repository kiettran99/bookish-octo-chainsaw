using Common.Interfaces.Messaging;
using Common.Shared.Models.Emails;
using MassTransit;

namespace Common.Implements.Messaging;

public class SendMailPublisher : ISendMailPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public SendMailPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task SendMailAsync(SendEmailMessage message)
    {
        await _publishEndpoint.Publish(message);
    }
}