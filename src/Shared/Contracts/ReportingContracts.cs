using System;

namespace GameNetManager.Shared.Contracts;

public sealed record ReportSummaryDto(
    DateTimeOffset From,
    DateTimeOffset To,
    decimal Revenue,
    decimal Expense,
    decimal OperatingProfit,
    int Sessions,
    int PaidInvoices,
    int CustomersServed);

public sealed record StationPerformanceDto(
    Guid StationId,
    string StationName,
    string Zone,
    int SessionCount,
    double BillableMinutes,
    decimal Revenue,
    decimal AverageSessionRevenue,
    int OccupancyEvents);

public sealed record HeatmapCellDto(
    int DayOfWeek,
    int Hour,
    int SessionCount,
    double BillableMinutes,
    decimal Revenue);

public sealed record AuditExplorerDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid? AppUserId,
    string OperatorName,
    string Action,
    string EntityName,
    string? EntityId,
    string? Details);

public sealed record AuditExplorerFilterDto(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Action,
    string? EntityName,
    Guid? AppUserId,
    string? Search,
    int Limit = 200);
