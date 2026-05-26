using Spider.Models;
using System.Collections.Concurrent;

namespace Spider.Services;

public class AlertService
{
    private readonly ILogger<AlertService> _logger;
    private readonly HttpClient _httpClient;

    // 告警收敛缓存：Key -> ServiceId, Value -> 上次报警时间
    private static readonly ConcurrentDictionary<string, DateTime> _alertCache = new();
    private const string WebhookUrl = "https://spotecnet.webhook.office.com/webhookb2/5c5e6d7b-aa5f-4ade-9dbd-56eb566e32da@0289995a-e7fd-44d6-80f0-532e69848858/IncomingWebhook/24e6dc5c63ed42269c66542c2e9910dc/30d1a412-861f-4ab9-8a0d-1f838150ddbe/V2qwOwO67NwRfvHDcTih-hdayT3dTsRSU53WtvG2oNmgs1"; // 替换为你真实的飞书/钉钉群机器人地址

    public AlertService(ILogger<AlertService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task SendAsync(ServiceInstance service, string reason, bool isCritical = false)
    {
        var cacheKey = $"{service.ServiceId}:{reason}";
        var now = DateTime.Now;

        // 如果不是阻断级故障，且5分钟内发送过相同报警，则启动收敛策略（拦截外发）
        if (!isCritical && _alertCache.TryGetValue(cacheKey, out var lastSentTime))
        {
            if (now - lastSentTime < TimeSpan.FromMinutes(5))
            {
                _logger.LogInformation("[告警收敛] 服务 {Name} 在 5 分钟内重复触发 [{Reason}]，已拦截外发通知。", service.ServiceName, reason);
                return;
            }
        }

        // 更新或写入报警时间存根
        _alertCache[cacheKey] = now;

        var message = $"[Spider 🚨 告警]\n" +
                      $"========================\n" +
                      $"服务名称: {service.ServiceName}\n" +
                      $"实例标识: {service.ServiceId}\n" +
                      $"内网物理IP: {service.InternalIp}\n" +
                      $"运行路径: {service.AppFolder}\n" +
                      $"指标状态: CPU: {service.CpuUsage}%, 内存: {service.MemoryUsageMb}MB\n" +
                      $"触发原因: {reason}\n" +
                      $"通知时间: {now:yyyy-MM-dd HH:mm:ss}";

        _logger.LogError(message);

        // 异步外发飞书/钉钉群机器人（以飞书标准格式为例）
        try
        {
            var payload = new { msg_type = "text", content = new { text = message } };
            await _httpClient.PostAsJsonAsync(WebhookUrl, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("群机器人 Webhook 消息发送失败: {Msg}", ex.Message);
        }
    }
}
