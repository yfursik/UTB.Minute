using Microsoft.EntityFrameworkCore;

namespace UTB.Minute.Db;

public class MinuteDbContext(DbContextOptions<MinuteDbContext> options) : DbContext(options)
{
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Food>(e =>
        {
            e.Property(x => x.Price).HasPrecision(18, 2);
        });
    }
}