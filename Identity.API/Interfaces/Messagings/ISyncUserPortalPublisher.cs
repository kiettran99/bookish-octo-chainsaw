using Common.Shared.Models.Users;

namespace Identity.Domain.Interfaces.Messagings;

public interface ISyncUserPortalPublisher
{
    Task SyncUserPortalAsync(SyncUserPortalMessage message);
}