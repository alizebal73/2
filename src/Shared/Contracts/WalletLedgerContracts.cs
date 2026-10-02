namespace GameNetManager.Shared.Contracts;

public sealed record WalletLedgerEntryDto(
    Guid Id,
    Guid CustomerId,
    decimal Amount,
    string Type,
    string Description,
    DateTimeOffset CreatedAt,
    decimal BalanceAfter,
    Guid? ReferenceTransactionId);

public sealed record WalletTransactionRequestDto(
    decimal Amount,
    string Type,
    string Description,
    Guid? AppUserId);

public sealed record WalletRefundRequestDto(
    decimal Amount,
    string Reason,
    Guid? AppUserId,
    Guid? SourceTransactionId);
