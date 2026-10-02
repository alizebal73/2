using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

const string defaultServerUrl = "http://localhost:5080";
var serverUrl = Environment.GetEnvironmentVariable("GAMENET_SERVER_URL") ?? defaultServerUrl;
var registrationToken = Environment.GetEnvironmentVariable("GAMENET_AGENT_REGISTRATION_TOKEN");
var stationText = Environment.GetEnvironmentVariable("GAMENET_STATION_ID");
var configuredName = Environment.GetEnvironmentVariable("GAMENET_AGENT_NAME");
var dataDirectory = Environment.GetEnvironmentVariable("GAMENET_AGENT_DATA_DIR");

if (string.IsNullOrWhiteSpace(dataDirectory))
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GameNetManager",
        "Agent");

Directory.CreateDirectory(dataDirectory);
var statePath = Path.Combine(dataDirectory, "agent-state.json");
var state = await LoadStateAsync(statePath);

var name = string.IsNullOrWhiteSpace(configuredName)
    ? Environment.MachineName
    : configuredName.Trim();

var stationId = state.StationId;
if (Guid.TryParse(stationText, out var parsedStationId))
    stationId = parsedStationId;

var agentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
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

try
{
    if (string.IsNullOrWhiteSpace(state.DeviceId))
        state = state with { DeviceId = Guid.NewGuid().ToString("N") };

    state = state with
    {
        Name = name,
        StationId = stationId
    };

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

    var hubUrl = $"{serverUrl.TrimEnd('/')}/hubs/agent";

    while (!shutdown.IsCancellationRequested)
    {
        await using var connection = CreateConnection(hubUrl, state);

        connection.On<AgentCommandEnvelope>("AgentCommand", command =>
            HandleAgentCommandAsync(connection, command, lockScreen, shutdown.Token));

        connection.On<AgentReadyDto>("AgentReady", async ready =>
        {
            if (ready.IsLocked)
                await lockScreen.LockAsync(shutdown.Token);
            else
                await lockScreen.UnlockAsync();

            Console.WriteLine(
                $"Agent متصل شد؛ شناسه سرور: {ready.AgentId}; زمان سرور: {ready.ServerUtcNow:O}; قفل={ready.IsLocked}");
        });

        connection.Reconnecting += error =>
        {
            Console.WriteLine(
                $"ارتباط Agent با سرور قطع شد؛ تلاش برای اتصال مجدد. {error?.Message ?? string.Empty}".Trim());
            return Task.CompletedTask;
        };

        connection.Reconnected += async connectionId =>
        {
            Console.WriteLine($"Agent دوباره متصل شد ({connectionId}).");
            await SendHeartbeatAsync(connection, agentVersion, osVersion, lockScreen, shutdown.Token);
        };

        connection.Closed += error =>
        {
            if (!shutdown.IsCancellationRequested)
                Console.WriteLine(
                    $"اتصال Agent بسته شد؛ چرخهٔ اتصال دوباره شروع می‌شود. {error?.Message ?? "علت نامشخص"}".Trim());
            return Task.CompletedTask;
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
                    shutdown.Token);

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

static async Task<int?> SendHeartbeatAsync(
    HubConnection connection,
    string agentVersion,
    string osVersion,
    AgentLockScreenController lockScreen,
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
                lockScreen.IsLocked));

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

    var tempPath = path + ".tmp";
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
static async Task HandleAgentCommandAsync(
    HubConnection connection,
    AgentCommandEnvelope command,
    AgentLockScreenController lockScreen,
    CancellationToken cancellationToken)
{
    var success = AgentCommandTypes.IsSupported(command.CommandType);
    var message = success
        ? "Agent فرمان را دریافت کرد."
        : "فرمان Agent ناشناخته است.";

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

            default:
                success = false;
                message = "فرمان Agent ناشناخته است.";
                break;
        }
    }
    catch (Exception exception) when (
        exception is HubException or HttpRequestException or InvalidOperationException or ObjectDisposedException)
    {
        success = false;
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
}


record AgentState(string DeviceId, string Name, string AgentToken, Guid? StationId);
