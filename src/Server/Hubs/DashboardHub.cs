using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace GameNetManager.Server.Hubs;

public sealed class DashboardHub(IWebHostEnvironment environment) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(
            "ServerReady",
            new ServerInfoDto("GameNet Manager", environment.EnvironmentName, DateTimeOffset.UtcNow));
        await base.OnConnectedAsync();
    }
}