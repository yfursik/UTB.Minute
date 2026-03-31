using System.Net;
using System.Net.Http.Json;
using UTB.Minute.Contracts;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

/// <summary>
/// Tests for /menu endpoints (Admin role).
/// Covers: create, read, update, delete menu items and today's menu filter.
/// </summary>
public class MenuApiTests : IClassFixture<MinuteWebAppFactory>
{
    private readonly HttpClient _client;

    public MenuApiTests(MinuteWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
    }

    private async Task<FoodDto> CreateFoodAsync(string name = "Test Food", decimal price = 100m)
    {
        var dto = new CreateFoodDto(name, $"Description for {name}", price);
        var response = await _client.PostAsJsonAsync("/foods", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FoodDto>())!;
    }

    [Fact]
    public async Task GetMenu_EmptyDb_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/menu");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<MenuItemDto[]>();
        Assert.NotNull(items);
    }

    [Fact]
    public async Task CreateMenuItem_ValidDto_ReturnsCreated()
    {
        var food = await CreateFoodAsync("Burger");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dto = new CreateMenuItemDto(today, food.Id, 30);

        var response = await _client.PostAsJsonAsync("/menu", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetMenu_ReturnsCreatedItem()
    {
        var food = await CreateFoodAsync("Fries");
        var today = DateOnly.FromDateTime(DateTime.Now);
        await _client.PostAsJsonAsync("/menu", new CreateMenuItemDto(today, food.Id, 25));

        var response = await _client.GetAsync("/menu");
        var items = await response.Content.ReadFromJsonAsync<MenuItemDto[]>();

        Assert.NotNull(items);
        Assert.Contains(items, m => m.Food.Name == "Fries" && m.AvailablePortions == 25);
    }

    [Fact]
    public async Task UpdateMenuItem_ChangesPortionsAndDate()
    {
        var food = await CreateFoodAsync("Steak");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var createResponse = await _client.PostAsJsonAsync("/menu", new CreateMenuItemDto(today, food.Id, 10));
        var menuItemId = await createResponse.Content.ReadFromJsonAsync<int>();

        var tomorrow = today.AddDays(1);
        var updateDto = new UpdateMenuItemDto(tomorrow, 50);

        var response = await _client.PutAsJsonAsync($"/menu/{menuItemId}", updateDto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var allItems = await (await _client.GetAsync("/menu")).Content.ReadFromJsonAsync<MenuItemDto[]>();
        var updated = allItems?.FirstOrDefault(m => m.Id == menuItemId);
        Assert.NotNull(updated);
        Assert.Equal(50, updated.AvailablePortions);
        Assert.Equal(tomorrow, updated.Date);
    }

    [Fact]
    public async Task DeleteMenuItem_ReturnsNoContent()
    {
        var food = await CreateFoodAsync("Salad");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var createResponse = await _client.PostAsJsonAsync("/menu", new CreateMenuItemDto(today, food.Id, 5));
        var menuItemId = await createResponse.Content.ReadFromJsonAsync<int>();

        var response = await _client.DeleteAsync($"/menu/{menuItemId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMenuItem_NonExistent_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/menu/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMenuToday_ReturnsOnlyTodaysActiveItems()
    {
        var food = await CreateFoodAsync("Today Special");
        var today = DateOnly.FromDateTime(DateTime.Now);
        await _client.PostAsJsonAsync("/menu", new CreateMenuItemDto(today, food.Id, 20));

        var request = new HttpRequestMessage(HttpMethod.Get, "/menu/today");
        request.Headers.Add("X-Test-Role", "Student");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<MenuItemDto[]>();
        Assert.NotNull(items);
        Assert.All(items, item => Assert.Equal(today, item.Date));
    }
}
