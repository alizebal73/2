using System.Text.Json;
using GameNetManager.Shared.Contracts;

namespace GameNetManager.Client;

public static class ClientUpdateCommandParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ClientUpdateCommandPayload Parse(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new InvalidOperationException("دادهٔ Update ناقص یا نامعتبر است.");

        ClientUpdateCommandPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ClientUpdateCommandPayload>(payloadJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("دادهٔ Update ناقص یا نامعتبر است.", exception);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.Version)
            || string.IsNullOrWhiteSpace(payload.PackageUrl)
            || string.IsNullOrWhiteSpace(payload.Sha256)
            || payload.SizeBytes <= 0)
        {
            throw new InvalidOperationException("دادهٔ Update ناقص یا نامعتبر است.");
        }

        return payload;
    }
}
