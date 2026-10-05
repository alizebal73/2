namespace GameNetManager.Server.Data;

public static class ProductionSecretValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        var registrationToken = configuration["Agent:RegistrationToken"];
        if (string.IsNullOrWhiteSpace(registrationToken) || registrationToken.Length < 16)
            throw new InvalidOperationException(
                "Production startup requires Agent:RegistrationToken with at least 16 characters; default Agent registration is disabled.");
    }
}
