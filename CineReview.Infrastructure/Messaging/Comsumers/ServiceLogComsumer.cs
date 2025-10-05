using System.Text.Json;
using Common.Enums;
using Common.Shared.Models.Events;
using MassTransit;
using Serilog;

namespace CineReview.Infrastructure.Messaging.Comsumers;

public class ServiceLogComsumer : IConsumer<ServiceLogMessage>
{
    public async Task Consume(ConsumeContext<ServiceLogMessage> context)
    {
        var serviceLogMessage = context.Message;
        switch (serviceLogMessage.LogLevel)
        {
            case ELogLevel.Information:
                Log.Logger.Information(
                    "[{@Environment} {@ServiceName}] {@EventName} - {@Description} - Detail",
                    serviceLogMessage.Environment,
                    serviceLogMessage.ServiceName, serviceLogMessage.EventName,
                    serviceLogMessage.Description, JsonSerializer.Serialize(serviceLogMessage));
                break;
            case ELogLevel.Warning:
                Log.Logger.Warning("[{@Environment} {@ServiceName}] {@EventName} - {@Description} - Detail",
                    serviceLogMessage.Environment,
                    serviceLogMessage.ServiceName, serviceLogMessage.EventName,
                    serviceLogMessage.Description, JsonSerializer.Serialize(serviceLogMessage));
                break;
            case ELogLevel.Error:
                Log.Logger.Error(
                    "[{@Environment} {@ServiceName}] {@EventName} - {@Description} - Detail",
                    serviceLogMessage.Environment,
                    serviceLogMessage.ServiceName, serviceLogMessage.EventName,
                    serviceLogMessage.Description,
                    JsonSerializer.Serialize(serviceLogMessage));
                break;
        }

        // Cheat warning async/await
        await Task.FromResult(0);
    }
}