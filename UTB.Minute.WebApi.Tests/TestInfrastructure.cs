using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Tests;

/// <summary>
/// Fake authentication handler that automatically authenticates requests
/// with configurable roles via the X-Test-Role header.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var roleHeader = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "Admin";
        var roles = roleHeader.Split(',', StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.NameIdentifier, "testuser"),
            new("preferred_username", "testuser"),
            new("sub", "testuser"),
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Shared WebApplicationFactory that replaces SQL Server with InMemory DB
/// and Keycloak auth with a fake test authentication handler.
/// Each test class gets its own isolated database via unique DB names.
/// </summary>
public class MinuteWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove every EF Core and SqlServer related service descriptor
            var toRemove = services.Where(d =>
            {
                var st = d.ServiceType.FullName ?? "";
                var it = d.ImplementationType?.FullName ?? "";
                return st.Contains("DbContext") || st.Contains("SqlServer")
                    || it.Contains("SqlServer")
                    || st.Contains("EntityFrameworkCore");
            }).ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Re-add MinuteDbContext with InMemory provider
            services.AddDbContext<MinuteDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace authentication with fake test scheme
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
}
