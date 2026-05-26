### 一、部署Spider

使用iis或者nginx或者其他web服务器部署Spider，确保它能够正常访问。

### 二、安装Serilog.Sinks.Spider

在你的项目中安装Serilog.Sinks.Spider包，可以通过NuGet Package Manager或者命令行安装：

### 方式一, 在代码中直接配置Serilog：

```
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information() // 设置基础过滤阈值  
        .WriteTo.Console()          // 在本地控制台打印
        .WriteTo.Spider(            // 注入到 Spider 注册中心
            spiderUrl: "http://server:80", 
            serviceId: "server-1", 
            serviceName: "myapi"
        )
        .CreateLogger();
```

### 方式二，在appsettings.json中配置Serilog：

```
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.Spider" ],
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Spider",
        "Args": {
          "spiderUrl": "http://local:5000",
          "serviceId": "server-1",
          "serviceName": "myapi"
        }
      }
    ]
  }
}
```

此时你只需要在代码中调用 
```
Log.Logger = new LoggerConfiguration()
    .ReadFrom
    .Configuration(builder.Configuration)
    .CreateLogger();
``` 
就可以完成 Serilog 的配置了。无论是 Console 还是 Spider Sink 都会被正确加载并使用。