using Microsoft.EntityFrameworkCore;
using Spider.Models;

namespace Spider.Services;

public class SpiderDbContext : DbContext
{
    public DbSet<ServiceInstance> Services => Set<ServiceInstance>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=spider.db");
    }
}
