using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class Stage13CustomerAndUsersReportTests
{
    [Fact]
    public async Task CustomerVipReportUsesDraftInvoicesForDebtAndSessionTotalsForRange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection).Options;
        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var stationType = new StationType { Name = "PC Report Test" };
        var station = new Station { Name = "PC REPORT", Zone = "pc", Type = "PC", StationType = stationType };
        var vip = new VipPackage
        {
            Name = "Gold Test",
            Tier = "gold",
            Price = 500000,
            DurationDays = 30,
            DailyMinutes = 180,
            TotalMinutes = 3000,
            DiscountPercent = 10,
            IsActive = true
        };
        var customer = new Customer
        {
            FullName = "مشتری گزارش VIP",
            Code = "CVIP01",
            Username = "cvip01",
            VipTier = "gold",
            IsVip = true,
            VipPackage = vip,
            VipActivatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            VipExpiresAt = DateTimeOffset.UtcNow.AddDays(28),
            Balance = 250000
        };
        var now = DateTimeOffset.UtcNow;
        var inRange = new Session
        {
            Customer = customer,
            Station = station,
            StartAt = now.AddMinutes(-90),
            EndAt = now.AddMinutes(-30),
            State = SessionState.Completed,
            TotalAmount = 120000
        };
        var outsideRange = new Session
        {
            Customer = customer,
            Station = station,
            StartAt = now.AddDays(-5),
            EndAt = now.AddDays(-5).AddMinutes(45),
            State = SessionState.Completed,
            TotalAmount = 70000
        };
        var debt = new Invoice
        {
            Customer = customer,
            TotalAmount = 95000,
            Status = InvoiceStatus.Draft,
            IssuedAt = now.AddHours(-2)
        };

        database.AddRange(stationType, station, vip, customer, inRange, outsideRange, debt);
        await database.SaveChangesAsync();

        var service = new CustomerVipReportService(database);
        var result = await service.QueryAsync(
            new CustomerVipReportQuery(
                now.AddHours(-3),
                now,
                null,
                "active",
                "debtor",
                null,
                1,
                10),
            CancellationToken.None);

        var row = Assert.Single(result.Items);
        Assert.Equal("CVIP01", row.Code);
        Assert.Equal(95000m, row.Debt);
        Assert.Equal(120000m, row.SessionRevenue);
        Assert.Equal(1, row.SessionCount);
        Assert.Equal(1, result.Summary.ActiveVipCount);
        Assert.Equal(95000m, result.Summary.DebtTotal);
        Assert.Equal(120000m, result.Summary.SessionRevenue);
        Assert.True(row.UsedTotalMinutes >= 59);
    }

    [Fact]
    public async Task UsersShiftReportCombinesShiftPaymentsExpensesSessionsAndApprovedPayroll()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection).Options;
        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var user = new AppUser
        {
            FullName = "اپراتور گزارش",
            UserName = "stage13-report-user",
            Email = "stage13-report-user@gamenet.local",
            PasswordHash = PasswordSecurity.Hash("StrongPass123!"),
            Role = "Operator",
            IsActive = true
        };
        var profile = new EmployeeProfile
        {
            AppUser = user,
            PayType = "hourly",
            HourlyRate = 100000,
            IsActive = true
        };
        var shift = new Shift
        {
            AppUser = user,
            OpenAt = DateTimeOffset.UtcNow.AddHours(-4),
            CloseAt = DateTimeOffset.UtcNow.AddHours(-1),
            CashOpening = 100000
        };
        var expense = new Expense
        {
            Shift = shift,
            Category = "سایر",
            Amount = 20000,
            Description = "هزینه تست"
        };
        var customer = new Customer { FullName = "مشتری شیفت", Code = "SHIFT01", Username = "shift01" };
        var stationType = new StationType { Name = "PC Shift Test" };
        var station = new Station { Name = "PC SHIFT", Zone = "pc", Type = "PC", StationType = stationType };
        var invoice = new Invoice
        {
            Customer = customer,
            AppUser = user,
            TotalAmount = 150000,
            Status = InvoiceStatus.Paid,
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-2),
            PaidAt = DateTimeOffset.UtcNow.AddHours(-2)
        };
        var payment = new InvoicePayment { Invoice = invoice, Method = "cash", Amount = 150000 };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            AppUser = user,
            StartAt = DateTimeOffset.UtcNow.AddHours(-3),
            EndAt = DateTimeOffset.UtcNow.AddHours(-2.5),
            State = SessionState.Completed,
            TotalAmount = 150000
        };
        var payroll = new PayrollLedgerEntry
        {
            EmployeeProfile = profile,
            Kind = "SalaryPayment",
            Amount = 80000,
            EmployeePayableDelta = -80000,
            OwnerReceivableDelta = 0,
            Reason = "پرداخت تست",
            Status = ApprovalStatus.Approved,
            CreatedByUserId = user.Id,
            PaymentMethod = "cash"
        };

        database.AddRange(
            user,
            profile,
            shift,
            expense,
            customer,
            stationType,
            station,
            invoice,
            payment,
            session,
            payroll);

        await database.SaveChangesAsync();

        var service = new UsersShiftReportService(database);
        var result = await service.QueryAsync(
            new UsersShiftReportQuery(
                DateTimeOffset.UtcNow.AddHours(-6),
                DateTimeOffset.UtcNow,
                "stage13-report-user",
                "closed",
                1,
                10),
            CancellationToken.None);

        var row = Assert.Single(result.Items);
        Assert.Equal("اپراتور گزارش", row.FullName);
        Assert.Equal(1, row.ShiftCount);
        Assert.Equal(1, row.ClosedShiftCount);
        Assert.Equal(150000m, row.ShiftRevenue);
        Assert.Equal(150000m, row.ShiftCashSales);
        Assert.Equal(20000m, row.ShiftExpenses);
        Assert.Equal(130000m, row.ShiftDifference);
        Assert.Equal(1, row.SessionCount);
        Assert.Equal(150000m, row.SessionRevenue);
        Assert.Equal(80000m, row.PaidThisPeriod);
        Assert.Equal(80000m, result.Summary.PayrollPaid);
    }
}
