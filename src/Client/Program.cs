using GameNetManager.Client;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

if (args.Length > 0 && string.Equals(args[0], "--gamenet-update-watchdog", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = await RunUpdateWatchdogAsync(args.Skip(1).ToArray());
    return;
}

const string defaultServerUrl = "http://localhost:5080";
var serverUrl = Environment.GetEnvironmentVariable("GAMENET_SERVER_URL") ?? defaultServerUrl;
var registrationToken = Environment.GetEnvironmentVariable("GAMENET_AGENT_REGISTRATION_TOKEN");
var stationText = Environment.GetEnvironmentVariable("GAMENET_STATION_ID");
var configuredName = Environment.GetEnvironmentVariable("GAMENET_AGENT_NAME");
var configuredDeviceId = Environment.GetEnvironmentVariable("GAMENET_AGENT_DEVICE_ID");
var dataDirectory = Environment.GetEnvironmentVariable("GAMENET_AGENT_DATA_DIR");
var testSessionFlow = string.Equals(Environment.GetEnvironmentVariable("GAMENET_AGENT_TEST_SESSION_FLOW"), "1", StringComparison.Ordinal);
var testSessionCustomerId = Environment.GetEnvironmentVariable("GAMENET_AGENT_TEST_CUSTOMER_ID");
var testSessionLoginId = Environment.GetEnvironmentVariable("GAMENET_AGENT_TEST_LOGIN_ID");

if (string.IsNullOrWhiteSpace(dataDirectory))
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GameNetManager",
        "Agent");

Directory.CreateDirectory(dataDirectory);
var statePath = Path.Combine(dataDirectory, "agent-state.json");
var state = await LoadStateAsync(statePath);
var testSessionFlowCompleted = false;
if (!ClientLifecycleStates.IsKnown(state.LifecycleState))
    state = state with { LifecycleState = ClientLifecycleStates.Starting };

var name = string.IsNullOrWhiteSpace(configuredName)
    ? Environment.MachineName
    : configuredName.Trim();

var stationId = state.StationId;
if (Guid.TryParse(stationText, out var parsedStationId))
    stationId = parsedStationId;

var agentVersion = await ResolveAgentVersionAsync(AppContext.BaseDirectory);
var osVersion = Environment.OSVersion.VersionString;

Console.WriteLine(
    $"پیکربندی Agent: Server={serverUrl}; DeviceId={state.DeviceId}; Name={state.Name}; StationId={stationId?.ToString() ?? "none"}");

using var shutdown = new CancellationTokenSource();
using var lockScreen = new AgentLockScreenController();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/")
};
httpClient.Timeout = TimeSpan.FromSeconds(110);
var updateManager = new ClientUpdateManager(httpClient, dataDirectory, statePath);

try
{
    if (string.IsNullOrWhiteSpace(state.DeviceId))
        state = state with { DeviceId = string.IsNullOrWhiteSpace(configuredDeviceId) ? Guid.NewGuid().ToString("N") : configuredDeviceId.Trim() };

    state = state with
    {
        Name = name,
        StationId = stationId,
        LifecycleState = ClientLifecycleStates.Starting
    };
    await SaveStateAsync(statePath, state);

    if (string.IsNullOrWhiteSpace(state.AgentToken))
    {
        if (string.IsNullOrWhiteSpace(registrationToken))
            throw new InvalidOperationException("توکن ثبت اولیه Agent در تنظیمات سیستم وارد نشده است.");

        state = await RegisterAgentAsync(
            httpClient,
            state,
            registrationToken,
            agentVersion,
            osVersion,
            shutdown.Token);

        await SaveStateAsync(statePath, state);
        Console.WriteLine($"Agent با شناسه {state.DeviceId} در سرور ثبت شد.");
    }
    else
    {
        await SaveStateAsync(statePath, state);
    }

    httpClient.DefaultRequestHeaders.Remove("X-GameNet-Device-Id");
    httpClient.DefaultRequestHeaders.Remove("Authorization");
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-GameNet-Device-Id", state.DeviceId);
    httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", state.AgentToken);

    var hubUrl = $"{serverUrl.TrimEnd('/')}/hubs/agent";

    while (!shutdown.IsCancellationRequested)
    {
        await using var connection = CreateConnection(hubUrl, state);
        var kioskEnabled = false;
        var lockOnDisconnect = true;

        connection.On<AgentCommandEnvelope>("AgentCommand", async command =>
        {
            var outcome = await HandleAgentCommandAsync(
                connection,
                command,
                lockScreen,
                updateManager,
                agentVersion,
                shutdown.Token);

            if (outcome.AwaitingFinalResult)
            {
                state = state with
                {
                    LifecycleState = command.CommandType == AgentCommandTypes.Update
                        ? ClientLifecycleStates.Updating
                        : ClientLifecycleStates.Recovering,
                    PendingUpdateVersion = command.CommandType == AgentCommandTypes.Update
                        ? outcome.RestartVersion
                        : null,
                    LastUpdateError = null,
                    PendingCommandId = command.CommandId,
                    PendingCommandType = command.CommandType,
                    PendingCommandTargetVersion = outcome.RestartVersion,
                    PendingCommandOutcome = null
                };
                await SaveStateAsync(statePath, state);
            }
            else if (outcome.PendingUpdateVersion is not null)
            {
                state = state with
                {
                    LifecycleState = outcome.RequiresRestart
                        ? ClientLifecycleStates.Updating
                        : ClientLifecycleStates.UpdatePending,
                    PendingUpdateVersion = outcome.PendingUpdateVersion,
                    LastUpdateError = outcome.Error
                };
                await SaveStateAsync(statePath, state);
            }
            else if (outcome.RollbackVersion is not null)
            {
                state = state with
                {
                    LifecycleState = outcome.RequiresRestart
                        ? ClientLifecycleStates.Recovering
                        : ClientLifecycleStates.Degraded,
                    PendingUpdateVersion = null,
                    LastUpdateError = outcome.Error
                };
                await SaveStateAsync(statePath, state);
            }
            else if (!outcome.Success
                && command.CommandType is AgentCommandTypes.Update or AgentCommandTypes.Rollback)
            {
                state = state with
                {
                    LifecycleState = ClientLifecycleStates.Failed,
                    PendingUpdateVersion = null,
                    PendingCommandId = null,
                    PendingCommandType = null,
                    PendingCommandTargetVersion = null,
                    PendingCommandOutcome = null,
                    LastUpdateError = outcome.Error ?? outcome.Message
                };
                await SaveStateAsync(statePath, state);
            }

            if (outcome.RequiresRestart && outcome.RestartVersion is not null)
            {
                await LaunchUpdateWatchdogAsync(dataDirectory, outcome.RestartVersion, Environment.ProcessId);
                shutdown.Cancel();
            }
        });

        connection.On<AgentPolicyDto>("AgentPolicyChanged", policy =>
        {
            kioskEnabled = policy.KioskEnabled;
            lockOnDisconnect = policy.LockOnDisconnect;
            Console.WriteLine($"Policy Agent تغییر کرد؛ Kiosk={kioskEnabled}; LockOnDisconnect={lockOnDisconnect}.");
            return Task.CompletedTask;
        });

        connection.On<AgentReadyDto>("AgentReady", async ready =>
        {
            kioskEnabled = ready.KioskEnabled;
            lockOnDisconnect = ready.LockOnDisconnect;

            if (ready.IsLocked)
                await lockScreen.LockAsync(shutdown.Token);
            else
                await lockScreen.UnlockAsync();

            state = state with
            {
                LifecycleState = ClientLifecycleStates.Running,
                LastUpdateError = null,
                LastHealthyAt = DateTimeOffset.UtcNow
            };
            await updateManager.MarkHealthyAsync(agentVersion, shutdown.Token);
            await SaveStateAsync(statePath, state);

            Console.WriteLine(
                $"Agent متصل شد؛ شناسه سرور: {ready.AgentId}; زمان سرور: {ready.ServerUtcNow:O}; قفل={ready.IsLocked}; Kiosk={kioskEnabled}; LockOnDisconnect={lockOnDisconnect}");

            if (!testSessionFlowCompleted
                && testSessionFlow
                && Guid.TryParse(testSessionCustomerId, out var testCustomerId)
                && Guid.TryParse(testSessionLoginId, out var testLoginId))
            {
                testSessionFlowCompleted = true;
                _ = RunTestSessionFlowAsync(
                    connection,
                    testCustomerId,
                    testLoginId,
                    shutdown.Token);
            }
        });

        connection.Reconnecting += error =>
        {
            Console.WriteLine(
                $"ارتباط Agent با سرور قطع شد؛ تلاش برای اتصال مجدد. {error?.Message ?? string.Empty}".Trim());
            return Task.CompletedTask;
        };

        connection.Reconnected += async connectionId =>
        {
            state = state with { LifecycleState = ClientLifecycleStates.Recovering };
            await SaveStateAsync(statePath, state);
            Console.WriteLine($"Agent دوباره متصل شد ({connectionId}).");
            var heartbeat = await SendHeartbeatAsync(connection, agentVersion, osVersion, lockScreen, state, shutdown.Token);
            if (heartbeat.HasValue)
            {
                state = state with
                {
                    LifecycleState = ClientLifecycleStates.Running,
                    LastUpdateError = null,
                    LastHealthyAt = DateTimeOffset.UtcNow
                };
                await updateManager.MarkHealthyAsync(agentVersion, shutdown.Token);
                await SaveStateAsync(statePath, state);
            }
        };

        connection.Closed += async error =>
        {
            if (kioskEnabled && lockOnDisconnect && !shutdown.IsCancellationRequested)
            {
                try
                {
                    await lockScreen.LockAsync(CancellationToken.None);
                    Console.WriteLine("ارتباط Agent قطع شد؛ طبق Policy صفحه قفل شد.");
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"قفل امن هنگام قطع ارتباط انجام نشد: {exception.Message}");
                }
            }

            if (!shutdown.IsCancellationRequested)
                Console.WriteLine(
                    $"اتصال Agent بسته شد؛ چرخهٔ اتصال دوباره شروع می‌شود. {error?.Message ?? "علت نامشخص"}".Trim());
        };

        try
        {
            await connection.StartAsync(shutdown.Token);
            Console.WriteLine($"Agent GameNet روی {hubUrl} فعال شد.");

            while (!shutdown.IsCancellationRequested
                && connection.State != HubConnectionState.Disconnected)
            {
                var heartbeatSeconds = await SendHeartbeatAsync(
                    connection,
                    agentVersion,
                    osVersion,
                    lockScreen,
                    state,
                    shutdown.Token);

                if (heartbeatSeconds.HasValue)
                {
                    await updateManager.MarkHealthyAsync(agentVersion, shutdown.Token);
                    state = state with
                    {
                        LifecycleState = ClientLifecycleStates.Running,
                        LastUpdateError = null,
                        LastHealthyAt = DateTimeOffset.UtcNow
                    };
                    await SaveStateAsync(statePath, state);
                }

                var delaySeconds = Math.Clamp(heartbeatSeconds ?? 10, 3, 60);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), shutdown.Token);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            break;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"چرخهٔ اتصال Agent با خطا پایان یافت: {exception.Message}");
        }
        finally
        {
            if (connection.State != HubConnectionState.Disconnected)
            {
                try
                {
                    await connection.StopAsync();
                }
                catch
                {
                    // The next cycle will create a fresh connection.
                }
            }
        }

        if (!shutdown.IsCancellationRequested)
        {
            state = state with { LifecycleState = lockOnDisconnect ? ClientLifecycleStates.Degraded : ClientLifecycleStates.Recovering };
            await SaveStateAsync(statePath, state);
            Console.WriteLine("Agent در وضعیت آفلاین است؛ ۵ ثانیه بعد اتصال دوباره امتحان می‌شود.");
            await Task.Delay(TimeSpan.FromSeconds(5), shutdown.Token);
        }
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    Console.WriteLine("Agent متوقف شد.");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"اجرای Agent با خطا متوقف شد: {exception.Message}");
    Environment.ExitCode = 1;
}

static HubConnection CreateConnection(string hubUrl, AgentState state)
{
    return new HubConnectionBuilder()
        .WithUrl(hubUrl, options =>
        {
            options.Headers["X-GameNet-Device-Id"] = state.DeviceId;
            options.AccessTokenProvider = () => Task.FromResult<string?>(state.AgentToken);
        })
        .WithAutomaticReconnect(new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
        })
        .Build();
}

static async Task<AgentState> RegisterAgentAsync(
    HttpClient httpClient,
    AgentState state,
    string registrationToken,
    string agentVersion,
    string osVersion,
    CancellationToken cancellationToken)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "api/agent/register");
    request.Headers.Add("X-GameNet-Registration-Token", registrationToken);
    request.Content = JsonContent.Create(new AgentRegistrationRequest(
        state.DeviceId,
        state.Name,
        state.StationId,
        agentVersion,
        osVersion));

    using var response = await httpClient.SendAsync(request, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"ثبت Agent در سرور با کد {(int)response.StatusCode} رد شد. {responseBody}");
    }

    var registration = await response.Content.ReadFromJsonAsync<AgentRegistrationResponse>(
        cancellationToken: cancellationToken)
        ?? throw new InvalidOperationException("پاسخ ثبت Agent از سرور نامعتبر بود.");

    return state with { AgentToken = registration.AgentToken };
}

static async Task RunTestSessionFlowAsync(
    HubConnection connection,
    Guid customerId,
    Guid customerLoginId,
    CancellationToken cancellationToken)
{
    try
    {
        var started = await connection.InvokeAsync<AgentSessionStartResponse>(
            "StartSession",
            new AgentSessionStartRequest(
                customerId,
                customerLoginId,
                Guid.NewGuid(),
                1m,
                1),
            cancellationToken);

        Console.WriteLine($"Agent session start موفق؛ SessionId={started.SessionId}.");
        Console.WriteLine($"AGENT_SESSION_START_OK:{started.SessionId}");

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        try
        {
            await connection.InvokeAsync<AgentSessionEndResponse>(
                "EndSession",
                new AgentSessionEndRequest(started.SessionId, null),
                cancellationToken);
            throw new InvalidOperationException("Agent session end without CustomerLoginId unexpectedly succeeded.");
        }
        catch (HubException)
        {
            Console.WriteLine("AGENT_SESSION_END_AUTH_GUARD_OK");
        }

        var ended = await connection.InvokeAsync<AgentSessionEndResponse>(
            "EndSession",
            new AgentSessionEndRequest(
                started.SessionId,
                customerLoginId),
            cancellationToken);

        Console.WriteLine($"Agent session end موفق؛ SessionId={ended.SessionId}; EndAt={ended.EndAt:O}.");
        Console.WriteLine($"AGENT_SESSION_END_OK:{ended.SessionId}");
    }
    catch (Exception exception) when (
        exception is HubException or HttpRequestException or InvalidOperationException)
    {
        Console.WriteLine($"چرخهٔ آزمایشی Session Agent ناموفق بود: {exception.Message}");
    }
}

static async Task<int?> SendHeartbeatAsync(
    HubConnection connection,
    string agentVersion,
    string osVersion,
    AgentLockScreenController lockScreen,
    AgentState state,
    CancellationToken cancellationToken)
{
    if (connection.State != HubConnectionState.Connected)
        return null;

    try
    {
        var response = await connection.InvokeAsync<AgentHeartbeatResponse>(
            "Heartbeat",
            new AgentHeartbeatRequest(
                agentVersion,
                osVersion,
                null,
                GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                Environment.TickCount64 / 1000,
                lockScreen.IsLocked,
                state.LifecycleState,
                state.PendingUpdateVersion,
                state.LastUpdateError));

        Console.WriteLine($"Heartbeat موفق؛ زمان سرور: {response.ServerUtcNow:HH:mm:ss}.");
        return response.HeartbeatIntervalSeconds;
    }
    catch (Exception exception) when (
        exception is HubException or HttpRequestException or InvalidOperationException)
    {
        Console.WriteLine($"Heartbeat ارسال نشد؛ وضعیت اتصال دوباره بررسی می‌شود. {exception.Message}");
        return null;
    }
}

static async Task<AgentState> LoadStateAsync(string path)
{
    if (!File.Exists(path))
        return new AgentState(string.Empty, string.Empty, string.Empty, null);

    try
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<AgentState>(json)
            ?? new AgentState(string.Empty, string.Empty, string.Empty, null);
    }
    catch (Exception exception)
    {
        Console.WriteLine($"فایل وضعیت Agent خوانده نشد؛ Agent با هویت جدید/قابل‌بازیابی ادامه می‌دهد: {exception.Message}");
        return new AgentState(string.Empty, string.Empty, string.Empty, null);
    }
}

static async Task SaveStateAsync(string path, AgentState state)
{
    var json = JsonSerializer.Serialize(
        state,
        new JsonSerializerOptions { WriteIndented = true });

    var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    await File.WriteAllTextAsync(tempPath, json);

    try
    {
        File.Move(tempPath, path, overwrite: true);
    }
    catch
    {
        try
        {
            File.Delete(tempPath);
        }
        catch
        {
            // Preserve the original state file when replacement cannot complete.
        }

        throw;
    }
}
static async Task<AgentCommandExecutionOutcome> HandleAgentCommandAsync(
    HubConnection connection,
    AgentCommandEnvelope command,
    AgentLockScreenController lockScreen,
    ClientUpdateManager updateManager,
    string agentVersion,
    CancellationToken cancellationToken)
{
    var success = AgentCommandTypes.IsSupported(command.CommandType);
    var message = success
        ? "Agent فرمان را دریافت کرد."
        : "فرمان Agent ناشناخته است.";

    string? pendingUpdateVersion = null;
    string? rollbackVersion = null;
    string? restartVersion = null;
    string? error = null;

    try
    {
        switch (command.CommandType.Trim().ToLowerInvariant())
        {
            case AgentCommandTypes.Ping:
                Console.WriteLine($"فرمان ping دریافت شد؛ CommandId={command.CommandId}.");
                message = "ارتباط Agent سالم است.";
                break;

            case AgentCommandTypes.Lock:
                await lockScreen.LockAsync(cancellationToken);
                Console.WriteLine($"فرمان قفل دریافت شد؛ CommandId={command.CommandId}.");
                message = "صفحه قفل GameNet فعال شد.";
                break;

            case AgentCommandTypes.Unlock:
                await lockScreen.UnlockAsync();
                Console.WriteLine($"فرمان بازگشایی دریافت شد؛ CommandId={command.CommandId}.");
                message = "صفحه قفل GameNet باز شد.";
                break;

            case AgentCommandTypes.LogoutLock:
                await connection.InvokeAsync("AgentLogoutAndLock", cancellationToken);
                await lockScreen.LockAsync(cancellationToken);
                Console.WriteLine($"فرمان خروج کاربر و قفل دریافت شد؛ CommandId={command.CommandId}.");
                message = "کاربر خارج شد و دستگاه قفل شد.";
                break;

            case AgentCommandTypes.Update:
            {
                var payload = ClientUpdateCommandParser.Parse(command.PayloadJson);

                Console.WriteLine($"CLIENT_UPDATE_SNAPSHOT_START:{agentVersion}");
                await updateManager.EnsureCurrentVersionSnapshotAsync(
                    agentVersion,
                    AppContext.BaseDirectory,
                    cancellationToken);
                Console.WriteLine($"CLIENT_UPDATE_SNAPSHOT_DONE:{agentVersion}");

                var package = new ClientUpdatePackage(
                    payload.Version,
                    payload.PackageUrl,
                    payload.Sha256,
                    payload.SizeBytes);

                Console.WriteLine($"CLIENT_UPDATE_STAGE_START:{payload.Version}");
                await updateManager.StageAsync(package, cancellationToken);
                pendingUpdateVersion = payload.Version;
                Console.WriteLine($"CLIENT_UPDATE_STAGE_DONE:{payload.Version}");

                if (payload.Activate)
                {
                    await updateManager.ActivateAsync(payload.Version, agentVersion, cancellationToken);
                    restartVersion = payload.Version;
                    message = $"نسخه {payload.Version} دریافت و برای فعال‌سازی آماده شد؛ راه‌اندازی مجدد کنترل‌شده آغاز می‌شود.";
                }
                else
                {
                    message = $"نسخه {payload.Version} دریافت و در محل نسخه‌ای ذخیره شد.";
                }

                Console.WriteLine($"CLIENT_UPDATE_STAGED:{payload.Version}");
                if (payload.Activate)
                    Console.WriteLine($"CLIENT_UPDATE_RESTART_REQUESTED:{payload.Version}");
                break;
            }

            case AgentCommandTypes.Rollback:
                rollbackVersion = await updateManager.RollbackAsync(cancellationToken);
                restartVersion = rollbackVersion;
                message = $"Rollback به نسخه {rollbackVersion} آماده شد؛ راه‌اندازی مجدد کنترل‌شده آغاز می‌شود.";
                Console.WriteLine($"CLIENT_ROLLBACK_REQUESTED:{rollbackVersion}");
                break;

            default:
                success = false;
                message = "فرمان Agent ناشناخته است.";
                break;
        }
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        success = false;
        error = "زمان اجرای فرمان Agent تمام شد.";
        message = error;
        Console.WriteLine($"اجرای فرمان Agent به‌دلیل پایان زمان ناموفق بود: {message}");
    }
    catch (Exception exception) when (
        exception is HubException
            or HttpRequestException
            or InvalidOperationException
            or ArgumentException
            or JsonException
            or ObjectDisposedException
            or IOException)
    {
        success = false;
        error = exception.Message;
        message = exception.Message;
        Console.WriteLine($"اجرای فرمان Agent ناموفق بود: {message}");
    }

    try
    {
        await connection.InvokeAsync(
            "AcknowledgeCommand",
            new AgentCommandAcknowledgement(
                command.CommandId,
                success,
                message,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }
    catch (Exception exception) when (
        exception is HubException or HttpRequestException or InvalidOperationException)
    {
        Console.WriteLine($"پاسخ فرمان Agent ارسال نشد: {exception.Message}");
    }

    return new AgentCommandExecutionOutcome(
        success,
        message,
        error,
        pendingUpdateVersion,
        rollbackVersion,
        restartVersion,
        restartVersion is not null);
}

static async Task LaunchUpdateWatchdogAsync(string dataDirectory, string targetVersion, int parentProcessId)
{
    var entryPoint = Assembly.GetEntryAssembly()?.Location;
    var processPath = Environment.ProcessPath
        ?? throw new InvalidOperationException("مسیر اجرای Agent پیدا نشد.");

    var startInfo = new ProcessStartInfo
    {
        FileName = processPath,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    if (!string.IsNullOrWhiteSpace(entryPoint)
        && entryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        startInfo.ArgumentList.Add(entryPoint);

    startInfo.ArgumentList.Add("--gamenet-update-watchdog");
    startInfo.ArgumentList.Add(dataDirectory);
    startInfo.ArgumentList.Add(targetVersion);
    startInfo.ArgumentList.Add(parentProcessId.ToString());
    startInfo.Environment["GAMENET_AGENT_DATA_DIR"] = dataDirectory;

    var watchdog = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Watchdog به‌روزرسانی اجرا نشد.");

    Console.WriteLine($"CLIENT_UPDATE_WATCHDOG_STARTED:{watchdog.Id}:{targetVersion}:PARENT={parentProcessId}");
}

static async Task<string> ResolveAgentVersionAsync(string installRoot)
{
    var marker = Path.Combine(installRoot, "client-version.txt");
    if (File.Exists(marker))
    {
        var value = (await File.ReadAllTextAsync(marker)).Trim();
        if (Version.TryParse(value, out _))
            return value;
    }

    return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
}

static async Task<int> RunUpdateWatchdogAsync(string[] arguments)
{
    if (arguments.Length < 2)
        return 2;

    var dataDirectory = arguments[0];
    var targetVersion = arguments[1];
    var parentProcessId = 0;
    if (arguments.Length >= 3)
        _ = int.TryParse(arguments[2], out parentProcessId);

    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    using var httpClient = new HttpClient();
    var statePath = Path.Combine(dataDirectory, "agent-state.json");
    var manager = new ClientUpdateManager(httpClient, dataDirectory, statePath);

    Process? child = null;

    try
    {
        if (parentProcessId > 0)
            await WaitForParentProcessExitAsync(parentProcessId, cancellation.Token);
        else
            await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);

        var baselineHealthyAt = await ReadAgentLastHealthyAtAsync(dataDirectory, cancellation.Token);
        child = StartVersionProcess(manager, dataDirectory, targetVersion);

        var healthy = await WaitForFreshHealthyVersionAsync(
            manager,
            dataDirectory,
            targetVersion,
            baselineHealthyAt,
            child,
            cancellation.Token);

        if (healthy)
        {
            await manager.CommitHealthyAsync(targetVersion, cancellation.Token);
            return 0;
        }

        TryTerminateProcess(child);

        var failedState = await manager.GetStateAsync(cancellation.Token);
        if (!string.Equals(failedState?.ActiveVersion, targetVersion, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(failedState?.PreviousVersion))
        {
            return 1;
        }

        var rollbackBaselineHealthyAt = await ReadAgentLastHealthyAtAsync(dataDirectory, cancellation.Token);
        var rollbackVersion = await manager.RollbackAsync(cancellation.Token);
        var rollbackChild = StartVersionProcess(manager, dataDirectory, rollbackVersion);

        var rollbackHealthy = await WaitForFreshHealthyVersionAsync(
            manager,
            dataDirectory,
            rollbackVersion,
            rollbackBaselineHealthyAt,
            rollbackChild,
            cancellation.Token);

        if (rollbackHealthy)
        {
            await manager.CommitHealthyAsync(rollbackVersion, cancellation.Token);
            return 1;
        }

        TryTerminateProcess(rollbackChild);
        return 2;
    }
    catch
    {
        TryTerminateProcess(child);
        return 1;
    }
}

static async Task WaitForParentProcessExitAsync(int parentProcessId, CancellationToken cancellationToken)
{
    for (var i = 0; i < 30; i++)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var parent = Process.GetProcessById(parentProcessId);
            if (parent.HasExited)
                return;
        }
        catch (ArgumentException)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }

    throw new TimeoutException("Agent قبلی قبل از شروع نسخهٔ جدید متوقف نشد.");
}

static Process StartVersionProcess(
    ClientUpdateManager manager,
    string dataDirectory,
    string targetVersion)
{
    var targetRoot = Path.Combine(manager.VersionsDirectory, targetVersion);
    var targetAssembly = Path.Combine(targetRoot, "GameNetManager.Client.dll");
    var targetExe = Path.Combine(targetRoot, "GameNetManager.Client.exe");
    var processPath = Environment.ProcessPath ?? "dotnet";
    var isDotnetHost = string.Equals(
        Path.GetFileNameWithoutExtension(processPath),
        "dotnet",
        StringComparison.OrdinalIgnoreCase);

    Process? process;
    if (isDotnetHost && File.Exists(targetAssembly))
    {
        var stdoutPath = Path.Combine(dataDirectory, $"watchdog-{targetVersion}-stdout.log");
        var stderrPath = Path.Combine(dataDirectory, $"watchdog-{targetVersion}-stderr.log");

        process = Process.Start(new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = targetRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { targetAssembly },
            Environment = { ["GAMENET_AGENT_DATA_DIR"] = dataDirectory, ["GAMENET_UPDATE_TARGET_VERSION"] = targetVersion },
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await using var stdout = new FileStream(stdoutPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                await using var stderr = new FileStream(stderrPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                await Task.WhenAll(
                    process!.StandardOutput.BaseStream.CopyToAsync(stdout),
                    process.StandardError.BaseStream.CopyToAsync(stderr));
            }
            catch
            {
            }
        });
    }
    else if (File.Exists(targetExe))
    {
        process = Process.Start(new ProcessStartInfo
        {
            FileName = targetExe,
            WorkingDirectory = targetRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = { ["GAMENET_AGENT_DATA_DIR"] = dataDirectory, ["GAMENET_UPDATE_TARGET_VERSION"] = targetVersion }
        });
    }
    else if (File.Exists(targetAssembly))
    {
        process = Process.Start(new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = targetRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { targetAssembly },
            Environment = { ["GAMENET_AGENT_DATA_DIR"] = dataDirectory, ["GAMENET_UPDATE_TARGET_VERSION"] = targetVersion }
        });
    }
    else
    {
        throw new FileNotFoundException("فایل اجرایی نسخهٔ هدف پیدا نشد.", targetRoot);
    }

    return process ?? throw new InvalidOperationException("نسخهٔ هدف اجرا نشد.");
}

static async Task<bool> WaitForFreshHealthyVersionAsync(
    ClientUpdateManager manager,
    string dataDirectory,
    string targetVersion,
    DateTimeOffset? baselineHealthyAt,
    Process child,
    CancellationToken cancellationToken)
{
    for (var i = 0; i < 60; i++)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = await manager.GetStateAsync(cancellationToken);
        var agentState = await ReadAgentStateAsync(dataDirectory, cancellationToken);

        var freshHealth =
            agentState?.LastHealthyAt.HasValue == true
            && (!baselineHealthyAt.HasValue || agentState.LastHealthyAt.Value > baselineHealthyAt.Value);

        if (string.Equals(status?.ActiveVersion, targetVersion, StringComparison.OrdinalIgnoreCase)
            && string.Equals(status?.HealthyVersion, targetVersion, StringComparison.OrdinalIgnoreCase)
            && freshHealth)
        {
            return true;
        }

        if (child.HasExited)
            break;

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }

    return false;
}

static async Task<DateTimeOffset?> ReadAgentLastHealthyAtAsync(
    string dataDirectory,
    CancellationToken cancellationToken)
{
    var state = await ReadAgentStateAsync(dataDirectory, cancellationToken);
    return state?.LastHealthyAt;
}

static async Task<AgentState?> ReadAgentStateAsync(
    string dataDirectory,
    CancellationToken cancellationToken)
{
    var path = Path.Combine(dataDirectory, "agent-state.json");
    if (!File.Exists(path))
        return null;

    try
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AgentState>(stream, cancellationToken: cancellationToken);
    }
    catch
    {
        return null;
    }
}

static void TryTerminateProcess(Process? process)
{
    if (process is null)
        return;

    try
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
    }
    catch
    {
    }
}


record AgentState(
    string DeviceId,
    string Name,
    string AgentToken,
    Guid? StationId,
    string LifecycleState = ClientLifecycleStates.Starting,
    string? PendingUpdateVersion = null,
    string? LastUpdateError = null,
    DateTimeOffset? LastHealthyAt = null);

record AgentCommandExecutionOutcome(
    bool Success,
    string Message,
    string? Error,
    string? PendingUpdateVersion,
    string? RollbackVersion,
    string? RestartVersion,
    bool RequiresRestart);
