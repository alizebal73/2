using System;

namespace GameNetManager.Shared.Contracts;

public sealed record ReservationDto(
    Guid Id,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid StationId,
    string StationName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    string Kind,
    int Priority,
    string? Notes);

public sealed record ReservationCreateRequest(
    Guid CustomerId,
    Guid StationId,
    DateTimeOffset StartAt,
    int DurationMinutes,
    string Kind = "reservation",
    int Priority = 0,
    string? Notes = null);

public sealed record ReservationTransitionRequest(
    string Action,
    Guid? TargetStationId = null,
    string? Notes = null);
