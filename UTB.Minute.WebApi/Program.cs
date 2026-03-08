using Microsoft.EntityFrameworkCore;
using UTB.Minute.WebApi.Endpoints;
using UTB.Minute.WebApi.Services;
using UTB.Minute.Db;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MinuteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("minutedb")));

builder.Services.AddSingleton<OrderNotificationService>();

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "minute",
        configureOptions: options =>
        {
            // Postman uses aud "minute.api", Blazor uses "minute-blazor"
            options.TokenValidationParameters.ValidAudiences = ["minute.api", "minute-blazor"];
            options.TokenValidationParameters.ValidIssuers = [
                "http://localhost:8080/realms/minute",
                "http://keycloak:8080/realms/minute"
            ];
            if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
                options.Authority = "http://localhost:8080/realms/minute";
                options.MetadataAddress = "http://localhost:8080/realms/minute/.well-known/openid-configuration";
            }
        });

builder.Services.AddAuthorizationBuilder();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MinuteDbContext>();
    await context.Database.EnsureCreatedAsync();
    await MinuteDbSeed.SeedAsync(context);
}

app.MapFoodEndpoints();
app.MapMenuEndpoints();
app.MapOrderEndpoints();
app.MapSseEndpoints();

app.Run();

public partial class Program { }
