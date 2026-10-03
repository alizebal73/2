using System;

namespace GameNetManager.Shared.Contracts;

public sealed record EventDto(
    Guid Id,
    string Name,
    string Kind,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    int MaxParticipants,
    int ParticipantCount,
    string? Notes);

public sealed record EventCreateRequest(
    string Name,
    string Kind,
    DateTimeOffset StartAt,
    int DurationMinutes,
    int MaxParticipants = 0,
    string? Notes = null);

public sealed record EventParticipantDto(
    Guid Id,
    Guid EventId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    int Seed,
    string Status,
    DateTimeOffset JoinedAt);

public sealed record EventParticipantRequest(
    Guid CustomerId,
    int Seed = 0);
