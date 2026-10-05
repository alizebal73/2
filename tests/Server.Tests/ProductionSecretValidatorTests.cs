using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GameNetManager.Server.Tests;

public sealed class ProductionSecretValidatorTests
{
    [Fact]
    public void Development_DoesNotRequireSecrets()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestEnvironment("Development");

        var exception = Record.Exception(() =>
            GameNetManager.Server.Data.ProductionSecretValidator.Validate(configuration, environment));

        Assert.Null(exception);
    }

    [Fact]
    public void Production_RejectsMissingRegistrationToken()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestEnvironment("Production");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GameNetManager.Server.Data.ProductionSecretValidator.Validate(configuration, environment));

        Assert.Contains("Agent:RegistrationToken", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_RejectsShortRegistrationToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:RegistrationToken"] = "short"
            })
            .Build();
        var environment = new TestEnvironment("Production");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GameNetManager.Server.Data.ProductionSecretValidator.Validate(configuration, environment));

        Assert.Contains("Agent:RegistrationToken", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_AllowsStartupWithRegistrationTokenAfterBootstrapPasswordWasConsumed()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:RegistrationToken"] = "1234567890123456"
            })
            .Build();
        var environment = new TestEnvironment("Production");

        var exception = Record.Exception(() =>
            GameNetManager.Server.Data.ProductionSecretValidator.Validate(configuration, environment));

        Assert.Null(exception);
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GameNetManager.Server.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
