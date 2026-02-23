using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db;

var builder = WebApplication.CreateBuilder(args);

// Configure DbContext to use SQL Server
builder.Services.AddDbContext<MinuteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("minutedb")));

var app = builder.Build();

// HTTP Command to reset the database and seed initial data
app.MapPost("/db/reset", async (MinuteDbContext db) => 
{
    // 1. Delete old DB and create a fresh one
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();

    // 2. Seed test data
    if (!await db.Foods.AnyAsync())
    {
        db.Foods.AddRange(
            new Food { Name = "Svíčková na smetaně", Description = "Traditional Czech food", Price = 120 },
            new Food { Name = "Smažený sýr", Description = "Fried cheese with fries", Price = 105 },
            new Food { Name = "Hovězí guláš", Description = "Beef goulash", Price = 115 }
        );
        
        await db.SaveChangesAsync();
    }

    return Results.Ok("Database successfully reset and seeded with test data.");
});

app.Run();