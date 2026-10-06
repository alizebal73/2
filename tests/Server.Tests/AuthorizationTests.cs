using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class AuthorizationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AuthorizationTests() => _connection.Open();

    [Fact]
    public async Task DatabaseContainsAuthorizationTablesAndPermissionRules()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new AppUser
        {
            FullName = "اپراتور آزمون",
            UserName = "operator-test",
            Email = "operator-test@gamenet.local",
            PasswordHash = PasswordSecurity.Hash("StrongPass123!"),
            Role = "Operator",
            IsActive = true
        };
        var permission = new Permission
        {
            Name = "session.start",
            Description = "شروع و پایان جلسه"
        };

        db.AddRange(user, permission);
        await db.SaveChangesAsync();

        db.AppUserPermissions.Add(new AppUserPermission { AppUserId = user.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();

        var saved = await db.AppUsers
            .Include(item => item.Permissions)
            .ThenInclude(item => item.Permission)
            .SingleAsync(item => item.Id == user.Id);

        Assert.True(PasswordSecurity.Verify("StrongPass123!", saved.PasswordHash));
        Assert.True(AuthorizationService.HasPermission(saved, "session.start"));
        Assert.False(AuthorizationService.HasPermission(saved, "finance.manage"));

        var session = new AppUserSession
        {
            AppUserId = user.Id,
            TokenHash = PasswordSecurity.HashToken("test-token"),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        db.AppUserSessions.Add(session);

        var approval = new ApprovalRequest
        {
            Action = "discount",
            EntityName = "Session",
            EntityId = Guid.NewGuid().ToString(),
            Reason = "تخفیف بیشتر از حد مجاز",
            RequestedByUserId = user.Id
        };
        db.ApprovalRequests.Add(approval);
        await db.SaveChangesAsync();

        Assert.Equal(ApprovalStatus.Pending, await db.ApprovalRequests.Select(item => item.Status).SingleAsync());
        Assert.Single(await db.AppUserSessions.ToListAsync());
    }

    [Fact]
    public async Task NotificationReadFlowUpdatesOnlyTheAuthenticatedUser()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var firstUser = new AppUser
        {
            FullName = "اعلان اول",
            UserName = "notify-first",
            Email = "notify-first@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        var secondUser = new AppUser
        {
            FullName = "اعلان دوم",
            UserName = "notify-second",
            Email = "notify-second@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        database.AppUsers.AddRange(firstUser, secondUser);
        await database.SaveChangesAsync();

        database.Notifications.AddRange(
            new Notification
            {
                AppUserId = firstUser.Id,
                Category = "test",
                Title = "اعلان تست",
                Detail = "جزئیات تست",
                Level = NotificationLevel.Warning
            },
            new Notification
            {
                AppUserId = secondUser.Id,
                Category = "test",
                Title = "اعلان دیگر",
                Detail = "نباید دیده شود",
                Level = NotificationLevel.Info
            });
        await database.SaveChangesAsync();

        var unread = await database.Notifications.CountAsync(
            item => item.AppUserId == firstUser.Id && !item.IsRead);
        Assert.Equal(1, unread);

        var target = await database.Notifications.SingleAsync(item => item.AppUserId == firstUser.Id);
        target.IsRead = true;
        target.ReadAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync();

        Assert.True(await database.Notifications.AnyAsync(
            item => item.Id == target.Id && item.AppUserId == firstUser.Id && item.IsRead && item.ReadAt != null));
        Assert.True(await database.Notifications.AnyAsync(
            item => item.AppUserId == secondUser.Id && !item.IsRead));
    }

    [Fact]
    public void ReportScopeDefaultsToOwnAndExplicitAllGrantsGlobalReportScope()
    {
        var operatorUser = new AppUser
        {
            FullName = "Report Operator",
            UserName = "report-operator",
            Email = "report-operator@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        var view = new Permission { Name = "report.sessions.view" };
        operatorUser.Permissions.Add(new AppUserPermission { AppUser = operatorUser, Permission = view });

        Assert.False(AuthorizationService.HasReportAllScope(operatorUser, "sessions"));
        Assert.False(AuthorizationService.HasReportExport(operatorUser));

        var all = new Permission { Name = "report.sessions.scope.all" };
        var export = new Permission { Name = "reports.export" };
        operatorUser.Permissions.Add(new AppUserPermission { AppUser = operatorUser, Permission = all });
        operatorUser.Permissions.Add(new AppUserPermission { AppUser = operatorUser, Permission = export });

        Assert.True(AuthorizationService.HasReportAllScope(operatorUser, "sessions"));
        Assert.True(AuthorizationService.HasReportExport(operatorUser));
    }

    [Fact]
    public void BuffetInventoryMutationIsRestrictedToAdminAndOwner()
    {
        var operatorUser = new AppUser
        {
            FullName = "اپراتور بوفه",
            UserName = "buffet-operator",
            Email = "buffet-operator@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        var managerUser = new AppUser
        {
            FullName = "مدیر بوفه",
            UserName = "buffet-manager",
            Email = "buffet-manager@test.local",
            PasswordHash = "hash",
            Role = "Manager"
        };
        var adminUser = new AppUser
        {
            FullName = "مدیر اصلی",
            UserName = "buffet-admin",
            Email = "buffet-admin@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var ownerUser = new AppUser
        {
            FullName = "صاحب",
            UserName = "buffet-owner",
            Email = "buffet-owner@test.local",
            PasswordHash = "hash",
            Role = "Owner"
        };

        operatorUser.Permissions.Add(new AppUserPermission
        {
            AppUser = operatorUser,
            Permission = new Permission { Name = "buffet.inventory" }
        });
        managerUser.Permissions.Add(new AppUserPermission
        {
            AppUser = managerUser,
            Permission = new Permission { Name = "buffet.inventory" }
        });

        Assert.False(AuthorizationService.CanMutateBuffetInventory(operatorUser));
        Assert.False(AuthorizationService.CanMutateBuffetInventory(managerUser));
        Assert.True(AuthorizationService.CanMutateBuffetInventory(adminUser));
        Assert.True(AuthorizationService.CanMutateBuffetInventory(ownerUser));
    }

    [Fact]
    public void AdminAndOwnerHaveGlobalPermission()
    {
        var admin = new AppUser
        {
            FullName = "Admin",
            UserName = "admin",
            Email = "admin@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var owner = new AppUser
        {
            FullName = "Owner",
            UserName = "owner",
            Email = "owner@test.local",
            PasswordHash = "hash",
            Role = "Owner"
        };

        Assert.True(AuthorizationService.HasPermission(admin, "anything"));
        Assert.True(AuthorizationService.HasPermission(owner, "anything"));
    }

    public void Dispose() => _connection.Dispose();
}
