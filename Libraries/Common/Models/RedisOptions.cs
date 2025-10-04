namespace Common.Models;

public class RedisOptions
{
    public string ConnectionString { get; set; } = null!;
    public string Host { get; set; } = null!;
    public string Port { get; set; } = null!;
    public string InstanceName { get; set; } = null!;
}