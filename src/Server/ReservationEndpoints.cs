using GameNetManager.Shared.Contracts;

namespace GameNetManager.Server;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/reservations", async (
            HttpContext context,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? status,
            string? kind,
            ReservationService reservations,
            GameNetManager.Server.Data.GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await GameNetManager.Server.Data.AuthorizationService.RequirePermissionAsync(
                context, database, "reservation.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            GameNetManager.Server.Data.ReservationStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<GameNetManager.Server.Data.ReservationStatus>(status, true, out var statusValue))
                parsedStatus = statusValue;

            GameNetManager.Server.Data.ReservationKind? parsedKind = null;
            if (!string.IsNullOrWhiteSpace(kind)
                && Enum.TryParse<GameNetManager.Server.Data.ReservationKind>(kind, true, out var kindValue))
                parsedKind = kindValue;

            return Results.Ok(await reservations.ListAsync(from, to, parsedStatus, parsedKind, cancellationToken));
        }).WithName("ListReservations");

        app.MapPost("/api/reservations", async (
            ReservationCreateRequest request,
            HttpContext context,
            ReservationService reservations,
            GameNetManager.Server.Data.GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await GameNetManager.Server.Data.AuthorizationService.RequirePermissionAsync(
                context, database, "reservation.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            try
            {
                return Results.Ok(await reservations.CreateAsync(
                    request,
                    auth.User!.Id,
                    cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { code = "reservation_reference_not_found", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "reservation_conflict", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "invalid_reservation", message = ex.Message });
            }
        }).WithName("CreateReservation");

        app.MapPost("/api/reservations/{reservationId:guid}/transition", async (
            Guid reservationId,
            ReservationTransitionRequest request,
            HttpContext context,
            ReservationService reservations,
            GameNetManager.Server.Data.GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await GameNetManager.Server.Data.AuthorizationService.RequirePermissionAsync(
                context, database, "reservation.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            try
            {
                return Results.Ok(await reservations.TransitionAsync(
                    reservationId,
                    request,
                    auth.User!.Id,
                    cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { code = "reservation_not_found", message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { code = "reservation_conflict", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "invalid_reservation_transition", message = ex.Message });
            }
        }).WithName("TransitionReservation");

        app.MapGet("/api/waitlist", async (
            HttpContext context,
            ReservationService reservations,
            GameNetManager.Server.Data.GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var auth = await GameNetManager.Server.Data.AuthorizationService.RequirePermissionAsync(
                context, database, "reservation.manage", cancellationToken);
            if (auth.Error is not null) return auth.Error;

            return Results.Ok(await reservations.ListAsync(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(7),
                GameNetManager.Server.Data.ReservationStatus.Pending,
                GameNetManager.Server.Data.ReservationKind.Waitlist,
                cancellationToken));
        }).WithName("ListWaitlist");
    }
}
