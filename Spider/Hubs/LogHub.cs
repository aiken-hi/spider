using Microsoft.AspNetCore.SignalR;
using Spider.Models;
using System.Collections.Concurrent;

namespace Spider.Hubs;

public class LogHub : Hub
{
    private readonly ConcurrentDictionary<string, ServiceInstance> _registry;

    public LogHub(ConcurrentDictionary<string, ServiceInstance> registry)
    {
        _registry = registry;
    }

    // 前端订阅指定服务的日志流
    public async Task JoinServiceGroup(string serviceId)
    {
        // 1. 将当前客户端连接加入独立的隔离组
        await Groups.AddToGroupAsync(Context.ConnectionId, serviceId);

        // 2. 推送内存中缓存的历史日志，让用户打开弹窗时能看到之前的内容
        if (_registry.TryGetValue(serviceId, out var service))
        {
            var historyLogs = service.RecentLogs.ToArray();
            foreach (var log in historyLogs)
            {
                await Clients.Caller.SendAsync("ReceiveLog", log);
            }
        }
    }

    // 前端关闭弹窗时，离开该组
    public async Task LeaveServiceGroup(string serviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, serviceId);
    }

    // ====== 新增：客户端节点注册专属控制组 ======
    public async Task RegisterClientNode(string serviceId)
    {
        // 客户端自身加入一个专门接收控制指令的组，命名为 control_serviceId
        await Groups.AddToGroupAsync(Context.ConnectionId, $"control_{serviceId}");
    }

    // ====== 新增：前端大屏发送控制命令 ======
    public async Task SendControlCommand(string serviceId, string command)
    {
        // 将命令（如 "RESTART"）精准广播给该服务部署的客户端
        await Clients.Group($"control_{serviceId}").SendAsync("ReceiveCommand", command);
    }
}
