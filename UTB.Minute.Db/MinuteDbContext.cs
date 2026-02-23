using Microsoft.EntityFrameworkCore;

namespace UTB.Minute.Db;

public class MinuteDbContext(DbContextOptions<MinuteDbContext> options) : DbContext(options)
{
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();
}