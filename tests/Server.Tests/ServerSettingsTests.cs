using System.Text.Json;
using GameNetManager.Server.Data;

namespace GameNetManager.Server.Tests;

public sealed class ServerSettingsTests
{
    [Fact]
    public void CatalogContainsOperationalDefaultsAndRejectsInvalidValues()
    {
        var values = ServerSettingsCatalog.BuildValues(Array.Empty<AppSetting>());

        Assert.Equal("settle", values["sessionMode"].GetString());
        Assert.Equal(10, values["operatorDiscount"].GetInt32());
        Assert.True(values["backupAuto"].GetBoolean());

        using var invalidDiscount = JsonDocument.Parse("101");
        Assert.False(ServerSettingsCatalog.TryValidate(
            "operatorDiscount",
            invalidDiscount.RootElement,
            out var discountError));
        Assert.Contains("۰ تا ۱۰۰", discountError);

        using var invalidHour = JsonDocument.Parse("\"25:61\"");
        Assert.False(ServerSettingsCatalog.TryValidate(
            "backupHour",
            invalidHour.RootElement,
            out var hourError));
        Assert.Contains("HH:mm", hourError);
    }

    [Fact]
    public void StoredValueOverridesDefaultAndUnknownKeyDoesNotLeakIntoResponse()
    {
        using var custom = JsonDocument.Parse("\"192.168.0.9:5080\"");
        var stored = new[]
        {
            new AppSetting
            {
                Key = "serverAddress",
                ScopeKey = ServerSettingsCatalog.GlobalScope,
                ValueJson = custom.RootElement.GetRawText()
            },
            new AppSetting
            {
                Key = "unexpected",
                ScopeKey = ServerSettingsCatalog.GlobalScope,
                ValueJson = "true"
            }
        };

        var values = ServerSettingsCatalog.BuildValues(stored);

        Assert.Equal("192.168.0.9:5080", values["serverAddress"].GetString());
        Assert.False(values.ContainsKey("unexpected"));
    }
}
