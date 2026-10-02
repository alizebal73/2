namespace GameNetManager.Shared.Contracts;

public sealed record CustomerDto(
    Guid Id,
    string? Code,
    string? Username,
    string FullName,
    string? Alias,
    string? NationalId,
    string? Phone,
    string? Email,
    string VipTier,
    decimal Balance,
    decimal FreeMoney,
    int FreeTimeMinutes,
    int ConcurrentLoginLimit,
    string? Notes);

public sealed record CreateCustomerRequest(
    string FullName,
    string? Code,
    string? Username,
    string? Alias,
    string? NationalId,
    string? Phone,
    string? Email,
    string VipTier,
    int ConcurrentLoginLimit,
    string? Notes,
    string? Password = null);

public sealed record ChangeCustomerPasswordRequest(string Password);
public sealed record CustomerLoginAuthRequest(string UsernameOrCode, string Password, string ClientKey);
public sealed record CustomerDebtSettlementRequest(string Method, Guid? AppUserId);

public sealed record UpdateCustomerRequest(
    string FullName,
    string? Code,
    string? Username,
    string? Alias,
    string? NationalId,
    string? Phone,
    string? Email,
    string VipTier,
    int ConcurrentLoginLimit,
    string? Notes);


public sealed record VipPackageDto(
    Guid Id,
    string Name,
    string Tier,
    decimal Price,
    int DurationDays,
    int DailyMinutes,
    int TotalMinutes,
    decimal DiscountPercent,
    string OverflowRule,
    string? Description,
    bool IsActive);

public sealed record CreateVipPackageRequest(
    string Name,
    string Tier,
    decimal Price,
    int DurationDays,
    int DailyMinutes,
    int TotalMinutes,
    decimal DiscountPercent,
    string OverflowRule,
    string? Description);

public sealed record AssignVipPackageRequest(Guid VipPackageId);
