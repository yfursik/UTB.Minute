using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Endpoints;

public static class FoodEndpoints
{
    public static void MapFoodEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/foods")
            .WithTags("Foods")
            .RequireAuthorization(pb => pb.RequireRole("Admin"));

        group.MapGet("/", async (MinuteDbContext db) =>
            TypedResults.Ok(await db.Foods.Select(f => new FoodDto(f.Id, f.Name, f.Description, f.Price, f.IsActive)).ToListAsync()));

        group.MapPost("/", async (CreateFoodDto dto, MinuteDbContext db) =>
        {
            var food = new Food { Name = dto.Name, Description = dto.Description, Price = dto.Price };
            db.Foods.Add(food);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/foods/{food.Id}", new FoodDto(food.Id, food.Name, food.Description, food.Price, food.IsActive));
        })  ;

        group.MapPut("/{id}", (Func<HttpContext, Task<IResult>>)(async (HttpContext context) =>
        {
            var id = int.Parse(context.Request.RouteValues["id"]!.ToString()!);
            var dto = await context.Request.ReadFromJsonAsync<UpdateFoodDto>();
            if (dto is null) return TypedResults.BadRequest();
            var db = context.RequestServices.GetRequiredService<MinuteDbContext>();
            var food = await db.Foods.FindAsync(id);
            if (food is null) return TypedResults.NotFound();
            food.Name = dto.Name;
            food.Description = dto.Description;
            food.Price = dto.Price;
            food.IsActive = dto.IsActive;
            await db.SaveChangesAsync();
            return TypedResults.Ok(new FoodDto(food.Id, food.Name, food.Description, food.Price, food.IsActive));
        }));
    }
}
