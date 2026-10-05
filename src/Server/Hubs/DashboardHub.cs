using GameNetManager.Server.Data;
using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace GameNetManager.Server.Hubs;

public sealed class DashboardHub(
    IWebHostEnvironment environment,
    GameNetDbContext database) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null)
        {
            Context.Abort();
            return;
        }

        var user = await AuthorizationService.ResolveUserAsync(
            httpContext,
            database,
            Context.ConnectionAborted);

        if (user is null)
        {
            Context.Abort();
            return;
        }

        Context.Items["GameNet.DashboardUserId"] = user.Id;

        await Clients.Caller.SendAsync(
            "ServerReady",
            new ServerInfoDto("GameNet Manager", environment.EnvironmentName, DateTimeOffset.UtcNow));
        await base.OnConnectedAsync();
    }
}