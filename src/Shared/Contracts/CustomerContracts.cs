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
    string? Notes);

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
