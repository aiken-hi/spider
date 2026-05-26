using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Spider.Serilog.Sink;

public class SpiderSink : ILogEventSink, IDisposable
{
    private readonly string _spiderUrl;
    private readonly string _serviceId;
    private readonly string _serviceName;
    private readonly IFormatProvider? _formatProvider;
    private readonly HttpClient _httpClient;
    private HubConnection? _hubConnection;

    public SpiderSink(string spiderUrl, string serviceId, string serviceName, IFormatProvider? formatProvider)
    {
        _spiderUrl = spiderUrl.TrimEnd('/');
        _serviceId = serviceId;
        _serviceName = serviceName;
        _formatProvider = formatProvider;
        _httpClient = new HttpClient { BaseAddress = new Uri(_spiderUrl) };

        // ====== 新增：初始化客户端的反向控制通道 ======
        InitControlChannel();

        SendRegisterSignal();
    }
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage(_formatProvider);
        if (logEvent.Exception != null)
        {
            message += $"\n[Exception]: {logEvent.Exception.Message}\n[StackTrace]: {logEvent.Exception.StackTrace}";
        }

        Task.Run(async () =>
        {
            try
            {
                var folder = Uri.EscapeDataString(EnvironmentHelper.AppFolder);
                var cpu = EnvironmentHelper.GetCpuUsage();
                var mem = EnvironmentHelper.GetMemoryUsage();

                // 扩充传输管道，带上指标数据
                var url = $"/spider/ingest-logs";

                var instance = new ServiceInstance
                {
                    ServiceId = _serviceId,
                    ServiceName = _serviceName,
                    InternalIp = EnvironmentHelper.InternalIp,
                    AppFolder = EnvironmentHelper.AppFolder,
                    CpuUsage = cpu,
                    MemoryUsageMb = mem,
                    LogContent = $"[{logEvent.Level}] {message}"
                };

                var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(url, content);
            }
            catch { /* 自卫逻辑 */ }
        });
    }
    public void Dispose()
    {
        _hubConnection?.DisposeAsync().AsTask().Wait();
        _httpClient.Dispose();
    }
    private void InitControlChannel()
    {
        Task.Run(async () =>
        {
            try
            {
                // 构建通往 Spider 服务端的长连接
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{_spiderUrl}/hub/logs")
                    .WithAutomaticReconnect()
                    .Build();

                // 监听来自大屏的远程命令
                _hubConnection.On<string>("ReceiveCommand", (command) =>
                {
                    if (command == "RESTART")
                    {
                        // 记录一条本地日志说明是被远程重启的
                        SelfLog("收到 Spider 服务端远程重启指令，正在启动外部托管重启脚本...");

                        // 强制压出未发完的日志
                        Log.CloseAndFlush();
                        this.Dispose();

                        // 使用非 0 错误码，告诉系统“我崩溃了，请帮我重启”
                        Environment.Exit(1024);
                    }
                });

                await _hubConnection.StartAsync();

                // 注册本节点到专属控制组
                await _hubConnection.InvokeAsync("RegisterClientNode", _serviceId);
            }
            catch (Exception ex)
            {
                SelfLog($"控制通道初始化失败: {ex.Message}");
            }
        });
    }
    private void SelfLog(string msg) => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Spider-Sink] {msg}");
    private void SendRegisterSignal()
    {
        // 使用 Task.Run 避免阻塞主线程启动
        Task.Run(async () =>
        {
            var folder = Uri.EscapeDataString(EnvironmentHelper.AppFolder);
            var cpu = EnvironmentHelper.GetCpuUsage();
            var mem = EnvironmentHelper.GetMemoryUsage();

            var url = $"/spider/register";

            var instance = new ServiceInstance
            {
                ServiceId = _serviceId,
                ServiceName = _serviceName,
                InternalIp = EnvironmentHelper.InternalIp,
                AppFolder = EnvironmentHelper.AppFolder,
                CpuUsage = cpu,
                MemoryUsageMb = mem,
                LogContent = "Heart beat"
            };

            // 内容可以为空，或者发送一条系统预定义的上报信息
            var content = new StringContent(JsonSerializer.Serialize(instance), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(url, content);
        });
    }
}
