using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spider.Models;

public enum ServiceStatus { Online, Offline, Fault }

public class ServiceInstance
{
    [Key]
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string? AlertEmail { get; set; }
    public DateTime LastSeen { get; set; }
    public ServiceStatus Status { get; set; }
    public bool IsLogDiscovery { get; set; } // 是否为日志无感知注册
    public string LogContent { get; set; } = string.Empty; // 仅日志无感知注册时使用

    public string InternalIp { get; set; } = "127.0.0.1";
    public string ExternalIp { get; set; } = "未知";
    public string AppFolder { get; set; } = "未知";

    // ====== 新增实时性能指标 ======
    public double CpuUsage { get; set; }
    public double MemoryUsageMb { get; set; }

    // 内存队列，不映射到数据库
    [NotMapped]
    public ConcurrentQueue<string> RecentLogs { get; } = new();
}
