using GameNetManager.Server.Data;
using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Hubs;

public sealed class AgentHub(
    GameNetDbContext database,
    IConfiguration configuration,
    IHubContext<DashboardHub> dashboardHub,
    ILogger<AgentHub> logger) : Hub
{
    private const string AgentDeviceContextKey = "GameNet.AgentDeviceId";
    public override async Task OnConnectedAsync()
    {
        var device = await ResolveDeviceAsync(Context, Context.ConnectionAborted);
        if (device is null || !device.IsActive)
        {
            Context.Abort();
            return;
        }

        Context.Items[AgentDeviceContextKey] = device.Id;

        var now = DateTimeOffset.UtcNow;
        device.IsOnline = true;
        device.LastSeenAt = now;
        device.ConnectedAt = now;
        device.ConnectionId = Context.ConnectionId;
        device.LastIpAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        await database.SaveChangesAsync(Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
            "AgentReady",
            new AgentReadyDto(
                device.Id,
                device.DeviceId,
                now,
                HeartbeatIntervalSeconds()),
            Context.ConnectionAborted);

        await BroadcastStatusAsync(device, now, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var device = await ResolveConnectedDeviceAsync(CancellationToken.None);
        if (device is not null && device.ConnectionId == Context.ConnectionId)
        {
            device.IsOnline = false;
            device.ConnectionId = null;
            await database.SaveChangesAsync();

            await BroadcastStatusAsync(device, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<AgentHeartbeatResponse> Heartbeat(
        AgentHeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Agent heartbeat start. ConnectionId={ConnectionId}, AgentVersion={AgentVersion}",
                Context.ConnectionId,
                request.AgentVersion);

            var device = await ResolveConnectedDeviceAsync(cancellationToken);

            logger.LogInformation(
                "Agent heartbeat identity resolved. ConnectionId={ConnectionId}, DeviceId={DeviceId}, StoredConnectionId={StoredConnectionId}",
                Context.ConnectionId,
                device?.DeviceId,
                device?.ConnectionId);

            if (device is null)
                throw new HubException("دستگاه مجاز نیست.");

            if (!device.IsActive)
                throw new HubException("دستگاه غیرفعال است.");

            var now = DateTimeOffset.UtcNow;
            device.IsOnline = true;
            device.LastSeenAt = now;
            device.AgentVersion = request.AgentVersion?.Trim();
            device.OsVersion = request.OsVersion?.Trim();
            device.CpuUsagePercent = request.CpuUsagePercent is >= 0 and <= 100
                ? request.CpuUsagePercent
                : null;
            device.MemoryAvailableBytes = request.MemoryAvailableBytes is > 0
                ? request.MemoryAvailableBytes
                : null;
            device.UptimeSeconds = request.UptimeSeconds is >= 0
                ? request.UptimeSeconds
                : null;
            device.ConnectionId = Context.ConnectionId;

            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Agent heartbeat persisted. DeviceId={DeviceId}, LastSeenAt={LastSeenAt}",
                device.DeviceId,
                now);

            try
            {
                await BroadcastStatusAsync(device, now, cancellationToken);
                logger.LogInformation(
                    "Agent heartbeat dashboard broadcast completed. DeviceId={DeviceId}",
                    device.DeviceId);
            }
            catch (Exception broadcastException)
            {
                logger.LogWarning(
                    broadcastException,
                    "Agent heartbeat persisted but Dashboard status broadcast failed for {DeviceId}.",
                    device.DeviceId);
            }

            return new AgentHeartbeatResponse(
                device.Id,
                now,
                HeartbeatIntervalSeconds(),
                true);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Agent heartbeat failed unexpectedly. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            throw new HubException("خطا در ثبت وضعیت Agent.");
        }
    }

    private async Task<AgentDevice?> ResolveDeviceAsync(
        HubCallerContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.GetHttpContext();
        var deviceId = httpContext?.Request.Headers["X-GameNet-Device-Id"].ToString().Trim();
        var authorization = httpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(deviceId)
            || string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHash = PasswordSecurity.HashToken(token);
        return await database.AgentDevices
            .Include(item => item.Station)
            .FirstOrDefaultAsync(
                item => item.DeviceId == deviceId
                    && item.AgentTokenHash == tokenHash
                    && item.IsActive,
                cancellationToken);
    }

    private async Task<AgentDevice?> ResolveConnectedDeviceAsync(CancellationToken cancellationToken)
    {
        var connectionId = Context.ConnectionId;
        if (string.IsNullOrWhiteSpace(connectionId))
            return null;

        return await database.AgentDevices
            .Include(item => item.Station)
            .FirstOrDefaultAsync(
                item => item.ConnectionId == connectionId && item.IsActive,
                cancellationToken);
    }

    private int HeartbeatIntervalSeconds()
        => Math.Clamp(configuration.GetValue("Agent:HeartbeatIntervalSeconds", 10), 3, 60);

    private int OfflineAfterSeconds()
        => Math.Clamp(
            configuration.GetValue("Agent:OfflineAfterSeconds", 30),
            HeartbeatIntervalSeconds() * 2,
            300);

    private async Task BroadcastStatusAsync(
        AgentDevice device,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dashboardHub.Clients.All.SendAsync(
            "AgentStatusChanged",
            ToStatusDto(device, now),
            cancellationToken);
    }

    private AgentStatusDto ToStatusDto(AgentDevice device, DateTimeOffset now)
    {
        var online = device.IsActive
            && device.LastSeenAt.HasValue
            && now - device.LastSeenAt.Value <= TimeSpan.FromSeconds(OfflineAfterSeconds());

        return new AgentStatusDto(
            device.Id,
            device.DeviceId,
            device.Name,
            device.StationId,
            device.Station?.Name,
            online,
            device.LastSeenAt,
            device.ConnectedAt,
            device.AgentVersion,
            device.OsVersion,
            device.CpuUsagePercent,
            device.MemoryAvailableBytes,
            device.UptimeSeconds);
    }
}
