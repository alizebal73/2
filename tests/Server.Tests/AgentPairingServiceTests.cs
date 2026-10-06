namespace GameNetManager.Server.Tests;

public sealed class AgentPairingServiceTests
{
    [Fact]
    public void Create_ReturnsSixDigitCodeAndExpiry()
    {
        var service = new GameNetManager.Server.AgentPairingService();

        var pairing = service.Create(TimeSpan.FromMinutes(5), maxUses: 2);

        Assert.Matches("^\\d{6}$", pairing.Code);
        Assert.True(pairing.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(2, pairing.MaxUses);
    }

    [Fact]
    public void TryUse_AllowsOnlyConfiguredCodeAndHonorsUseLimit()
    {
        var service = new GameNetManager.Server.AgentPairingService();
        var pairing = service.Create(TimeSpan.FromMinutes(5), maxUses: 2);

        Assert.False(service.TryUse("000000"));
        Assert.True(service.TryUse(pairing.Code));
        Assert.True(service.TryUse(pairing.Code));
        Assert.False(service.TryUse(pairing.Code));
    }

    [Fact]
    public void TryUse_RejectsExpiredCode()
    {
        var service = new GameNetManager.Server.AgentPairingService();
        var pairing = service.Create(TimeSpan.FromMilliseconds(1), maxUses: 10);

        Thread.Sleep(25);

        Assert.False(service.TryUse(pairing.Code));
    }
}
