using System.Net;
using GameNetManager.Server;
using GameNetManager.Server.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class ClientDeviceResolutionTests
{
    [Fact]
    public async Task Remote_client_is_bound_to_the_agent_with_the_same_ip()
    {
        var databaseName = $"client-device-resolution-{Guid.NewGuid():N}";
        await using var keeper = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
        await keeper.OpenAsync();

        await using var database = CreateContext(keeper);
        await database.Database.EnsureCreatedAsync();

        database.AgentDevices.Add(new AgentDevice
        {
            DeviceId = "agent-pc-01",
            Name = "PC-01",
            AgentTokenHash = "hash",
            IsActive = true,
            IsOnline = true,
            LastIpAddress = "192.168.0.111",
            LastSeenAt = DateTimeOffset.UtcNow,
        });
        await database.SaveChangesAsync();

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.0.111");

        var resolved = await ClientExperienceEndpoints.ResolveDeviceAsync(
            context,
            database,
            CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal("agent-pc-01", resolved!.DeviceId);
    }

    [Fact]
    public async Task Registered_client_can_resolve_when_agent_is_temporarily_offline()
    {
        var databaseName = $"client-device-registered-offline-{Guid.NewGuid():N}";
        await using var keeper = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
        await keeper.OpenAsync();

        await using var database = CreateContext(keeper);
        await database.Database.EnsureCreatedAsync();

        database.AgentDevices.Add(new AgentDevice
        {
            DeviceId = "agent-pc-offline",
            Name = "PC-offline",
            AgentTokenHash = "hash",
            IsActive = true,
            IsOnline = false,
            LastIpAddress = "192.168.0.113",
            LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await database.SaveChangesAsync();

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.0.113");

        var resolved = await ClientExperienceEndpoints.ResolveRegisteredDeviceAsync(
            context,
            database,
            CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal("agent-pc-offline", resolved!.DeviceId);
    }

    [Fact]
    public async Task Remote_client_cannot_resolve_an_agent_from_a_different_ip()
    {
        var databaseName = $"client-device-resolution-negative-{Guid.NewGuid():N}";
        await using var keeper = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
        await keeper.OpenAsync();

        await using var database = CreateContext(keeper);
        await database.Database.EnsureCreatedAsync();

        database.AgentDevices.Add(new AgentDevice
        {
            DeviceId = "agent-pc-02",
            Name = "PC-02",
            AgentTokenHash = "hash",
            IsActive = true,
            IsOnline = true,
            LastIpAddress = "192.168.0.112",
            LastSeenAt = DateTimeOffset.UtcNow,
        });
        await database.SaveChangesAsync();

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.0.111");

        var resolved = await ClientExperienceEndpoints.ResolveDeviceAsync(
            context,
            database,
            CancellationToken.None);

        Assert.Null(resolved);
    }

    private static GameNetDbContext CreateContext(Microsoft.Data.Sqlite.SqliteConnection connection)
        => new(new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options);
}
