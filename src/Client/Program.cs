using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using GameNetManager.Shared.Contracts;
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

using var shutdown = new CancellationTokenSource();
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

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/agent/register");
        request.Headers.Add("X-GameNet-Registration-Token", registrationToken);
        request.Content = JsonContent.Create(new AgentRegistrationRequest(
            state.DeviceId,
            state.Name,
            stationId,
            agentVersion,
            osVersion));

        using var response = await httpClient.SendAsync(request, shutdown.Token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ثبت Agent در سرور با کد {(int)response.StatusCode} رد شد.");

        var registration = await response.Content.ReadFromJsonAsync<AgentRegistrationResponse>(
            cancellationToken: shutdown.Token)
            ?? throw new InvalidOperationException("پاسخ ثبت Agent از سرور نامعتبر بود.");

        state = state with { AgentToken = registration.AgentToken };
        await SaveStateAsync(statePath, state);
        Console.WriteLine($"Agent با شناسه {state.DeviceId} در سرور ثبت شد.");
    }
    else
    {
        await SaveStateAsync(statePath, state);
    }

    var hubUrl = $"{serverUrl.TrimEnd('/')}/hubs/agent";
    await using var connection = new HubConnectionBuilder()
        .WithUrl(hubUrl, options =>
        {
            options.Headers["X-GameNet-Device-Id"] = state.DeviceId;
            options.Headers["Authorization"] = $"Bearer {state.AgentToken}";
        })
        .WithAutomaticReconnect()
        .Build();

    connection.On<AgentReadyDto>("AgentReady", ready =>
        Console.WriteLine($"Agent متصل شد؛ شناسه سرور: {ready.AgentId}; زمان سرور: {ready.ServerUtcNow:O}"));

    connection.Reconnecting += error =>
    {
        Console.WriteLine($"ارتباط Agent با سرور قطع شد؛ تلاش برای اتصال مجدد. {error?.Message ?? string.Empty}".Trim());
        return Task.CompletedTask;
    };

    connection.Reconnected += async connectionId =>
    {
        Console.WriteLine($"Agent دوباره متصل شد ({connectionId}).");
        await SendHeartbeatAsync(connection, agentVersion, osVersion, shutdown.Token);
    };

    connection.Closed += error =>
    {
        if (!shutdown.IsCancellationRequested)
            Console.WriteLine($"اتصال Agent بسته شد: {error?.Message ?? "علت نامشخص"}");
        return Task.CompletedTask;
    };

    await connection.StartAsync(shutdown.Token);
    Console.WriteLine($"Agent GameNet روی {hubUrl} فعال شد.");

    while (!shutdown.IsCancellationRequested)
    {
        var heartbeatSeconds = await SendHeartbeatAsync(
            connection,
            agentVersion,
            osVersion,
            shutdown.Token);

        await Task.Delay(TimeSpan.FromSeconds(
            Math.Clamp(heartbeatSeconds ?? 10, 3, 60)), shutdown.Token);
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

static async Task<int?> SendHeartbeatAsync(
    HubConnection connection,
    string agentVersion,
    string osVersion,
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
                Environment.TickCount64 / 1000),
            cancellationToken);

        Console.WriteLine($"Heartbeat موفق؛ زمان سرور: {response.ServerUtcNow:HH:mm:ss}.");
        return response.HeartbeatIntervalSeconds;
    }
    catch (Exception exception) when (
        exception is HubException or HttpRequestException or InvalidOperationException)
    {
        Console.WriteLine($"Heartbeat ارسال نشد؛ اتصال دوباره بررسی می‌شود. {exception.Message}");
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
    catch
    {
        return new AgentState(string.Empty, string.Empty, string.Empty, null);
    }
}

static async Task SaveStateAsync(string path, AgentState state)
{
    var json = JsonSerializer.Serialize(
        state,
        new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(path, json);
}

record AgentState(string DeviceId, string Name, string AgentToken, Guid? StationId);
