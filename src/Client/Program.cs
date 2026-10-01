using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

var serverUrl = Environment.GetEnvironmentVariable("GAMENET_SERVER_URL") ?? "http://localhost:5080";
var hubUrl = $"{serverUrl.TrimEnd('/')}/hubs/dashboard";

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
	eventArgs.Cancel = true;
	shutdown.Cancel();
};

await using var connection = new HubConnectionBuilder()
	.WithUrl(hubUrl)
	.WithAutomaticReconnect()
	.Build();

connection.On<ServerInfoDto>("ServerReady", info =>
	Console.WriteLine($"Connected to {info.Name} ({info.Environment}) at {info.UtcNow:O}"));
connection.Reconnecting += error =>
{
	Console.WriteLine($"Connection lost; retrying: {error?.Message ?? "network interruption"}");
	return Task.CompletedTask;
};
connection.Reconnected += connectionId =>
{
	Console.WriteLine($"Reconnected ({connectionId})");
	return Task.CompletedTask;
};

try
{
	await connection.StartAsync(shutdown.Token);
	Console.WriteLine($"GameNet client connected to {hubUrl}. Press Ctrl+C to stop.");
	await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
	Console.WriteLine("Client stopped.");
}
catch (Exception exception)
{
	Console.Error.WriteLine($"Could not connect to the GameNet server: {exception.Message}");
	Environment.ExitCode = 1;
}
Console.WriteLine("Hello, World!");
