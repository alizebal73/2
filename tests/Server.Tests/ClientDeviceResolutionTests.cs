using System.Net;
using GameNetManager.Server;
using GameNetManager.Server.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Protected_device_cookie_resolves_agent_without_relying_on_remote_ip()
    {
        var databaseName = $"client-device-cookie-{Guid.NewGuid():N}";
        await using var keeper = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
        await keeper.OpenAsync();

        await using var database = CreateContext(keeper);
        await database.Database.EnsureCreatedAsync();

        var device = new AgentDevice
        {
            DeviceId = "agent-cookie-01",
            Name = "PC-cookie",
            AgentTokenHash = "hash",
            IsActive = true,
            IsOnline = true,
            LastIpAddress = "192.168.0.111",
            LastSeenAt = DateTimeOffset.UtcNow,
        };
        database.AgentDevices.Add(device);
        await database.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddDataProtection();
        using var serviceProvider = services.BuildServiceProvider();

        var protector = serviceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("GameNetManager.ClientDeviceIdentity");
        var cookie = protector.Protect($"{device.Id:D}|{DateTimeOffset.UtcNow.UtcTicks}");

        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        context.Request.Headers.Cookie = $"gamenet_client_device={cookie}";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.0.222");

        var resolved = await ClientExperienceEndpoints.ResolveDeviceAsync(
            context,
            database,
            CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(device.DeviceId, resolved!.DeviceId);
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
