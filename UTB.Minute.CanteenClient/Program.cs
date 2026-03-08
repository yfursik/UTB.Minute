using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UTB.Minute.CanteenClient;
using UTB.Minute.CanteenClient.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor()
    .AddTransient<AuthorizationHandler>();

var webApiUrl = builder.Configuration["services:webapi:http:0"] ?? builder.Configuration["services:webapi:https:0"];
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(webApiUrl!);
}).AddHttpMessageHandler<AuthorizationHandler>();

builder.Services.AddHttpClient("api-sse", client =>
{
    client.BaseAddress = new Uri(webApiUrl!);
    client.Timeout = Timeout.InfiniteTimeSpan;
});

var oidcScheme = OpenIdConnectDefaults.AuthenticationScheme;
builder.Services.AddAuthentication(oidcScheme)
    .AddKeycloakOpenIdConnect("keycloak", realm: "minute", oidcScheme, options =>
    {
        options.ClientId = "minute-blazor";
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.Scope.Add("minute:all");
        options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Name;
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
        options.SaveTokens = true;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
            options.Authority = "http://localhost:8080/realms/minute";
            options.MetadataAddress = "http://localhost:8080/realms/minute/.well-known/openid-configuration";
        }
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("CanteenApp", policy => policy.RequireRole("Student", "Cook"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapLoginAndLogout();

app.Run();