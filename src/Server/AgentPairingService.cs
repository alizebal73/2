using System.Globalization;
using System.Security.Cryptography;

namespace GameNetManager.Server;

public sealed class AgentPairingService
{
    private readonly object _gate = new();
    private string? _code;
    private DateTimeOffset _expiresAt;
    private int _uses;
    private int _maxUses;

    public AgentPairingCode Create(TimeSpan lifetime, int maxUses = 100)
    {
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        if (maxUses <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxUses));

        lock (_gate)
        {
            _code = RandomNumberGenerator.GetInt32(100000, 1_000_000)
                .ToString(CultureInfo.InvariantCulture);
            _expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
            _uses = 0;
            _maxUses = maxUses;
            return new AgentPairingCode(_code, _expiresAt, maxUses);
        }
    }

    public bool TryUse(string? code)
    {
        var normalized = code?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        lock (_gate)
        {
            if (_code is null || !string.Equals(_code, normalized, StringComparison.Ordinal))
                return false;

            if (_expiresAt <= DateTimeOffset.UtcNow || _uses >= _maxUses)
                return false;

            _uses++;
            return true;
        }
    }
}

public sealed record AgentPairingCode(
    string Code,
    DateTimeOffset ExpiresAt,
    int MaxUses);
