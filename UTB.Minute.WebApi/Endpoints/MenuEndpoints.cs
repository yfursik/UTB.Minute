using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Endpoints;

public static class MenuEndpoints
{
    public static void MapMenuEndpoints(this IEndpointRouteBuilder routes)
    {
        var menuAdmin = routes.MapGroup("/menu")
            .WithTags("Menu")
            .RequireAuthorization(pb => pb.RequireRole("Admin"));

        var menuToday = routes.MapGroup("/menu").WithTags("Menu");

        menuAdmin.MapGet("/", async (MinuteDbContext db) =>
        {
            var result = await db.MenuItems.Include(m => m.Food)
                .Select(m => new MenuItemDto(m.Id, m.Date, new FoodDto(m.Food.Id, m.Food.Name, m.Food.Description, m.Food.Price, m.Food.IsActive), m.AvailablePortions))
                .ToListAsync();
            return TypedResults.Ok(result);
        });

        menuToday.MapGet("/today", async (MinuteDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var result = await db.MenuItems.Include(m => m.Food)
                .Where(m => m.Date == today && m.Food.IsActive)
                .Select(m => new MenuItemDto(m.Id, m.Date, new FoodDto(m.Food.Id, m.Food.Name, m.Food.Description, m.Food.Price, m.Food.IsActive), m.AvailablePortions))
                .ToListAsync();
            return TypedResults.Ok(result);
        })
            .RequireAuthorization(pb => pb.RequireRole("Student"));

        menuAdmin.MapPost("/", async (CreateMenuItemDto dto, MinuteDbContext db) =>
        {
            var menuItem = new MenuItem { Date = dto.Date, FoodId = dto.FoodId, AvailablePortions = dto.AvailablePortions };
            db.MenuItems.Add(menuItem);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/menu/{menuItem.Id}", menuItem.Id);
        });

        menuAdmin.MapPut("/{id}", (Func<HttpContext, Task<IResult>>)(async (HttpContext context) =>
        {
            var id = int.Parse(context.Request.RouteValues["id"]!.ToString()!);
            var dto = await context.Request.ReadFromJsonAsync<UpdateMenuItemDto>();
            if (dto is null) return TypedResults.BadRequest();
            var db = context.RequestServices.GetRequiredService<MinuteDbContext>();
            var menuItem = await db.MenuItems.FindAsync(id);
            if (menuItem is null) return TypedResults.NotFound();
            menuItem.Date = dto.Date;
            menuItem.AvailablePortions = dto.AvailablePortions;
            await db.SaveChangesAsync();
            return TypedResults.Ok();
        }));

        menuAdmin.MapPost("/copy", (Func<HttpContext, Task<IResult>>)(async (HttpContext context) =>
        {
            var dto = await context.Request.ReadFromJsonAsync<CopyMenuDto>();
            if (dto is null) return TypedResults.BadRequest();
            var db = context.RequestServices.GetRequiredService<MinuteDbContext>();
            var sourceItems = await db.MenuItems.Include(m => m.Food)
                .Where(m => m.Date == dto.FromDate)
                .ToListAsync();
            if (sourceItems.Count == 0)
                return TypedResults.BadRequest("No menu items found for the source date.");
            var existingFoodIdsForTarget = await db.MenuItems
                .Where(m => m.Date == dto.ToDate)
                .Select(m => m.FoodId)
                .ToHashSetAsync();
            foreach (var src in sourceItems)
            {
                if (existingFoodIdsForTarget.Contains(src.FoodId))
                    continue;
                db.MenuItems.Add(new MenuItem
                {
                    Date = dto.ToDate,
                    FoodId = src.FoodId,
                    AvailablePortions = src.AvailablePortions
                });
                existingFoodIdsForTarget.Add(src.FoodId);
            }
            await db.SaveChangesAsync();
            return TypedResults.Ok();
        }));

        menuAdmin.MapDelete("/{id}", (Func<HttpContext, Task<IResult>>)(async (HttpContext context) =>
        {
            var id = int.Parse(context.Request.RouteValues["id"]!.ToString()!);
            var db = context.RequestServices.GetRequiredService<MinuteDbContext>();
            var menuItem = await db.MenuItems.FindAsync(id);
            if (menuItem is null) return TypedResults.NotFound();
            db.MenuItems.Remove(menuItem);
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        }));
    }
}
