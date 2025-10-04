using Common.Interfaces.Messaging;
using Common.Shared.Models.Events;
using MassTransit;

namespace Common.Implements.Messaging;

public class ServiceLogPublisher : IServiceLogPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public ServiceLogPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task WriteLogAsync(ServiceLogMessage message)
    {
        await _publishEndpoint.Publish(message);
    }
}