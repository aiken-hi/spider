using Spider.Models;
using System.Collections.Concurrent;

namespace Spider.Services;

public class MonitorWorker : BackgroundService
{
    private readonly ConcurrentDictionary<string, ServiceInstance> _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly AlertService _alertService;

    // 超时重试计数器 (ServiceId -> 连续超时次数)
    private readonly ConcurrentDictionary<string, int> _retryTracker = new();

    public MonitorWorker(ConcurrentDictionary<string, ServiceInstance> registry, IServiceProvider serviceProvider, AlertService alertService)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _alertService = alertService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SpiderDbContext>();

            foreach (var kvp in _registry)
            {
                var service = kvp.Value;
                var idleTime = now - service.LastSeen;

                // 1. 自动化僵尸实例清理：超过 24 小时无任何动静，执行物理抹除
                if (idleTime > TimeSpan.FromHours(24))
                {
                    _registry.TryRemove(kvp.Key, out _);
                    var dbEntity = await dbContext.Services.FindAsync(kvp.Key);
                    if (dbEntity != null) dbContext.Services.Remove(dbEntity);
                    await dbContext.SaveChangesAsync();
                    continue;
                }

                // 2. 心跳抖动缓冲（防误报）：超过 30 秒没动静，进行疑似标记，不立即报警
                if (idleTime > TimeSpan.FromSeconds(30))
                {
                    _retryTracker.AddOrUpdate(service.ServiceId, 1, (_, count) => count + 1);

                    // 只有连续 3 次扫描（共约 15 秒）都超时，才正式确诊掉线，触发报警
                    if (_retryTracker[service.ServiceId] >= 3 && service.Status != ServiceStatus.Offline)
                    {
                        service.Status = ServiceStatus.Offline;
                        dbContext.Services.Update(service);
                        await dbContext.SaveChangesAsync();
                        await _alertService.SendAsync(service, "服务心跳连续多次重试失败，判定已掉线！", isCritical: true);
                    }
                }
                else
                {
                    // 恢复正常，重试计数清零
                    _retryTracker[service.ServiceId] = 0;
                }
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}
