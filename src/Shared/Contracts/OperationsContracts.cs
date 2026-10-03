using System;

namespace GameNetManager.Shared.Contracts;

public sealed record StationManagementDto(
    Guid Id,
    string Name,
    string Zone,
    string Type,
    decimal RatePerHour,
    string State,
    Guid StationTypeId,
    Guid? TariffId,
    string NetworkRoute,
    bool IsActive);

public sealed record StationWriteRequest(
    string Name,
    string Zone,
    string Type,
    Guid StationTypeId,
    Guid? TariffId,
    decimal RatePerHour,
    string NetworkRoute = "internet1",
    bool IsActive = true);

public sealed record OperationsHealthDto(
    DateTimeOffset GeneratedAt,
    int StationsTotal,
    int StationsAvailable,
    int StationsOccupied,
    int StationsMaintenance,
    int StationsOffline,
    int AgentsTotal,
    int AgentsOnline,
    int ActiveSessions,
    int ActiveLeases,
    IReadOnlyDictionary<string, int> NetworkRoutes);

