using GameNetManager.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class ReservationService(GameNetDbContext database)
{
    public async Task<IReadOnlyList<ReservationDto>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        ReservationStatus? status,
        ReservationKind? kind,
        CancellationToken cancellationToken)
    {
        var query = database.Reservations
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Station)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);
        if (kind.HasValue)
            query = query.Where(item => item.Kind == kind.Value);

        // Keep SQLite DateTimeOffset filtering/order in CLR after a bounded read.
        var rows = await query
            .Take(2000)
            .ToListAsync(cancellationToken);

        return rows
            .Where(item => (!from.HasValue || item.EndAt >= from.Value)
                && (!to.HasValue || item.StartAt <= to.Value))
            .OrderBy(item => item.StartAt)
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => item.CreatedAt)
            .Take(1000)
            .Select(ToDto)
            .ToList();
    }

    public async Task<ReservationDto> CreateAsync(
        ReservationCreateRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty || request.StationId == Guid.Empty)
            throw new ArgumentException("مشتری و ایستگاه الزامی هستند.");
        if (request.DurationMinutes <= 0 || request.DurationMinutes > 24 * 60)
            throw new ArgumentException("مدت رزرو باید بین ۱ تا ۱۴۴۰ دقیقه باشد.");

        var kind = request.Kind.Trim().Equals("waitlist", StringComparison.OrdinalIgnoreCase)
            ? ReservationKind.Waitlist
            : ReservationKind.Reservation;
        var start = request.StartAt;
        var end = start.AddMinutes(request.DurationMinutes);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var customer = await database.Customers
            .FirstOrDefaultAsync(item => item.Id == request.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException("مشتری پیدا نشد.");

        var station = await database.Stations
            .FirstOrDefaultAsync(item => item.Id == request.StationId && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("ایستگاه پیدا نشد.");

        // Upgrade the SQLite transaction to a writer before checking the station's
        // reservation window. This serializes concurrent reservations on the same station.
        var stationLock = await database.Stations
            .Where(item => item.Id == station.Id && item.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        if (stationLock != 1)
            throw new KeyNotFoundException("ایستگاه پیدا نشد.");

        if (kind == ReservationKind.Reservation)
        {
            if (station.State is StationState.Offline or StationState.Maintenance)
                throw new InvalidOperationException("این ایستگاه قابل رزرو نیست.");

            var conflictCandidates = await database.Reservations
                .AsNoTracking()
                .Where(item => item.StationId == station.Id)
                .Where(item => item.Kind == ReservationKind.Reservation)
                .Where(item => item.Status == ReservationStatus.Pending
                    || item.Status == ReservationStatus.Confirmed
                    || item.Status == ReservationStatus.CheckedIn)
                .ToListAsync(cancellationToken);

            var hasConflict = conflictCandidates.Any(
                item => item.StartAt < end && item.EndAt > start);

            if (hasConflict)
                throw new InvalidOperationException("این ایستگاه در این بازه قبلاً رزرو شده است.");
        }

        var reservation = new Reservation
        {
            CustomerId = customer.Id,
            StationId = station.Id,
            StartAt = start,
            EndAt = end,
            Status = kind == ReservationKind.Waitlist ? ReservationStatus.Pending : ReservationStatus.Confirmed,
            Kind = kind,
            Priority = Math.Clamp(request.Priority, -100, 100),
            CreatedByUserId = appUserId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        database.Reservations.Add(reservation);
        database.AuditLogs.Add(new AuditLog
        {
            Action = kind == ReservationKind.Waitlist ? "WaitlistCreate" : "ReservationCreate",
            EntityName = "Reservation",
            EntityId = reservation.Id.ToString(),
            AppUserId = appUserId,
            Details = $"{customer.FullName} · {station.Name} · {start:O} تا {end:O}"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDto(reservation, customer, station);
    }

    public async Task<ReservationDto> TransitionAsync(
        Guid id,
        ReservationTransitionRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var reservation = await database.Reservations
            .Include(item => item.Customer)
            .Include(item => item.Station)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("رزرو/صف پیدا نشد.");

        var action = request.Action.Trim().ToLowerInvariant();
        switch (action)
        {
            case "confirm":
                if (reservation.Kind != ReservationKind.Reservation)
                    reservation.Kind = ReservationKind.Reservation;
                if (reservation.Status is ReservationStatus.Cancelled or ReservationStatus.Completed)
                    throw new InvalidOperationException("این مورد دیگر قابل تأیید نیست.");
                await EnsureNoConflictAsync(reservation, request.TargetStationId, cancellationToken);
                reservation.Status = ReservationStatus.Confirmed;
                break;

            case "cancel":
                if (reservation.Status == ReservationStatus.Completed)
                    throw new InvalidOperationException("رزرو تکمیل‌شده قابل لغو نیست.");
                reservation.Status = ReservationStatus.Cancelled;
                break;

            case "checkin":
                if (reservation.Status != ReservationStatus.Confirmed)
                    throw new InvalidOperationException("فقط رزرو تأییدشده قابل Check-in است.");
                reservation.Status = ReservationStatus.CheckedIn;
                reservation.Station.State = StationState.Occupied;
                break;

            case "complete":
                if (reservation.Status != ReservationStatus.CheckedIn
                    && reservation.Status != ReservationStatus.Confirmed)
                    throw new InvalidOperationException("وضعیت رزرو برای تکمیل معتبر نیست.");
                reservation.Status = ReservationStatus.Completed;
                break;

            case "assign":
                if (reservation.Kind != ReservationKind.Waitlist || reservation.Status != ReservationStatus.Pending)
                    throw new InvalidOperationException("فقط نفر در انتظار قابل تخصیص است.");
                await EnsureNoConflictAsync(reservation, request.TargetStationId, cancellationToken);
                reservation.Kind = ReservationKind.Reservation;
                reservation.Status = ReservationStatus.Confirmed;
                break;

            default:
                throw new ArgumentException("عملیات رزرو پشتیبانی نمی‌شود.");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
            reservation.Notes = request.Notes.Trim();

        database.AuditLogs.Add(new AuditLog
        {
            Action = action switch
            {
                "confirm" => "ReservationConfirm",
                "cancel" => "ReservationCancel",
                "checkin" => "ReservationCheckIn",
                "complete" => "ReservationComplete",
                "assign" => "WaitlistAssign",
                _ => "ReservationTransition"
            },
            EntityName = "Reservation",
            EntityId = reservation.Id.ToString(),
            AppUserId = appUserId,
            Details = request.Notes
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDto(reservation);
    }

    private async Task EnsureNoConflictAsync(
        Reservation reservation,
        Guid? targetStationId,
        CancellationToken cancellationToken)
    {
        if (targetStationId.HasValue)
        {
            var station = await database.Stations
                .FirstOrDefaultAsync(item => item.Id == targetStationId.Value && item.IsActive, cancellationToken)
                ?? throw new KeyNotFoundException("ایستگاه مقصد پیدا نشد.");
            reservation.StationId = station.Id;
            reservation.Station = station;
        }

        var stationLock = await database.Stations
            .Where(item => item.Id == reservation.StationId && item.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        if (stationLock != 1)
            throw new KeyNotFoundException("ایستگاه مقصد پیدا نشد.");

        var conflictCandidates = await database.Reservations
            .AsNoTracking()
            .Where(item => item.Id != reservation.Id)
            .Where(item => item.StationId == reservation.StationId)
            .Where(item => item.Kind == ReservationKind.Reservation)
            .Where(item => item.Status == ReservationStatus.Pending
                || item.Status == ReservationStatus.Confirmed
                || item.Status == ReservationStatus.CheckedIn)
            .ToListAsync(cancellationToken);

        var hasConflict = conflictCandidates.Any(
            item => item.StartAt < reservation.EndAt
                && item.EndAt > reservation.StartAt);

        if (hasConflict)
            throw new InvalidOperationException("ایستگاه مقصد در این بازه رزرو شده است.");
    }

    private static ReservationDto ToDto(Reservation item)
        => ToDto(item, item.Customer, item.Station);

    private static ReservationDto ToDto(Reservation item, Customer customer, Station station)
        => new(
            item.Id,
            item.CustomerId,
            customer.Code ?? customer.Username ?? customer.Id.ToString(),
            customer.FullName,
            station.Id,
            station.Name,
            item.StartAt,
            item.EndAt,
            item.Status.ToString(),
            item.Kind.ToString(),
            item.Priority,
            item.Notes);
}
