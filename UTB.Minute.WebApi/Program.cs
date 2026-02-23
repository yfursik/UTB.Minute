using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

var builder = WebApplication.CreateBuilder(args);

// 1. Connect the database (connection string is provided by Aspire)
builder.Services.AddDbContext<MinuteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("minutedb")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<UTB.Minute.Db.MinuteDbContext>();
    // Эта команда проверит базу и создаст все таблицы, если их нет
    await context.Database.EnsureCreatedAsync(); 
}

// ==========================================
// 1. FOODS
// ==========================================
var foods = app.MapGroup("/foods");

// Get all foods
foods.MapGet("/", async (MinuteDbContext db) => 
    TypedResults.Ok(await db.Foods.Select(f => new FoodDto(f.Id, f.Name, f.Description, f.Price, f.IsActive)).ToListAsync()));

// Create a new food item
foods.MapPost("/", async (CreateFoodDto dto, MinuteDbContext db) => 
{
    var food = new Food { Name = dto.Name, Description = dto.Description, Price = dto.Price };
    db.Foods.Add(food);
    await db.SaveChangesAsync();
    return TypedResults.Created($"/foods/{food.Id}", new FoodDto(food.Id, food.Name, food.Description, food.Price, food.IsActive));
});

// Update food (or deactivate by changing IsActive)
foods.MapPut("/{id}", async (int id, UpdateFoodDto dto, MinuteDbContext db) => 
{
    var food = await db.Foods.FindAsync(id);
    if (food is null) return Results.NotFound();
    
    food.Name = dto.Name; 
    food.Description = dto.Description; 
    food.Price = dto.Price; 
    food.IsActive = dto.IsActive;
    
    await db.SaveChangesAsync();
    return TypedResults.Ok(new FoodDto(food.Id, food.Name, food.Description, food.Price, food.IsActive));
});

// ==========================================
// 2. MENU ITEMS
// ==========================================
var menu = app.MapGroup("/menu");

// Get menu for all days (include related food data)
menu.MapGet("/", async (MinuteDbContext db) => 
{
    var result = await db.MenuItems.Include(m => m.Food)
        .Select(m => new MenuItemDto(m.Id, m.Date, new FoodDto(m.Food.Id, m.Food.Name, m.Food.Description, m.Food.Price, m.Food.IsActive), m.AvailablePortions))
        .ToListAsync();
    return TypedResults.Ok(result);
});

// Add a new item to the menu
menu.MapPost("/", async (CreateMenuItemDto dto, MinuteDbContext db) => 
{
    var menuItem = new MenuItem { Date = dto.Date, FoodId = dto.FoodId, AvailablePortions = dto.AvailablePortions };
    db.MenuItems.Add(menuItem);
    await db.SaveChangesAsync();
    return TypedResults.Created($"/menu/{menuItem.Id}", menuItem.Id);
});

// Update available portions
menu.MapPut("/{id}", async (int id, UpdateMenuItemDto dto, MinuteDbContext db) => 
{
    var menuItem = await db.MenuItems.FindAsync(id);
    if (menuItem is null) return Results.NotFound();
    
    menuItem.AvailablePortions = dto.AvailablePortions;
    await db.SaveChangesAsync();
    return TypedResults.Ok();
});

// Delete a menu item (allowed by requirements, unlike foods)
menu.MapDelete("/{id}", async (int id, MinuteDbContext db) => 
{
    var menuItem = await db.MenuItems.FindAsync(id);
    if (menuItem is null) return Results.NotFound();
    
    db.MenuItems.Remove(menuItem);
    await db.SaveChangesAsync();
    return TypedResults.NoContent();
});

// ==========================================
// 3. ORDERS
// ==========================================
var orders = app.MapGroup("/orders");

// Get all active (not completed) orders for the cook
orders.MapGet("/active", async (MinuteDbContext db) => 
{
    var result = await db.Orders.Include(o => o.MenuItem).ThenInclude(m => m.Food)
        .Where(o => o.Status != OrderStatus.Completed)
        .Select(o => new OrderDto(o.Id, 
            new MenuItemDto(o.MenuItem.Id, o.MenuItem.Date, new FoodDto(o.MenuItem.Food.Id, o.MenuItem.Food.Name, o.MenuItem.Food.Description, o.MenuItem.Food.Price, o.MenuItem.Food.IsActive), o.MenuItem.AvailablePortions), 
            o.Status, o.CreatedAt))
        .ToListAsync();
    return TypedResults.Ok(result);
});

// Student places an order (concurrency logic is here!)
orders.MapPost("/", async (CreateOrderDto dto, MinuteDbContext db) => 
{
    var menuItem = await db.MenuItems.FindAsync(dto.MenuItemId);
    if (menuItem is null) return Results.NotFound("Menu item not found");
    if (menuItem.AvailablePortions <= 0) return Results.BadRequest("Sold out!");

    // Decrease available portions
    menuItem.AvailablePortions--;
    
    // Hardcode student ID for now, before Keycloak integration
    var order = new Order { MenuItemId = menuItem.Id, Status = OrderStatus.Preparing, StudentId = "student_123" }; 
    db.Orders.Add(order);

    try 
    {
        await db.SaveChangesAsync();
        return TypedResults.Created($"/orders/{order.Id}", order.Id);
    }
    catch (DbUpdateConcurrencyException) 
    {
        // If two users order the last portion at the exact same time, the DB will reject the second one
        return Results.Conflict("Someone beat you to it and ordered the last portion!");
    }
});

// Cook changes the order status
orders.MapPut("/{id}/status", async (int id, UpdateOrderStatusDto dto, MinuteDbContext db) => 
{
    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound();

    // Check valid transitions according to requirements
    bool isValidTransition = (order.Status, dto.Status) switch 
    {
        (OrderStatus.Preparing, OrderStatus.Ready) => true,
        (OrderStatus.Preparing, OrderStatus.Cancelled) => true,
        (OrderStatus.Ready, OrderStatus.Completed) => true,
        (OrderStatus.Cancelled, OrderStatus.Completed) => true,
        _ => false
    };

    if (!isValidTransition) 
        return Results.BadRequest($"Cannot transition from {order.Status} to {dto.Status}");

    order.Status = dto.Status;
    await db.SaveChangesAsync();
    return TypedResults.Ok();
});

app.Run();

public partial class Program { }