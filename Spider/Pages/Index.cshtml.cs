using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Spider.Models;
using System.Collections.Concurrent;

namespace Spider.Pages;

public class IndexModel : PageModel
{
    private readonly ConcurrentDictionary<string, ServiceInstance> _registry;

    public List<ServiceInstance> ServicesList { get; set; } = new();

    public IndexModel(ConcurrentDictionary<string, ServiceInstance> registry)
    {
        _registry = registry;
    }

    public void OnGet()
    {
        // 按照最后活跃时间倒序排列展示
        ServicesList = _registry.Values.OrderByDescending(s => s.LastSeen).ToList();
    }
}
