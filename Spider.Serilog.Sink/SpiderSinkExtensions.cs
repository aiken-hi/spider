using Serilog;
using Serilog.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spider.Serilog.Sink;

public static class SpiderSinkExtensions
{
    /// <summary>
    /// 将日志管道输出重定向投递到 Spider 注册与监控中心
    /// </summary>
    /// <param name="sinkConfiguration">Serilog 基础配置对象</param>
    /// <param name="spiderUrl">Spider 监控服务端的运行地址</param>
    /// <param name="serviceId">该部署节点在注册中心中的唯一实例 ID (例: demo1)</param>
    /// <param name="serviceName">大屏看板上显示的别名 (例: 结算服务群)</param>
    /// <param name="formatProvider">格式化提供者</param>
    public static LoggerConfiguration Spider(
        this LoggerSinkConfiguration sinkConfiguration,
        string spiderUrl,
        string serviceId,
        string serviceName,
        IFormatProvider? formatProvider = null)
    {
        if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
        if (string.IsNullOrEmpty(spiderUrl)) throw new ArgumentNullException(nameof(spiderUrl));
        if (string.IsNullOrEmpty(serviceId)) throw new ArgumentNullException(nameof(serviceId));
        if (string.IsNullOrEmpty(serviceName)) throw new ArgumentNullException(nameof(serviceName));

        return sinkConfiguration.Sink(new SpiderSink(spiderUrl, serviceId, serviceName, formatProvider));
    }
}
