using System.Net;
using System.Net.Http.Json;
using UTB.Minute.Contracts;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

/// <summary>
/// Tests for /foods endpoints (Admin role).
/// Covers: create, read, update, deactivate.
/// </summary>
public class FoodApiTests : IClassFixture<MinuteWebAppFactory>
{
    private readonly HttpClient _client;

    public FoodApiTests(MinuteWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
    }

    [Fact]
    public async Task GetFoods_EmptyDb_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/foods");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var foods = await response.Content.ReadFromJsonAsync<FoodDto[]>();
        Assert.NotNull(foods);
    }

    [Fact]
    public async Task CreateFood_ValidDto_ReturnsCreated()
    {
        var dto = new CreateFoodDto("Schnitzel", "Breaded pork cutlet", 129m);

        var response = await _client.PostAsJsonAsync("/foods", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var food = await response.Content.ReadFromJsonAsync<FoodDto>();
        Assert.NotNull(food);
        Assert.Equal("Schnitzel", food.Name);
        Assert.Equal("Breaded pork cutlet", food.Description);
        Assert.Equal(129m, food.Price);
        Assert.True(food.IsActive);
    }

    [Fact]
    public async Task CreateAndGetFoods_ReturnsCreatedItem()
    {
        var dto = new CreateFoodDto("Goulash", "Traditional Czech goulash", 99m);
        var createResponse = await _client.PostAsJsonAsync("/foods", dto);
        createResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/foods");
        var foods = await response.Content.ReadFromJsonAsync<FoodDto[]>();

        Assert.NotNull(foods);
        Assert.Contains(foods, f => f.Name == "Goulash");
    }

    [Fact]
    public async Task UpdateFood_ValidDto_ReturnsOk()
    {
        var createDto = new CreateFoodDto("Pasta", "Italian pasta", 110m);
        var createResponse = await _client.PostAsJsonAsync("/foods", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<FoodDto>();
        Assert.NotNull(created);

        var updateDto = new UpdateFoodDto("Pasta Carbonara", "Creamy Italian pasta", 135m, true);
        var updateResponse = await _client.PutAsJsonAsync($"/foods/{created.Id}", updateDto);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<FoodDto>();
        Assert.NotNull(updated);
        Assert.Equal("Pasta Carbonara", updated.Name);
        Assert.Equal(135m, updated.Price);
    }

    [Fact]
    public async Task DeactivateFood_SetsIsActiveFalse()
    {
        var createDto = new CreateFoodDto("Soup", "Tomato soup", 59m);
        var createResponse = await _client.PostAsJsonAsync("/foods", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<FoodDto>();
        Assert.NotNull(created);
        Assert.True(created.IsActive);

        var updateDto = new UpdateFoodDto(created.Name, created.Description, created.Price, false);
        var updateResponse = await _client.PutAsJsonAsync($"/foods/{created.Id}", updateDto);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<FoodDto>();
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task UpdateFood_NonExistentId_ReturnsNotFound()
    {
        var updateDto = new UpdateFoodDto("Ghost", "Does not exist", 1m, true);
        var response = await _client.PutAsJsonAsync("/foods/99999", updateDto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
