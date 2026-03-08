using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MinuteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("minutedb")));

var app = builder.Build();

// Reset database (delete + create). WebApi seeds data on its startup.
app.MapPost("/db/reset", async (MinuteDbContext db) =>
{
    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();
    return Results.Ok("Database reset. Restart WebApi to run seed.");
});

app.Run();