using GameNetManager.Shared.Contracts;
using GameNetManager.Server.Data;

namespace GameNetManager.Server;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this WebApplication app)
    {
        app.MapGet("/api/events", async (
            HttpContext context,
            DateTimeOffset? from,
            DateTimeOffset? to,
            EventService events,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "operations.view", cancellationToken);
            if (auth.Error is not null) return auth.Error;
            return Results.Ok(await events.ListAsync(from, to, cancellationToken));
        }).WithName("ListEvents");

        app.MapPost("/api/events", async (
            EventCreateRequest request,
            HttpContext context,
            EventService events,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "reservation.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;
            try
            {
                return Results.Ok(await events.CreateAsync(request, auth.User!.Id, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "event_conflict", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "invalid_event", message = ex.Message });
            }
        }).WithName("CreateEvent");

        app.MapPost("/api/events/{eventId:guid}/participants", async (
            Guid eventId,
            EventParticipantRequest request,
            HttpContext context,
            EventService events,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "reservation.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;
            try
            {
                return Results.Ok(await events.AddParticipantAsync(eventId, request, auth.User!.Id, cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { code = "event_reference_not_found", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "event_conflict", message = ex.Message });
            }
        }).WithName("AddEventParticipant");

        app.MapGet("/api/events/{eventId:guid}/participants", async (
            Guid eventId,
            HttpContext context,
            EventService events,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "operations.view", cancellationToken);
            if (auth.Error is not null) return auth.Error;
            return Results.Ok(await events.ListParticipantsAsync(eventId, cancellationToken));
        }).WithName("ListEventParticipants");

        app.MapPost("/api/events/{eventId:guid}/transition", async (
            Guid eventId,
            EventTransitionRequest request,
            HttpContext context,
            EventService events,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizationService.RequirePermissionAsync(context, database, "reservation.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;
            try
            {
                return Results.Ok(await events.TransitionAsync(eventId, request.Action, auth.User!.Id, cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { code = "event_not_found", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "event_conflict", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "invalid_event_transition", message = ex.Message });
            }
        }).WithName("TransitionEvent");
    }
}
