namespace GameNetManager.Server.Data;

public static class ProductionSecretValidator
{
    public const string AdminPasswordEnvironmentVariable = "GAMENET_ADMIN_PASSWORD";

    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        var adminPassword = Environment.GetEnvironmentVariable(AdminPasswordEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 8)
            throw new InvalidOperationException(
                "Production startup requires GAMENET_ADMIN_PASSWORD with at least 8 characters; default credentials are disabled.");

        var registrationToken = configuration["Agent:RegistrationToken"];
        if (string.IsNullOrWhiteSpace(registrationToken) || registrationToken.Length < 16)
            throw new InvalidOperationException(
                "Production startup requires Agent:RegistrationToken with at least 16 characters; default Agent registration is disabled.");
    }
}
