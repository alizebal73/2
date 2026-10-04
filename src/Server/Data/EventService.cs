using GameNetManager.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class EventService(GameNetDbContext database)
{
    public async Task<IReadOnlyList<EventDto>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var query = database.Events
            .AsNoTracking()
            .Include(item => item.Participants)
            .AsQueryable();

        // SQLite stores these timestamps as TEXT and does not reliably translate
        // DateTimeOffset range/order expressions. Load the bounded Event set first,
        // then apply the time window deterministically in CLR/UTC.
        var rows = await query
            .Take(1000)
            .ToListAsync(cancellationToken);

        return rows
            .Where(item => (!from.HasValue || item.EndAt >= from.Value)
                && (!to.HasValue || item.StartAt <= to.Value))
            .OrderBy(item => item.StartAt)
            .Take(500)
            .Select(ToDto)
            .ToList();
    }

    public async Task<EventDto> CreateAsync(
        EventCreateRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var kind = string.IsNullOrWhiteSpace(request.Kind) ? "tournament" : request.Kind.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام Event الزامی است.");
        if (request.DurationMinutes <= 0 || request.DurationMinutes > 7 * 24 * 60)
            throw new ArgumentException("مدت Event باید بین ۱ دقیقه تا ۷ روز باشد.");
        if (request.MaxParticipants < 0)
            throw new ArgumentException("ظرفیت Event نامعتبر است.");

        var start = request.StartAt;
        var end = start.AddMinutes(request.DurationMinutes);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // SQLite has one writer. Touch the requesting AppUser before the
        // overlap check to serialize concurrent Event creation attempts.
        var writerGate = await database.AppUsers
            .Where(item => item.Id == appUserId && item.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);

        if (writerGate != 1)
            throw new KeyNotFoundException("اپراتور ایجادکننده Event پیدا نشد.");

        var eventCandidates = kind.Equals("tournament", StringComparison.OrdinalIgnoreCase)
            ? await database.Events
                .AsNoTracking()
                .Where(item => item.Status != EventStatus.Cancelled)
                .Where(item => item.Status != EventStatus.Completed)
                .ToListAsync(cancellationToken)
            : [];

        var hasConflict = kind.Equals("tournament", StringComparison.OrdinalIgnoreCase)
            && eventCandidates.Any(item => item.StartAt < end && item.EndAt > start);

        if (hasConflict)
            throw new InvalidOperationException("Event دیگری در این بازه فعال است.");

        var entity = new GameEvent
        {
            Name = name,
            Kind = kind,
            StartAt = start,
            EndAt = end,
            MaxParticipants = request.MaxParticipants,
            Status = EventStatus.Scheduled,
            CreatedByUserId = appUserId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        database.Events.Add(entity);
        database.AuditLogs.Add(new AuditLog
        {
            Action = "EventCreate",
            EntityName = "GameEvent",
            EntityId = entity.Id.ToString(),
            AppUserId = appUserId,
            Details = $"{name} · {kind} · {start:O}–{end:O}"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<EventParticipantDto>> ListParticipantsAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var rows = await database.EventParticipants
            .AsNoTracking()
            .Include(item => item.Customer)
            .Where(item => item.EventId == eventId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(item => item.Seed)
            .ThenBy(item => item.JoinedAt)
            .Select(item => new EventParticipantDto(
            item.Id,
            item.EventId,
            item.CustomerId,
            item.Customer.Code ?? item.Customer.Username ?? item.CustomerId.ToString(),
            item.Customer.FullName,
            item.Seed,
            item.Status,
            item.JoinedAt)).ToList();
    }

    public async Task<EventParticipantDto> AddParticipantAsync(
        Guid eventId,
        EventParticipantRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var writerLockAt = DateTimeOffset.UtcNow;
        var lockedEvent = await database.Events
            .Where(item => item.Id == eventId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UpdatedAt, writerLockAt), cancellationToken);
        if (lockedEvent != 1)
            throw new KeyNotFoundException("Event پیدا نشد.");

        var entity = await database.Events
            .Include(item => item.Participants)
            .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            ?? throw new KeyNotFoundException("Event پیدا نشد.");

        // ExecuteUpdate bypasses EF tracking. If this Event was already tracked by a
        // previous operation in the same scoped service/context, its concurrency-token
        // OriginalValue would still contain the old UpdatedAt and the next SaveChanges
        // would legitimately affect zero rows. Reload the tracked row so its current
        // and original concurrency values match the writer-locked database row.
        await database.Entry(entity).ReloadAsync(cancellationToken);

        if (entity.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw new InvalidOperationException("این Event دیگر شرکت‌کننده جدید نمی‌پذیرد.");

        if (entity.MaxParticipants > 0 && entity.Participants.Count >= entity.MaxParticipants)
            throw new InvalidOperationException("ظرفیت Event تکمیل شده است.");

        if (entity.Participants.Any(item => item.CustomerId == request.CustomerId))
            throw new InvalidOperationException("این مشتری قبلاً در Event ثبت شده است.");

        var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == request.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException("مشتری پیدا نشد.");

        if (request.Seed > 0
            && entity.Participants.Any(item => item.Seed == request.Seed))
            throw new InvalidOperationException("Seed انتخاب‌شده تکراری است.");

        var participant = new GameEventParticipant
        {
            EventId = entity.Id,
            CustomerId = customer.Id,
            Seed = Math.Max(0, request.Seed),
            Status = "registered",
            JoinedAt = DateTimeOffset.UtcNow
        };

        database.EventParticipants.Add(participant);
        database.AuditLogs.Add(new AuditLog
        {
            Action = "EventParticipantAdd",
            EntityName = "GameEventParticipant",
            EntityId = participant.Id.ToString(),
            AppUserId = appUserId,
            Details = $"{customer.FullName} · Event {entity.Name}"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EventParticipantDto(
            participant.Id,
            participant.EventId,
            participant.CustomerId,
            customer.Code ?? customer.Username ?? customer.Id.ToString(),
            customer.FullName,
            participant.Seed,
            participant.Status,
            participant.JoinedAt);
    }

    public async Task<EventDto> TransitionAsync(
        Guid eventId,
        string action,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        var entity = await database.Events
            .Include(item => item.Participants)
            .FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            ?? throw new KeyNotFoundException("Event پیدا نشد.");

        var next = action.Trim().ToLowerInvariant() switch
        {
            "schedule" => EventStatus.Scheduled,
            "start" => EventStatus.Running,
            "complete" => EventStatus.Completed,
            "cancel" => EventStatus.Cancelled,
            _ => throw new ArgumentException("عملیات Event معتبر نیست.")
        };

        if (entity.Status == EventStatus.Completed && next != EventStatus.Completed)
            throw new InvalidOperationException("Event تکمیل‌شده قابل تغییر نیست.");
        if (next == EventStatus.Running && entity.StartAt > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("زمان شروع Event هنوز نرسیده است.");

        entity.Status = next;
        database.AuditLogs.Add(new AuditLog
        {
            Action = "Event" + next,
            EntityName = "GameEvent",
            EntityId = entity.Id.ToString(),
            AppUserId = appUserId,
            Details = entity.Name
        });
        await database.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    private static EventDto ToDto(GameEvent item)
        => new(
            item.Id,
            item.Name,
            item.Kind,
            item.StartAt,
            item.EndAt,
            item.Status.ToString(),
            item.MaxParticipants,
            item.Participants.Count,
            item.Notes);
}
