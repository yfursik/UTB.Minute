using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTB.Minute.Db;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

public class FoodApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FoodApiTests(WebApplicationFactory<Program> factory)
    {
        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 1. Сносим настройки базы данных
                services.RemoveAll(typeof(DbContextOptions<MinuteDbContext>));
                services.RemoveAll(typeof(DbContextOptions));

                // 2. Создаем изолированную микро-среду только для InMemory
                var efServiceProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                // 3. Добавляем базу и строго привязываем её к этой изолированной среде
                services.AddDbContext<MinuteDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_For_Foods")
                        .UseInternalServiceProvider(efServiceProvider);
                });
            });
        });

        _client = customizedFactory.CreateClient();
    }

    [Fact]
    public async Task GetFoods_ReturnsOkStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/foods");

        // Assert
        response.EnsureSuccessStatusCode(); 
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}