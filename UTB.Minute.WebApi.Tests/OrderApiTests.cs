using System.Net;
using System.Net.Http.Json;
using UTB.Minute.Contracts;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

/// <summary>
/// Tests for /orders endpoints (Student and Cook roles).
/// Covers: create order, read orders, status transitions, sold-out scenario, invalid transitions.
/// </summary>
public class OrderApiTests : IClassFixture<MinuteWebAppFactory>
{
    private readonly HttpClient _adminClient;
    private readonly HttpClient _studentClient;
    private readonly HttpClient _cookClient;

    public OrderApiTests(MinuteWebAppFactory factory)
    {
        _adminClient = factory.CreateClient();
        _adminClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        _studentClient = factory.CreateClient();
        _studentClient.DefaultRequestHeaders.Add("X-Test-Role", "Student");

        _cookClient = factory.CreateClient();
        _cookClient.DefaultRequestHeaders.Add("X-Test-Role", "Cook");
    }

    /// <summary>
    /// Helper: creates a food item and adds it to today's menu.
    /// Returns the menu item ID.
    /// </summary>
    private async Task<int> CreateMenuItemAsync(string foodName = "Test Dish", int portions = 10)
    {
        var foodDto = new CreateFoodDto(foodName, $"Desc for {foodName}", 100m);
        var foodResponse = await _adminClient.PostAsJsonAsync("/foods", foodDto);
        var food = await foodResponse.Content.ReadFromJsonAsync<FoodDto>();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var menuResponse = await _adminClient.PostAsJsonAsync("/menu",
            new CreateMenuItemDto(today, food!.Id, portions));
        return await menuResponse.Content.ReadFromJsonAsync<int>();
    }

    [Fact]
    public async Task CreateOrder_ValidMenuItem_ReturnsCreated()
    {
        var menuItemId = await CreateMenuItemAsync("Pizza");

        var response = await _studentClient.PostAsJsonAsync("/orders",
            new CreateOrderDto(menuItemId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_DecreasesAvailablePortions()
    {
        var menuItemId = await CreateMenuItemAsync("Risotto", 5);

        await _studentClient.PostAsJsonAsync("/orders", new CreateOrderDto(menuItemId));

        var menuItems = await (await _adminClient.GetAsync("/menu"))
            .Content.ReadFromJsonAsync<MenuItemDto[]>();
        var item = menuItems?.FirstOrDefault(m => m.Id == menuItemId);
        Assert.NotNull(item);
        Assert.Equal(4, item.AvailablePortions);
    }

    [Fact]
    public async Task CreateOrder_SoldOut_ReturnsBadRequest()
    {
        var menuItemId = await CreateMenuItemAsync("Last Portion", 1);

        var first = await _studentClient.PostAsJsonAsync("/orders", new CreateOrderDto(menuItemId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _studentClient.PostAsJsonAsync("/orders", new CreateOrderDto(menuItemId));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_NonExistentMenuItem_ReturnsNotFound()
    {
        var response = await _studentClient.PostAsJsonAsync("/orders",
            new CreateOrderDto(99999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStudentOrders_ReturnsOwnOrders()
    {
        var menuItemId = await CreateMenuItemAsync("Sushi");
        await _studentClient.PostAsJsonAsync("/orders", new CreateOrderDto(menuItemId));

        var response = await _studentClient.GetAsync("/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<OrderDto[]>();
        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
        Assert.Contains(orders, o => o.Status == OrderStatus.Preparing);
    }

    [Fact]
    public async Task GetActiveOrders_Cook_ReturnsNonCompletedOrders()
    {
        var menuItemId = await CreateMenuItemAsync("Curry");
        await _studentClient.PostAsJsonAsync("/orders", new CreateOrderDto(menuItemId));

        var response = await _cookClient.GetAsync("/orders/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<OrderDto[]>();
        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
    }

    [Fact]
    public async Task UpdateOrderStatus_PreparingToReady_ReturnsOk()
    {
        var menuItemId = await CreateMenuItemAsync("Tacos");
        var createResponse = await _studentClient.PostAsJsonAsync("/orders",
            new CreateOrderDto(menuItemId));
        var orderId = await createResponse.Content.ReadFromJsonAsync<int>();

        var response = await _cookClient.PutAsJsonAsync($"/orders/{orderId}/status",
            new UpdateOrderStatusDto(OrderStatus.Ready));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_ReadyToCompleted_ReturnsOk()
    {
        var menuItemId = await CreateMenuItemAsync("Ramen");
        var createResponse = await _studentClient.PostAsJsonAsync("/orders",
            new CreateOrderDto(menuItemId));
        var orderId = await createResponse.Content.ReadFromJsonAsync<int>();

        await _cookClient.PutAsJsonAsync($"/orders/{orderId}/status",
            new UpdateOrderStatusDto(OrderStatus.Ready));

        var response = await _cookClient.PutAsJsonAsync($"/orders/{orderId}/status",
            new UpdateOrderStatusDto(OrderStatus.Completed));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_PreparingToCancelled_ReturnsOk()
    {
        var menuItemId = await CreateMenuItemAsync("Wok");
        var createResponse = await _studentClient.PostAsJsonAsync("/orders",
            new CreateOrderDto(menuItemId));
        var orderId = await createResponse.Content.ReadFromJsonAsync<int>();

        var response = await _cookClient.PutAsJsonAsync($"/orders/{orderId}/status",
            new UpdateOrderStatusDto(OrderStatus.Cancelled));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_InvalidTransition_ReturnsBadRequest()
    {
        var menuItemId = await CreateMenuItemAsync("Kebab");
        var createResponse = await _studentClient.PostAsJsonAsync("/orders",
            new CreateOrderDto(menuItemId));
        var orderId = await createResponse.Content.ReadFromJsonAsync<int>();

        var response = await _cookClient.PutAsJsonAsync($"/orders/{orderId}/status",
            new UpdateOrderStatusDto(OrderStatus.Completed));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_NonExistentOrder_ReturnsNotFound()
    {
        var response = await _cookClient.PutAsJsonAsync("/orders/99999/status",
            new UpdateOrderStatusDto(OrderStatus.Ready));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
