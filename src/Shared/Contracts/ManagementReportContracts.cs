using System;

namespace GameNetManager.Shared.Contracts;

public sealed record CustomerPerformanceDto(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    int SessionCount,
    double BillableMinutes,
    decimal Revenue,
    int VipMinutesUsed,
    decimal WalletBalance,
    decimal OutstandingDebt);

public sealed record OperatorPerformanceDto(
    Guid AppUserId,
    string OperatorName,
    int ShiftCount,
    double ShiftHours,
    int PaidInvoiceCount,
    decimal Revenue,
    decimal Expenses,
    decimal Difference);
