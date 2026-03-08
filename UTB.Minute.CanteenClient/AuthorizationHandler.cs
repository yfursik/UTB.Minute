using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace UTB.Minute.CanteenClient;

/// <summary>
/// Adds the current user's access token to outgoing API requests when using OIDC with SaveTokens.
/// </summary>
public sealed class AuthorizationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available from the IHttpContextAccessor.");

        var accessToken = await httpContext.GetTokenAsync("access_token").ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
