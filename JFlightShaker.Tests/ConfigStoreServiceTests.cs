using JFlightShaker.Config;
using JFlightShaker.Service;

namespace JFlightShaker.Tests;

public sealed class ConfigStoreServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "JFlightShaker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void MalformedJson_IsBackedUpAndReplacedWithDefaults()
    {
        var store = new ConfigStoreService(_root);
        File.WriteAllText(store.AppConfigPath, "{ definitely not json");

        var config = store.LoadAppConfig();

        Assert.Equal(BuildInfo.DisplayVersion, config.Version);
        Assert.True(File.Exists(store.AppConfigPath));
        Assert.Single(Directory.GetFiles(_root, "appsettings.broken-*.json"));
    }

    [Fact]
    public void OutOfRangeProfileValues_AreNormalized()
    {
        var store = new ConfigStoreService(_root);
        const string path = @"profiles\high_g_effect.json";
        store.SaveProfile(
            path,
            new HighGSettings
            {
                DefaultActivationThreshold = 4f,
                Hysteresis = -1f
            });

        var settings = store.LoadProfile<HighGSettings>(path);

        Assert.Equal(0.95f, settings.DefaultActivationThreshold);
        Assert.Equal(0f, settings.Hysteresis);
    }

    [Fact]
    public void RootedConfigurationPaths_AreRejected()
    {
        var store = new ConfigStoreService(_root);
        store.SaveAppConfig(new AppConfig
        {
            BindingsPath = @"C:\private\bindings.json"
        });

        var config = store.LoadAppConfig();

        Assert.Equal("bindings.json", config.BindingsPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
