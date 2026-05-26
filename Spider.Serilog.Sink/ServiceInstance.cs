using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Spider.Serilog.Sink;

public class ServiceInstance
{
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string LogContent { get; set; } = string.Empty; // 仅日志无感知注册时使用
    public double CpuUsage { get; set; }
    public double MemoryUsageMb { get; set; }
    public string InternalIp { get; set; } = "127.0.0.1";
    public string ExternalIp { get; set; } = "未知";
    public string AppFolder { get; set; } = "未知";
}
