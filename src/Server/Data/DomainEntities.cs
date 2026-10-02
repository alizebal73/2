namespace GameNetManager.Server.Data;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum StationState
{
    Available,
    Occupied,
    Maintenance,
    Offline
}

public enum WalletTransactionType
{
    Credit,
    Debit
}

public enum InvoiceStatus
{
    Draft,
    Paid,
    Cancelled
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled
}

public enum SessionState
{
    Active,
    Completed,
    Cancelled
}

public enum TransactionDirection
{
    In,
    Out
}

public sealed class StationType : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<Station> Stations { get; set; } = new List<Station>();
}

public sealed class Tariff : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal DailyRate { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Station> Stations { get; set; } = new List<Station>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public sealed class Customer : BaseEntity
{
    public required string FullName { get; set; }
    public string? Code { get; set; }
    public string? Username { get; set; }
    public string? Alias { get; set; }
    public string? NationalId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsVip { get; set; }
    public string VipTier { get; set; } = "none";
    public Guid? VipPackageId { get; set; }
    public VipPackage? VipPackage { get; set; }
    public DateTimeOffset? VipActivatedAt { get; set; }
    public DateTimeOffset? VipExpiresAt { get; set; }
    public decimal Balance { get; set; }
    public decimal FreeMoney { get; set; }
    public int FreeTimeMinutes { get; set; }
    public int ConcurrentLoginLimit { get; set; } = 1;
    public string? Notes { get; set; }
    public string? PasswordHash { get; set; }
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public ICollection<GameAccount> GameAccounts { get; set; } = new List<GameAccount>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public enum BenefitTransactionType
{
    FreeMoneyCredit,
    FreeMoneyDebit,
    FreeTimeCredit,
    FreeTimeDebit
}

public sealed class BenefitTransaction : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public BenefitTransactionType Type { get; set; }
    public decimal MoneyAmount { get; set; }
    public int Minutes { get; set; }
    public Guid? ReferenceInvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class CustomerLogin : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public string ClientKey { get; set; } = string.Empty;
    public DateTimeOffset LoggedInAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LoggedOutAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Product : BaseEntity
{
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}

public sealed class AppUser : BaseEntity
{
    public required string FullName { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string Role { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public ICollection<AppUserPermission> Permissions { get; set; } = new List<AppUserPermission>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public sealed class Permission : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<AppUserPermission> AppUsers { get; set; } = new List<AppUserPermission>();
}

public sealed class AppUserPermission
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = default!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}

public sealed class VipPackage : BaseEntity
{
    public required string Name { get; set; }
    public string Tier { get; set; } = "custom";
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int DailyMinutes { get; set; }
    public int TotalMinutes { get; set; }
    public decimal DiscountPercent { get; set; }
    public string OverflowRule { get; set; } = "half-hourly";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

public sealed class Game : BaseEntity
{
    public required string Name { get; set; }
    public string? Genre { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<GameAccount> GameAccounts { get; set; } = new List<GameAccount>();
}

public sealed class GameAccount : BaseEntity
{
    public required string AccountName { get; set; }
    public string? Login { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid GameId { get; set; }
    public Game Game { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
}

public sealed class Client : BaseEntity
{
    public required string Name { get; set; }
    public string? Type { get; set; }
    public string? Contact { get; set; }
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public sealed class Reservation : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public Guid StationId { get; set; }
    public Station Station { get; set; } = default!;
    public Guid? TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Notes { get; set; }
}

public sealed class Session : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public Guid StationId { get; set; }
    public Station Station { get; set; } = default!;
    public Guid? TariffId { get; set; }
    public Tariff? Tariff { get; set; }
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? HourlyRateOverride { get; set; }
    public int Persons { get; set; } = 1;
    public SessionState State { get; set; } = SessionState.Active;
    public string? Notes { get; set; }
}

public sealed class Invoice : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid? SessionId { get; set; }
    public Customer Customer { get; set; } = default!;
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public sealed class InvoicePayment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public required string Method { get; set; }
    public decimal Amount { get; set; }
}

public sealed class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public sealed class WalletTransaction : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public decimal Amount { get; set; }
    public WalletTransactionType Type { get; set; }
    public Guid? ReferenceTransactionId { get; set; }
    public Guid? ReferenceInvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class InvoiceReversal : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public required string Reason { get; set; }
    public bool ExternalRefundRequired { get; set; }
}

public sealed class InventoryTransaction : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public int Quantity { get; set; }
    public TransactionDirection Direction { get; set; }
    public string? Notes { get; set; }
}

public sealed class Shift : BaseEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = default!;
    public DateTimeOffset OpenAt { get; set; }
    public DateTimeOffset? CloseAt { get; set; }
    public decimal CashOpening { get; set; }
    public decimal? CashClosing { get; set; }
    public string? Notes { get; set; }
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

public sealed class Expense : BaseEntity
{
    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = default!;
    public required string Category { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public sealed class AuditLog : BaseEntity
{
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public required string Action { get; set; }
    public required string EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}
