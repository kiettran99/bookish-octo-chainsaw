using Common.Shared.Models.Users;
using Identity.Domain.Interfaces.Messagings;
using MassTransit;

namespace Identity.Infrastructure.Implements.Messagings;

public class SyncUserPortalPublisher : ISyncUserPortalPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public SyncUserPortalPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task SyncUserPortalAsync(SyncUserPortalMessage message)
    {
        await _publishEndpoint.Publish(message);
    }
}
