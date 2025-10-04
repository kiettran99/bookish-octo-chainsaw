using Common.Enums;

namespace Common.Shared.Models.Events;

public class ServiceLogMessage
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public DateTime CreatedOnUtc { get; } = DateTime.UtcNow;

    public ELogLevel LogLevel { get; set; }
    public string EventName { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public string Environment { get; set; } = null!;

    public string? Description { get; set; }
    public string? StackTrace { get; set; }

    public string? IpAddress { get; set; }
    public string? UserId { get; set; }
    public int? PortalUserId { get; set; }

    public string? Url { get; set; }
    public string? Request { get; set; }
    public string? Response { get; set; }
    public string? StatusCode { get; set; }
}