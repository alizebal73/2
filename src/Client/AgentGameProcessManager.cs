using System.Diagnostics;
using System.Collections.Concurrent;
using GameNetManager.Shared.Contracts;

namespace GameNetManager.Client;

public sealed class AgentGameProcessManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<GameProcessKey, Process> runningGames = new();

    public bool IsRunning(Guid sessionId, Guid gameId)
        => runningGames.TryGetValue(new GameProcessKey(sessionId, gameId), out var process)
            && !process.HasExited;

    public Task LaunchAsync(
        AgentGameLaunchCommandPayload payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (payload.GameId == Guid.Empty || payload.SessionId == Guid.Empty)
            throw new InvalidOperationException("شناسه بازی یا Session معتبر نیست.");

        var executablePath = Path.IsPathRooted(payload.Executable)
            ? Path.GetFullPath(payload.Executable)
            : Path.GetFullPath(Path.Combine(payload.Path, payload.Executable));

        if (!File.Exists(executablePath))
            throw new FileNotFoundException("فایل اجرایی بازی روی این Client پیدا نشد.", executablePath);

        foreach (var existing in runningGames.ToArray())
        {
            if (existing.Value.HasExited)
            {
                if (runningGames.TryRemove(existing.Key, out var exited))
                    exited.Dispose();
                continue;
            }

            if (existing.Key.SessionId == payload.SessionId && existing.Key.GameId == payload.GameId)
                throw new InvalidOperationException("این بازی در حال اجراست.");

            throw new InvalidOperationException("بازی دیگری روی این Client در حال اجراست.");
        }

        var workingDirectory = Path.GetDirectoryName(executablePath) ?? Path.GetFullPath(payload.Path);
        var key = new GameProcessKey(payload.SessionId, payload.GameId);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = payload.LaunchArgs ?? string.Empty,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            },
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("فرآیند بازی روی Client اجرا نشد.");
        }

        if (!runningGames.TryAdd(key, process))
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            process.Dispose();
            throw new InvalidOperationException("بازی دیگری روی این Client هم‌زمان در حال اجراست.");
        }

        process.Exited += (_, _) =>
        {
            if (runningGames.TryRemove(key, out var exited))
                exited.Dispose();
        };

        return Task.CompletedTask;
    }

    public async Task StopAsync(
        Guid sessionId,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = new GameProcessKey(sessionId, gameId);
        if (!runningGames.TryRemove(key, out var process))
            return;

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.CloseMainWindow();
                    await Task.WhenAny(
                        process.WaitForExitAsync(cancellationToken),
                        Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
                }
                catch
                {
                    // Fall back to a hard stop below.
                }

                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }

            if (!process.HasExited)
                await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            process.Dispose();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        var keys = runningGames.Keys.ToArray();
        foreach (var key in keys)
            await StopAsync(key.SessionId, key.GameId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAllAsync(CancellationToken.None);
        }
        catch
        {
            // Agent shutdown must not be blocked by a child process.
        }
    }

    private readonly record struct GameProcessKey(Guid SessionId, Guid GameId);
}
