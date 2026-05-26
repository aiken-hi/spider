using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Spider.Serilog.Sink;

public static class EnvironmentHelper
{
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();
    private static DateTime _lastCpuTime = DateTime.UtcNow;
    private static TimeSpan _lastCpuUsage = CurrentProcess.TotalProcessorTime;
    public static string AppFolder => AppDomain.CurrentDomain.BaseDirectory;
    public static string InternalIp { get; } = GetLocalIpAddress();

    // 获取当前进程消耗的物理内存 (MB)
    public static double GetMemoryUsage()
    {
        CurrentProcess.Refresh();
        return Math.Round(CurrentProcess.WorkingSet64 / 1024.0 / 1024.0, 2);
    }

    // 简易进程级 CPU 使用率采样
    public static double GetCpuUsage()
    {
        var now = DateTime.UtcNow;
        var cpuTime = CurrentProcess.TotalProcessorTime;

        var timeWindow = now - _lastCpuTime;
        var systemTimePassed = cpuTime - _lastCpuUsage;

        _lastCpuTime = now;
        _lastCpuUsage = cpuTime;

        if (timeWindow.TotalMilliseconds == 0) return 0;

        var cpuPercent = (systemTimePassed.TotalMilliseconds / (timeWindow.TotalMilliseconds * Environment.ProcessorCount)) * 100;
        return Math.Round(Math.Clamp(cpuPercent, 0, 100), 2);
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
