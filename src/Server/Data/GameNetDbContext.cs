using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class GameNetDbContext(DbContextOptions<GameNetDbContext> options) : DbContext(options)
{
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<StationType> StationTypes => Set<StationType>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AppUserPermission> AppUserPermissions => Set<AppUserPermission>();
    public DbSet<AppUserSession> AppUserSessions => Set<AppUserSession>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<VipPackage> VipPackages => Set<VipPackage>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameAccount> GameAccounts => Set<GameAccount>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<BenefitTransaction> BenefitTransactions => Set<BenefitTransaction>();
    public DbSet<CustomerLogin> CustomerLogins => Set<CustomerLogin>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<InvoiceReversal> InvoiceReversals => Set<InvoiceReversal>();

    private void TouchUpdatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        TouchUpdatedAt();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        TouchUpdatedAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureStation(modelBuilder);
        ConfigureStationType(modelBuilder);
        ConfigureTariff(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureCustomerLogin(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureAppUser(modelBuilder);
        ConfigurePermission(modelBuilder);
        ConfigureAppUserPermission(modelBuilder);
        ConfigureAppUserSession(modelBuilder);
        ConfigureApprovalRequest(modelBuilder);
        ConfigureVipPackage(modelBuilder);
        ConfigureGame(modelBuilder);
        ConfigureGameAccount(modelBuilder);
        ConfigureClient(modelBuilder);
        ConfigureReservation(modelBuilder);
        ConfigureSession(modelBuilder);
        ConfigureInvoice(modelBuilder);
        ConfigureInvoiceItem(modelBuilder);
        ConfigureWalletTransaction(modelBuilder);
        ConfigureBenefitTransaction(modelBuilder);
        ConfigureInvoiceReversal(modelBuilder);
        ConfigureInventoryTransaction(modelBuilder);
        ConfigureShift(modelBuilder);
        ConfigureExpense(modelBuilder);
        ConfigureAuditLog(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => typeof(BaseEntity).IsAssignableFrom(type.ClrType)))
        {
            var updatedAt = entityType.FindProperty(nameof(BaseEntity.UpdatedAt));
            if (updatedAt is not null)
                updatedAt.IsConcurrencyToken = true;
        }
    }

    private static void ConfigureStation(ModelBuilder modelBuilder)
    {
        var station = modelBuilder.Entity<Station>();
        station.HasKey(item => item.Id);
        station.HasIndex(item => item.Name).IsUnique();
        station.HasIndex(item => item.Zone);
        station.Property(item => item.Name).HasMaxLength(80).IsRequired();
        station.Property(item => item.Zone).HasMaxLength(60).IsRequired();
        station.Property(item => item.Type).HasMaxLength(50).IsRequired();
        station.Property(item => item.State).HasConversion<string>().HasMaxLength(20);
        station.HasOne(item => item.StationType)
            .WithMany(item => item.Stations)
            .HasForeignKey(item => item.StationTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        station.HasOne(item => item.Tariff)
            .WithMany(item => item.Stations)
            .HasForeignKey(item => item.TariffId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureStationType(ModelBuilder modelBuilder)
    {
        var stationType = modelBuilder.Entity<StationType>();
        stationType.HasKey(item => item.Id);
        stationType.HasIndex(item => item.Name).IsUnique();
        stationType.Property(item => item.Name).HasMaxLength(80).IsRequired();
        stationType.Property(item => item.Description).HasMaxLength(250);
    }

    private static void ConfigureTariff(ModelBuilder modelBuilder)
    {
        var tariff = modelBuilder.Entity<Tariff>();
        tariff.HasKey(item => item.Id);
        tariff.HasIndex(item => item.Name).IsUnique();
        tariff.Property(item => item.Name).HasMaxLength(80).IsRequired();
        tariff.Property(item => item.Description).HasMaxLength(250);
        tariff.Property(item => item.HourlyRate).HasColumnType("decimal(18,2)");
        tariff.Property(item => item.DailyRate).HasColumnType("decimal(18,2)");
    }

    private static void ConfigureCustomer(ModelBuilder modelBuilder)
    {
        var customer = modelBuilder.Entity<Customer>();
        customer.HasKey(item => item.Id);
        customer.HasIndex(item => item.Code).IsUnique();
        customer.HasIndex(item => item.Username).IsUnique();
        customer.HasIndex(item => item.Phone).IsUnique();
        customer.HasIndex(item => item.Email).IsUnique();
        customer.HasIndex(item => item.NationalId).IsUnique();
        customer.Property(item => item.FullName).HasMaxLength(120).IsRequired();
        customer.Property(item => item.Code).HasMaxLength(20);
        customer.Property(item => item.Username).HasMaxLength(60);
        customer.Property(item => item.Phone).HasMaxLength(20);
        customer.Property(item => item.Email).HasMaxLength(120);
        customer.Property(item => item.Alias).HasMaxLength(120);
        customer.Property(item => item.NationalId).HasMaxLength(20);
        customer.Property(item => item.VipTier).HasMaxLength(20).IsRequired();
        customer.Property(item => item.Notes).HasMaxLength(500);
        customer.Property(item => item.PasswordHash).HasMaxLength(250);
        customer.Property(item => item.Balance).HasColumnType("decimal(18,2)");
        customer.Property(item => item.FreeMoney).HasColumnType("decimal(18,2)");
        customer.Property(item => item.ConcurrentLoginLimit).IsRequired();
        customer.HasOne(item => item.VipPackage)
            .WithMany(item => item.Customers)
            .HasForeignKey(item => item.VipPackageId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureCustomerLogin(ModelBuilder modelBuilder)
    {
        var login = modelBuilder.Entity<CustomerLogin>();
        login.HasKey(item => item.Id);
        login.Property(item => item.ClientKey).HasMaxLength(120).IsRequired();
        login.Property(item => item.IsActive).IsRequired();
        login.HasIndex(item => new { item.CustomerId, item.ClientKey, item.IsActive });
        login.HasOne(item => item.Customer)
            .WithMany()
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();
        product.HasKey(item => item.Id);
        product.HasIndex(item => item.Name).IsUnique();
        product.Property(item => item.Name).HasMaxLength(120).IsRequired();
        product.Property(item => item.Category).HasMaxLength(80).IsRequired();
        product.Property(item => item.UnitPrice).HasColumnType("decimal(18,2)");
        product.Property(item => item.CostPrice).HasColumnType("decimal(18,2)");
        product.Property(item => item.MinimumStock).IsRequired();
        product.Property(item => item.Unit).HasMaxLength(20).IsRequired();
    }

    private static void ConfigureAppUser(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.HasKey(item => item.Id);
        user.HasIndex(item => item.UserName).IsUnique();
        user.HasIndex(item => item.Email).IsUnique();
        user.Property(item => item.FullName).HasMaxLength(120).IsRequired();
        user.Property(item => item.UserName).HasMaxLength(60).IsRequired();
        user.Property(item => item.Email).HasMaxLength(120).IsRequired();
        user.Property(item => item.PasswordHash).HasMaxLength(250).IsRequired();
        user.Property(item => item.Role).HasMaxLength(60).IsRequired();
    }

    private static void ConfigurePermission(ModelBuilder modelBuilder)
    {
        var permission = modelBuilder.Entity<Permission>();
        permission.HasKey(item => item.Id);
        permission.HasIndex(item => item.Name).IsUnique();
        permission.Property(item => item.Name).HasMaxLength(80).IsRequired();
        permission.Property(item => item.Description).HasMaxLength(250);
    }

    private static void ConfigureAppUserPermission(ModelBuilder modelBuilder)
    {
        var join = modelBuilder.Entity<AppUserPermission>();
        join.HasKey(item => new { item.AppUserId, item.PermissionId });
        join.HasOne(item => item.AppUser)
            .WithMany(item => item.Permissions)
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
        join.HasOne(item => item.Permission)
            .WithMany(item => item.AppUsers)
            .HasForeignKey(item => item.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureAppUserSession(ModelBuilder modelBuilder)
    {
        var session = modelBuilder.Entity<AppUserSession>();
        session.HasKey(item => item.Id);
        session.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
        session.HasIndex(item => item.TokenHash).IsUnique();
        session.Property(item => item.ExpiresAt).IsRequired();
        session.HasOne(item => item.AppUser)
            .WithMany()
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureApprovalRequest(ModelBuilder modelBuilder)
    {
        var approval = modelBuilder.Entity<ApprovalRequest>();
        approval.HasKey(item => item.Id);
        approval.Property(item => item.Action).HasMaxLength(80).IsRequired();
        approval.Property(item => item.EntityName).HasMaxLength(80).IsRequired();
        approval.Property(item => item.EntityId).HasMaxLength(120);
        approval.Property(item => item.Reason).HasMaxLength(500).IsRequired();
        approval.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        approval.Property(item => item.DecisionNote).HasMaxLength(500);
        approval.HasIndex(item => new { item.Status, item.CreatedAt });
        approval.HasOne(item => item.RequestedByUser)
            .WithMany()
            .HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        approval.HasOne(item => item.DecidedByUser)
            .WithMany()
            .HasForeignKey(item => item.DecidedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureVipPackage(ModelBuilder modelBuilder)
    {
        var vipPackage = modelBuilder.Entity<VipPackage>();
        vipPackage.HasKey(item => item.Id);
        vipPackage.HasIndex(item => item.Name).IsUnique();
        vipPackage.Property(item => item.Name).HasMaxLength(120).IsRequired();
        vipPackage.Property(item => item.Tier).HasMaxLength(30).IsRequired();
        vipPackage.Property(item => item.Description).HasMaxLength(250);
        vipPackage.Property(item => item.Price).HasColumnType("decimal(18,2)");
        vipPackage.Property(item => item.DiscountPercent).HasColumnType("decimal(18,2)");

    }

    private static void ConfigureGame(ModelBuilder modelBuilder)
    {
        var game = modelBuilder.Entity<Game>();
        game.HasKey(item => item.Id);
        game.HasIndex(item => item.Name).IsUnique();
        game.Property(item => item.Name).HasMaxLength(120).IsRequired();
        game.Property(item => item.Genre).HasMaxLength(80);
    }

    private static void ConfigureGameAccount(ModelBuilder modelBuilder)
    {
        var gameAccount = modelBuilder.Entity<GameAccount>();
        gameAccount.HasKey(item => item.Id);
        gameAccount.HasIndex(item => item.AccountName).IsUnique();
        gameAccount.Property(item => item.AccountName).HasMaxLength(120).IsRequired();
        gameAccount.Property(item => item.Login).HasMaxLength(120);
        gameAccount.Property(item => item.PasswordHash).HasMaxLength(250);
        gameAccount.HasOne(item => item.Game)
            .WithMany(item => item.GameAccounts)
            .HasForeignKey(item => item.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        gameAccount.HasOne(item => item.Customer)
            .WithMany(item => item.GameAccounts)
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureClient(ModelBuilder modelBuilder)
    {
        var client = modelBuilder.Entity<Client>();
        client.HasKey(item => item.Id);
        client.HasIndex(item => item.Name).IsUnique();
        client.Property(item => item.Name).HasMaxLength(120).IsRequired();
        client.Property(item => item.Type).HasMaxLength(60);
        client.Property(item => item.Contact).HasMaxLength(250);
    }

    private static void ConfigureReservation(ModelBuilder modelBuilder)
    {
        var reservation = modelBuilder.Entity<Reservation>();
        reservation.HasKey(item => item.Id);
        reservation.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        reservation.Property(item => item.Notes).HasMaxLength(500);
        reservation.HasOne(item => item.Customer)
            .WithMany(item => item.Reservations)
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        reservation.HasOne(item => item.Station)
            .WithMany(item => item.Reservations)
            .HasForeignKey(item => item.StationId)
            .OnDelete(DeleteBehavior.Cascade);
        reservation.HasOne(item => item.Tariff)
            .WithMany(item => item.Reservations)
            .HasForeignKey(item => item.TariffId)
            .OnDelete(DeleteBehavior.SetNull);
        reservation.HasOne(item => item.Client)
            .WithMany(item => item.Reservations)
            .HasForeignKey(item => item.ClientId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureSession(ModelBuilder modelBuilder)
    {
        var session = modelBuilder.Entity<Session>();
        session.HasKey(item => item.Id);
        session.Property(item => item.TotalAmount).HasColumnType("decimal(18,2)");
        session.Property(item => item.HourlyRateOverride).HasColumnType("decimal(18,2)");
        session.Property(item => item.Persons).IsRequired();
        session.Property(item => item.State).HasConversion<string>().HasMaxLength(20);
        session.Property(item => item.Notes).HasMaxLength(500);
        session.HasOne(item => item.Customer)
            .WithMany(item => item.Sessions)
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        session.HasOne(item => item.Station)
            .WithMany(item => item.Sessions)
            .HasForeignKey(item => item.StationId)
            .OnDelete(DeleteBehavior.Cascade);
        session.HasOne(item => item.Tariff)
            .WithMany(item => item.Sessions)
            .HasForeignKey(item => item.TariffId)
            .OnDelete(DeleteBehavior.SetNull);
        session.HasOne(item => item.AppUser)
            .WithMany(item => item.Sessions)
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureInvoice(ModelBuilder modelBuilder)
    {
        var invoice = modelBuilder.Entity<Invoice>();
        invoice.HasKey(item => item.Id);
        invoice.Property(item => item.TotalAmount).HasColumnType("decimal(18,2)");
        invoice.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        invoice.HasIndex(item => item.SessionId).IsUnique();
        invoice.HasOne(item => item.Customer)
            .WithMany(item => item.Invoices)
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        invoice.HasOne(item => item.AppUser)
            .WithMany(item => item.Invoices)
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureInvoicePayment(ModelBuilder modelBuilder)
    {
        var payment = modelBuilder.Entity<InvoicePayment>();
        payment.HasKey(item => item.Id);
        payment.Property(item => item.Method).HasMaxLength(20).IsRequired();
        payment.Property(item => item.Amount).HasColumnType("decimal(18,2)");
        payment.HasOne(item => item.Invoice)
            .WithMany()
            .HasForeignKey(item => item.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInvoiceItem(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<InvoiceItem>();
        item.HasKey(x => x.Id);
        item.Property(item => item.Description).HasMaxLength(200).IsRequired();
        item.Property(item => item.UnitPrice).HasColumnType("decimal(18,2)");
        item.Property(item => item.Amount).HasColumnType("decimal(18,2)");
        item.HasOne(item => item.Invoice)
            .WithMany(item => item.Items)
            .HasForeignKey(item => item.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
        item.HasOne(item => item.Product)
            .WithMany(item => item.InvoiceItems)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureWalletTransaction(ModelBuilder modelBuilder)
    {
        var transaction = modelBuilder.Entity<WalletTransaction>();
        transaction.HasKey(item => item.Id);
        transaction.Property(item => item.Amount).HasColumnType("decimal(18,2)");
        transaction.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
        transaction.Property(item => item.Description).HasMaxLength(250).IsRequired();
        transaction.HasOne(item => item.Customer)
            .WithMany(item => item.WalletTransactions)
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        transaction.HasIndex(item => item.ReferenceInvoiceId);
    }

    private static void ConfigureBenefitTransaction(ModelBuilder modelBuilder)
    {
        var transaction = modelBuilder.Entity<BenefitTransaction>();
        transaction.HasKey(item => item.Id);
        transaction.Property(item => item.Type).HasConversion<string>().HasMaxLength(30);
        transaction.Property(item => item.MoneyAmount).HasColumnType("decimal(18,2)");
        transaction.Property(item => item.Description).HasMaxLength(250).IsRequired();
        transaction.HasOne(item => item.Customer)
            .WithMany()
            .HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        transaction.HasIndex(item => item.ReferenceInvoiceId);
    }

    private static void ConfigureInvoiceReversal(ModelBuilder modelBuilder)
    {
        var reversal = modelBuilder.Entity<InvoiceReversal>();
        reversal.HasKey(item => item.Id);
        reversal.Property(item => item.Reason).HasMaxLength(500).IsRequired();
        reversal.Property(item => item.ExternalRefundRequired).IsRequired();
        reversal.HasIndex(item => item.InvoiceId).IsUnique();
        reversal.HasOne(item => item.Invoice)
            .WithMany()
            .HasForeignKey(item => item.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
        reversal.HasOne(item => item.AppUser)
            .WithMany()
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureInventoryTransaction(ModelBuilder modelBuilder)
    {
        var transaction = modelBuilder.Entity<InventoryTransaction>();
        transaction.HasKey(item => item.Id);
        transaction.Property(item => item.Direction).HasConversion<string>().HasMaxLength(20);
        transaction.Property(item => item.Kind).HasMaxLength(20).IsRequired();
        transaction.Property(item => item.UnitPrice).HasColumnType("decimal(18,2)");
        transaction.Property(item => item.UnitCost).HasColumnType("decimal(18,2)");
        transaction.Property(item => item.Notes).HasMaxLength(250);
        transaction.HasOne(item => item.Product)
            .WithMany(item => item.InventoryTransactions)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        transaction.HasOne(item => item.AppUser)
            .WithMany(item => item.InventoryTransactions)
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);
        transaction.HasIndex(item => item.ReferenceInvoiceId);
    }

    private static void ConfigureShift(ModelBuilder modelBuilder)
    {
        var shift = modelBuilder.Entity<Shift>();
        shift.HasKey(item => item.Id);
        shift.Property(item => item.CashOpening).HasColumnType("decimal(18,2)");
        shift.Property(item => item.CashClosing).HasColumnType("decimal(18,2)");
        shift.Property(item => item.Notes).HasMaxLength(500);
        shift.HasOne(item => item.AppUser)
            .WithMany(item => item.Shifts)
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureExpense(ModelBuilder modelBuilder)
    {
        var expense = modelBuilder.Entity<Expense>();
        expense.HasKey(item => item.Id);
        expense.Property(item => item.Category).HasMaxLength(80).IsRequired();
        expense.Property(item => item.Description).HasMaxLength(250);
        expense.Property(item => item.Amount).HasColumnType("decimal(18,2)");
        expense.HasOne(item => item.Shift)
            .WithMany(item => item.Expenses)
            .HasForeignKey(item => item.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        var auditLog = modelBuilder.Entity<AuditLog>();
        auditLog.HasKey(item => item.Id);
        auditLog.Property(item => item.Action).HasMaxLength(80).IsRequired();
        auditLog.Property(item => item.EntityName).HasMaxLength(80).IsRequired();
        auditLog.Property(item => item.EntityId).HasMaxLength(120);
        auditLog.Property(item => item.Details).HasMaxLength(1000);
        auditLog.HasOne(item => item.AppUser)
            .WithMany(item => item.AuditLogs)
            .HasForeignKey(item => item.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}