using Microsoft.EntityFrameworkCore;

namespace UTB.Minute.Db;

/// <summary>
/// Seeds the database with initial data. Idempotent: only adds data when tables are empty.
/// </summary>
public static class MinuteDbSeed
{
    public static async Task SeedAsync(MinuteDbContext db)
    {
        await SeedFoodsAsync(db).ConfigureAwait(false);
        await SeedMenuItemsAsync(db).ConfigureAwait(false);
    }

    private static async Task SeedFoodsAsync(MinuteDbContext db)
    {
        if (await db.Foods.AnyAsync().ConfigureAwait(false))
            return;

        var foods = new[]
        {
            new Food { Name = "Minestrone", Description = "Italská zeleninová polévka s fazolemi a těstovinami, podávaná s pečivem", Price = 89m, IsActive = true },
            new Food { Name = "Bruschetta", Description = "Opečený chléb s rajčaty, bazalkou, česnekem a olivovým olejem", Price = 95m, IsActive = true },
            new Food { Name = "Spaghetti carbonara", Description = "Špagety se smetanovou omáčkou, pancettou, vejcem a parmazánem", Price = 165m, IsActive = true },
            new Food { Name = "Pizza Margherita", Description = "Těstovinová pizza s rajčatovou omáčkou, mozzarellou a bazalkou", Price = 149m, IsActive = true },
            new Food { Name = "Tiramisu", Description = "Klasický italský dezert z mascarpone, kávy a kakaa", Price = 99m, IsActive = true },
            new Food { Name = "Espresso", Description = "Italská káva 30 ml", Price = 45m, IsActive = true },
        };

        db.Foods.AddRange(foods);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedMenuItemsAsync(MinuteDbContext db)
    {
        if (await db.MenuItems.AnyAsync().ConfigureAwait(false))
            return;

        var foodIds = await db.Foods.OrderBy(f => f.Id).Select(f => f.Id).ToListAsync().ConfigureAwait(false);
        if (foodIds.Count == 0)
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var menuItems = foodIds
            .Select(foodId => new MenuItem { Date = today, FoodId = foodId, AvailablePortions = 50 })
            .ToList();

        db.MenuItems.AddRange(menuItems);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
