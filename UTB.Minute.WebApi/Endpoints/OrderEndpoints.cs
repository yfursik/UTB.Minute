using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using UTB.Minute.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        var ordersStudent = routes.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization(pb => pb.RequireRole("Student"));

        var ordersCook = routes.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization(pb => pb.RequireRole("Cook"));

        ordersStudent.MapGet("/", (Func<HttpContext, Task<IResult>>)(async (HttpContext context) =>
        {
            if (context.User.IsInRole("Admin"))
                return TypedResults.Forbid();
            var studentUsername = ResolveStudentUsername(context.User);
            if (string.IsNullOrEmpty(studentUsername))
                return TypedResults.Unauthorized();
            var db = context.RequestServices.GetRequiredService<MinuteDbContext>();
            var result = await db.Orders.Include(o => o.MenuItem).ThenInclude(m => m.Food)
                .Where(o => o.StudentUsername == studentUsername)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderDto(o.Id,
                    new MenuItemDto(o.MenuItem.Id, o.MenuItem.Date, new FoodDto(o.MenuItem.Food.Id, o.MenuItem.Food.Name, o.MenuItem.Food.Description, o.MenuItem.Food.Price, o.MenuItem.Food.IsActive), o.MenuItem.AvailablePortions),
                    o.Status, o.CreatedAt))
                .ToListAsync();
            return TypedResults.Ok(result);
        }));

        ordersCook.MapGet("/active", async (MinuteDbContext db) =>
        {
            var result = await db.Orders.Include(o => o.MenuItem).ThenInclude(m => m.Food)
                .Where(o => o.Status != OrderStatus.Completed)
                .Select(o => new OrderDto(o.Id,
                    new MenuItemDto(o.MenuItem.Id, o.MenuItem.Date, new FoodDto(o.MenuItem.Food.Id, o.MenuItem.Food.Name, o.MenuItem.Food.Description, o.MenuItem.Food.Price, o.MenuItem.Food.IsActive), o.MenuItem.AvailablePortions),
                    o.Status, o.CreatedAt))
                .ToListAsync();
            return TypedResults.Ok(result);
        });

        ordersStudent.MapPost("/", (Func<HttpContext, Task<IResult>>)(async (HttpContext context) =>
        {
            if (context.User.IsInRole("Admin"))
                return TypedResults.Forbid();
            var studentUsername = ResolveStudentUsername(context.User);
            if (string.IsNullOrEmpty(studentUsername))
                return TypedResults.Unauthorized();
            var dto = await context.Request.ReadFromJsonAsync<CreateOrderDto>();
            if (dto is null) return TypedResults.BadRequest();
            var db = context.RequestServices.GetRequiredService<MinuteDbContext>();
            var notifications = context.RequestServices.GetRequiredService<OrderNotificationService>();
            var menuItem = await db.MenuItems.FindAsync(dto.MenuItemId);
            if (menuItem is null) return TypedResults.NotFound();
            if (menuItem.AvailablePortions <= 0) return TypedResults.BadRequest("Sold out!");
            menuItem.AvailablePortions--;
            var order = new Order { MenuItemId = menuItem.Id, Status = OrderStatus.Preparing, StudentUsername = studentUsername };
            db.Orders.Add(order);
            try
            {
                await db.SaveChangesAsync();
                notifications.Notify("order-created");
                return TypedResults.Created($"/orders/{order.Id}", order.Id);
            }
            catch (DbUpdateConcurrencyException)
            {
                return TypedResults.Conflict("Someone beat you to it and ordered the last portion!");
            }
        }));

        ordersCook.MapPut("/{id}/status", (Func<HttpContext, Task<IResult>>)(async (HttpContext context) =>
        {
            var id = int.Parse(context.Request.RouteValues["id"]!.ToString()!);
            var dto = await context.Request.ReadFromJsonAsync<UpdateOrderStatusDto>();
            if (dto is null) return TypedResults.BadRequest();
            var db = context.RequestServices.GetRequiredService<MinuteDbContext>();
            var notifications = context.RequestServices.GetRequiredService<OrderNotificationService>();
            var order = await db.Orders.FindAsync(id);
            if (order is null) return TypedResults.NotFound();
            var valid = (order.Status, dto.Status) switch
            {
                (OrderStatus.Preparing, OrderStatus.Ready) => true,
                (OrderStatus.Preparing, OrderStatus.Cancelled) => true,
                (OrderStatus.Ready, OrderStatus.Completed) => true,
                (OrderStatus.Ready, OrderStatus.Cancelled) => true,
                (OrderStatus.Cancelled, OrderStatus.Completed) => true,
                _ => false
            };
            if (!valid) return TypedResults.BadRequest($"Cannot transition from {order.Status} to {dto.Status}");
            order.Status = dto.Status;
            await db.SaveChangesAsync();
            notifications.Notify("order-updated");
            return TypedResults.Ok();
        }));
    }

    /// <summary>
    /// Resolves the student username from the principal. Keycloak can send it as sub (mapped to username),
    /// preferred_username, or NameIdentifier (mapped from sub).
    /// </summary>
    private static string? ResolveStudentUsername(ClaimsPrincipal user)
    {
        return user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
    }
}
