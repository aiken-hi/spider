using Microsoft.AspNetCore.SignalR;
using Spider.Hubs;
using Spider.Models;
using Spider.Services;
using System.Collections.Concurrent;

namespace Spider;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 显式初始化 SQLite 底层驱动，注入原生 C++ 依赖
        SQLitePCL.Batteries.Init();

        #region 注入核心基础设施
        builder.Services.AddDbContext<SpiderDbContext>();
        builder.Services.AddSingleton<ConcurrentDictionary<string, ServiceInstance>>();
        builder.Services.AddSingleton<AlertService>();
        builder.Services.AddHostedService<MonitorWorker>();
        builder.Services.AddRazorPages();
        builder.Services.AddSignalR();
        #endregion

        var app = builder.Build();

        #region 启动时自动建立 SQLite 本地数据库与表结构
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpiderDbContext>();
            db.Database.EnsureCreated();

            // 把上一次保留在本地数据库的实体，重新灌回高并发内存缓存中，防止重启失忆
            var registry = scope.ServiceProvider.GetRequiredService<ConcurrentDictionary<string, ServiceInstance>>();
            foreach (var service in db.Services)
            {
                service.Status = ServiceStatus.Offline; // 重启时默认为离线，等待心跳点亮
                registry[service.ServiceId] = service;
            }
        }
        #endregion

        app.UseStaticFiles();
        app.UseRouting();
        app.MapRazorPages();
        app.MapHub<LogHub>("/hub/logs");

        #region API 路由配置
        // 主动注册与心跳接口
        app.MapPost("/spider/register", (ServiceInstance instance, ConcurrentDictionary<string, ServiceInstance> registry) =>
        {
            instance.LastSeen = DateTime.Now;
            instance.Status = ServiceStatus.Online;
            instance.IsLogDiscovery = false;

            registry.AddOrUpdate(instance.ServiceId, instance, (_, existing) =>
            {
                existing.ServiceName = instance.ServiceName;
                existing.LastSeen = DateTime.Now;
                existing.Status = ServiceStatus.Online;
                return existing;
            });
            return Results.Ok($"服务 [{instance.ServiceName}] 主动注册/心跳更新成功。");
        });

        // 日志无感知监控注册接口
        app.MapPost("/spider/ingest-logs", async (
            ServiceInstance instance,
            ConcurrentDictionary<string, ServiceInstance> registry,
            AlertService alertService,
            SpiderDbContext db,
            IHubContext<LogHub> hubContext) =>
        {
            // 判断日志中是否包含致命错误关键词
            var isError = instance.LogContent.Contains("Critical", StringComparison.OrdinalIgnoreCase) ||
                          instance.LogContent.Contains("Exception", StringComparison.OrdinalIgnoreCase);
            var targetStatus = isError ? ServiceStatus.Fault : ServiceStatus.Online;
            var formattedLog = $"[{DateTime.Now:HH:mm:ss}] {instance.LogContent}";

            var service = registry.AddOrUpdate(instance.ServiceId,
        _ =>
        {
            var ins = new ServiceInstance { ServiceId = instance.ServiceId, ServiceName = instance.ServiceName, InternalIp = instance.InternalIp, AppFolder = instance.AppFolder, CpuUsage = instance.CpuUsage, MemoryUsageMb = instance.MemoryUsageMb, LastSeen = DateTime.Now, Status = targetStatus, IsLogDiscovery = true };
            ins.RecentLogs.Enqueue(formattedLog);
            return ins;
        },
        (_, existing) =>
        {
            existing.LastSeen = DateTime.Now;
            existing.Status = targetStatus;
            existing.CpuUsage = instance.CpuUsage;
            existing.MemoryUsageMb = instance.MemoryUsageMb;
            // 维持最近 50 条日志的内存缓存，避免爆内存
            existing.RecentLogs.Enqueue(formattedLog);
            while (existing.RecentLogs.Count > 50) existing.RecentLogs.TryDequeue(out var result);
            return existing;
        });

            // 2. 【核心修复】数据库持久化解耦，避免并发引发引用篡改
            try
            {
                // 先去数据库查一查有没有这一行记录（通过纯净管道，不污染内存中正在高频使用的 service 对象）
                var dbService = await db.Services.FindAsync(instance.ServiceId);
                if (dbService == null)
                {
                    // 如果是全新服务，深度克隆或新建一个实体丢给 EF Core
                    dbService = new ServiceInstance
                    {
                        ServiceId = instance.ServiceId,
                        ServiceName = instance.ServiceName,
                        InternalIp = instance.InternalIp,
                        AppFolder = instance.AppFolder,
                        CpuUsage = instance.CpuUsage,
                        MemoryUsageMb = instance.MemoryUsageMb,
                        LastSeen = DateTime.Now,
                        Status = targetStatus,
                        IsLogDiscovery = true
                    };
                    db.Services.Add(dbService);
                }
                else
                {
                    // 仅仅对查出来的独立局部实体字段进行赋值覆盖，隔绝高并发干扰
                    dbService.ServiceName = instance.ServiceName;
                    dbService.InternalIp = instance.InternalIp;
                    dbService.AppFolder = instance.AppFolder;
                    dbService.CpuUsage = instance.CpuUsage;
                    dbService.MemoryUsageMb = instance.MemoryUsageMb;
                    dbService.LastSeen = DateTime.Now;
                    dbService.Status = targetStatus;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // 数据库由于瞬时网络或 I/O 阻塞写入失败时，抓取异常
                // 绝不影响内存队列以及 SignalR 实时日志推流，保障大屏依然丝滑可用
                Console.WriteLine($"[DB Persistence Warning] 数据库高频同步略过一次: {ex.Message}");
            }

            // 3. 异步推送 SignalR 给前端弹窗控制台
            await hubContext.Clients.Group(instance.ServiceId).SendAsync("ReceiveLog", formattedLog);

            if (targetStatus == ServiceStatus.Fault)
            {
                await alertService.SendAsync(service, "日志管道捕获到阻断级异常关键词");
            }

            return Results.Accepted();
        });
        #endregion

        app.Run();
    }
}
