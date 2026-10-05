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
    public void Production_RejectsMissingAdminPassword()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:RegistrationToken"] = "1234567890123456"
            })
            .Build();
        var environment = new TestEnvironment("Production");
        var old = Environment.GetEnvironmentVariable("GAMENET_ADMIN_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", null);
            var exception = Assert.Throws<InvalidOperationException>(() =>
                GameNetManager.Server.Data.ProductionSecretValidator.Validate(configuration, environment));
            Assert.Contains("GAMENET_ADMIN_PASSWORD", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", old);
        }
    }

    [Fact]
    public void Production_RejectsShortAgentToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:RegistrationToken"] = "short"
            })
            .Build();
        var environment = new TestEnvironment("Production");
        var old = Environment.GetEnvironmentVariable("GAMENET_ADMIN_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", "strong-test-password");
            var exception = Assert.Throws<InvalidOperationException>(() =>
                GameNetManager.Server.Data.ProductionSecretValidator.Validate(configuration, environment));
            Assert.Contains("Agent:RegistrationToken", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", old);
        }
    }

    [Fact]
    public void Production_AcceptsConfiguredSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:RegistrationToken"] = "1234567890123456"
            })
            .Build();
        var environment = new TestEnvironment("Production");
        var old = Environment.GetEnvironmentVariable("GAMENET_ADMIN_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", "strong-test-password");
            var exception = Record.Exception(() =>
                GameNetManager.Server.Data.ProductionSecretValidator.Validate(configuration, environment));
            Assert.Null(exception);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", old);
        }
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GameNetManager.Server.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
