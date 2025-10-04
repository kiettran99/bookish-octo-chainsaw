using Common.Shared.Models.Events;

namespace Common.Interfaces.Messaging;

public interface IServiceLogPublisher
{
    Task WriteLogAsync(ServiceLogMessage message);
}
