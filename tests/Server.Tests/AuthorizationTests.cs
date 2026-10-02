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
