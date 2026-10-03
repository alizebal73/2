using Microsoft.AspNetCore.DataProtection;

namespace GameNetManager.Server.Data;

public sealed class GameCredentialProtectionService(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("GameNetManager.GameAccountCredential.v1");

    public string Protect(string secret)
        => protector.Protect(secret);

    public string Unprotect(string ciphertext)
        => protector.Unprotect(ciphertext);
}
