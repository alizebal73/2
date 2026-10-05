using GameNetManager.Shared.Contracts;

namespace GameNetManager.Client.Tests;

public sealed class AgentGameProcessManagerTests
{
    [Fact]
    public async Task Stop_requires_the_same_session_and_game_owner()
    {
        await using var manager = new GameNetManager.Client.AgentGameProcessManager();

        var sessionId = Guid.NewGuid();
        var wrongSessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var commandShell = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";

        var payload = new AgentGameLaunchCommandPayload(
            gameId,
            sessionId,
            Path.GetDirectoryName(commandShell) ?? string.Empty,
            commandShell,
            "/c ping 127.0.0.1 -n 20 > nul");

        try
        {
            await manager.LaunchAsync(payload, CancellationToken.None);

            Assert.True(manager.IsRunning(sessionId, gameId));
            Assert.False(manager.IsRunning(wrongSessionId, gameId));

            await manager.StopAsync(wrongSessionId, gameId, CancellationToken.None);

            Assert.True(manager.IsRunning(sessionId, gameId));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.LaunchAsync(payload, CancellationToken.None));

            await manager.StopAsync(sessionId, gameId, CancellationToken.None);

            Assert.False(manager.IsRunning(sessionId, gameId));
        }
        finally
        {
            await manager.StopAllAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StopAll_stops_the_active_game_process()
    {
        await using var manager = new GameNetManager.Client.AgentGameProcessManager();

        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var commandShell = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";

        var payload = new AgentGameLaunchCommandPayload(
            gameId,
            sessionId,
            Path.GetDirectoryName(commandShell) ?? string.Empty,
            commandShell,
            "/c ping 127.0.0.1 -n 20 > nul");

        try
        {
            await manager.LaunchAsync(payload, CancellationToken.None);
            Assert.True(manager.IsRunning(sessionId, gameId));

            await manager.StopAllAsync(CancellationToken.None);

            Assert.False(manager.IsRunning(sessionId, gameId));
        }
        finally
        {
            await manager.StopAllAsync(CancellationToken.None);
        }
    }
}
