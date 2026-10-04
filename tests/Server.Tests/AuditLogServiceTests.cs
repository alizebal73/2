using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task QueryFiltersOrdersAndPaginatesAuditLogs()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var admin = new AppUser
        {
            FullName = "مدیر آزمون",
            UserName = "audit-admin",
            Email = "audit-admin@gamenet.local",
            PasswordHash = PasswordSecurity.Hash("StrongPass123!"),
            Role = "Admin",
            IsActive = true
        };
        var operatorUser = new AppUser
        {
            FullName = "اپراتور آزمون",
            UserName = "audit-operator",
            Email = "audit-operator@gamenet.local",
            PasswordHash = PasswordSecurity.Hash("StrongPass123!"),
            Role = "Operator",
            IsActive = true
        };

        var first = new AuditLog
        {
            AppUser = operatorUser,
            Action = "SessionStarted",
            EntityName = "Session",
            EntityId = "session-1",
            Details = "شروع جلسه PC ۰۱",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var second = new AuditLog
        {
            AppUser = admin,
            Action = "TariffUpdated",
            EntityName = "Tariff",
            EntityId = "tariff-1",
            Details = "ویرایش تعرفه PC",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var third = new AuditLog
        {
            AppUser = operatorUser,
            Action = "InvoiceReverse",
            EntityName = "Invoice",
            EntityId = "invoice-1",
            Details = "برگشت فاکتور",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        database.AddRange(admin, operatorUser, first, second, third);
        await database.SaveChangesAsync();

        var service = new AuditLogService(database);

        var result = await service.QueryAsync(
            new AuditLogQuery(
                From: DateTimeOffset.UtcNow.AddMinutes(-6),
                To: DateTimeOffset.UtcNow,
                Operator: "audit-admin",
                Action: "Tariff",
                EntityName: "Tariff",
                Search: "ویرایش",
                Page: 1,
                PageSize: 10),
            CancellationToken.None);

        Assert.Equal(1, result.Total);
        var row = Assert.Single(result.Items);
        Assert.Equal("مدیر آزمون", row.Operator);
        Assert.Equal("TariffUpdated", row.Action);
        Assert.Equal("Tariff", row.EntityName);
        Assert.Equal("tariff-1", row.EntityId);
    }

    [Fact]
    public async Task QueryUsesStableNewestFirstOrderingAcrossPages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        for (var index = 0; index < 23; index++)
        {
            database.AuditLogs.Add(new AuditLog
            {
                Action = "TestAction",
                EntityName = "TestEntity",
                EntityId = $"entity-{index}",
                Details = $"جزئیات {index}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-index)
            });
        }

        await database.SaveChangesAsync();

        var service = new AuditLogService(database);
        var page1 = await service.QueryAsync(
            new AuditLogQuery(null, null, null, null, null, null, 1, 10),
            CancellationToken.None);
        var page2 = await service.QueryAsync(
            new AuditLogQuery(null, null, null, null, null, null, 2, 10),
            CancellationToken.None);

        Assert.Equal(23, page1.Total);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(10, page2.Items.Count);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
        Assert.True(page1.Items[0].CreatedAt >= page1.Items[^1].CreatedAt);
        Assert.True(page2.Items[0].CreatedAt >= page2.Items[^1].CreatedAt);
    }
}
