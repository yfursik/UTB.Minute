using UTB.Minute.WebApi.Services;

namespace UTB.Minute.WebApi.Endpoints;

public static class SseEndpoints
{
    public static void MapSseEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/sse/orders", (OrderNotificationService service, CancellationToken requestAborted) =>
            TypedResults.ServerSentEvents(
                service.GetEvents(requestAborted),
                eventType: "order"))
            .AllowAnonymous();
    }
}
