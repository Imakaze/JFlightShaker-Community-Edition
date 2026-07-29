using JFlightShaker.Config;
using System.IO;
using System.Text.Json;

namespace JFlightShaker.Service;

public sealed class ConfigStoreService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string RootDir { get; }
    public string AppConfigPath => Path.Combine(RootDir, "appsettings.json");

    public ConfigStoreService(string? rootDirectory = null)
    {
        RootDir = rootDirectory ??
            Path.Combine(AppContext.BaseDirectory, "Config");
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(Path.Combine(RootDir, "profiles"));
    }

    public AppConfig LoadAppConfig()
        => LoadOrRecover<AppConfig>(AppConfigPath, SaveAppConfig);

    public void SaveAppConfig(AppConfig cfg)
        => SaveJson(AppConfigPath, cfg);

    public T LoadProfile<T>(string relativePath) where T : new()
    {
        string full = Path.Combine(RootDir, relativePath);
        return LoadOrRecover<T>(full, value => SaveProfile(relativePath, value));
    }

    public void SaveProfile<T>(string relativePath, T profile)
        => SaveJson(Path.Combine(RootDir, relativePath), profile);

    public List<BindingConfig> LoadBindings(string relativePath)
        => LoadProfile<List<BindingConfig>>(relativePath);

    public void SaveBindings(string relativePath, List<BindingConfig> bindings)
        => SaveProfile(relativePath, bindings);

    private T LoadOrRecover<T>(string fullPath, Action<T> saveDefault) where T : new()
    {
        if (!File.Exists(fullPath))
        {
            var created = Normalize(new T());
            saveDefault(created);
            return created;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var value = JsonSerializer.Deserialize<T>(json, JsonOpts);
            if (value == null)
                throw new JsonException("The configuration file contained no usable data.");
            return Normalize(value);
        }
        catch (Exception ex) when (
            ex is JsonException or IOException or UnauthorizedAccessException)
        {
            string? backup = BackupBrokenFile(fullPath);
            AppLog.Error(
                $"Invalid configuration recovered: {fullPath}" +
                (backup == null ? "" : $" (backup: {backup})"),
                ex);

            var defaults = Normalize(new T());
            saveDefault(defaults);
            return defaults;
        }
    }

    private static string? BackupBrokenFile(string fullPath)
    {
        try
        {
            string directory = Path.GetDirectoryName(fullPath)!;
            string name = Path.GetFileNameWithoutExtension(fullPath);
            string extension = Path.GetExtension(fullPath);
            string backup = Path.Combine(
                directory,
                $"{name}.broken-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
            File.Move(fullPath, backup);
            return backup;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Unable to back up invalid configuration: {fullPath}", ex);
            return null;
        }
    }

    private static void SaveJson<T>(string fullPath, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        string json = JsonSerializer.Serialize(value, JsonOpts);
        File.WriteAllText(temporary, json);
        File.Move(temporary, fullPath, true);
    }

    private static T Normalize<T>(T value)
    {
        switch (value)
        {
            case AppConfig app:
                app.Version = BuildInfo.DisplayVersion;
                app.ThrottleProfilePath = SafeRelativePath(
                    app.ThrottleProfilePath, @"profiles\throttle_effect.json");
                app.GunProfilePath = SafeRelativePath(
                    app.GunProfilePath, @"profiles\gun_effect.json");
                app.MissileProfilePath = SafeRelativePath(
                    app.MissileProfilePath, @"profiles\missile_effect.json");
                app.HighGProfilePath = SafeRelativePath(
                    app.HighGProfilePath, @"profiles\high_g_effect.json");
                app.BindingsPath = SafeRelativePath(app.BindingsPath, "bindings.json");
                break;

            case ThrottleSettings throttle:
                throttle.SampleRate = Math.Clamp(throttle.SampleRate, 8000, 192000);
                throttle.Channels = 2;
                throttle.Deadzone = Math.Clamp(throttle.Deadzone, 0f, 0.95f);
                throttle.BaselineAmp = Math.Clamp(throttle.BaselineAmp, 0f, 1f);
                throttle.TopAmp = Math.Clamp(throttle.TopAmp, 0f, 1f);
                throttle.AmpSmoothing = Math.Clamp(throttle.AmpSmoothing, 0f, 0.999f);
                throttle.ResponseCurve = Math.Clamp(throttle.ResponseCurve, 0.1f, 5f);
                throttle.DefaultAxisName = string.IsNullOrWhiteSpace(throttle.DefaultAxisName)
                    ? "RotationX"
                    : throttle.DefaultAxisName.Trim();
                break;

            case GunHoldSettings gun:
                gun.PulseHz = Math.Clamp(gun.PulseHz, 1f, 100f);
                gun.Punch = Math.Clamp(gun.Punch, 0f, 1f);
                gun.Jitter = Math.Clamp(gun.Jitter, 0f, 1f);
                gun.Floor = Math.Clamp(gun.Floor, 0f, 1f);
                gun.DefaultButtonIndex = Math.Clamp(gun.DefaultButtonIndex, 0, 127);
                break;

            case MissileSettings missile:
                missile.DurationSeconds = Math.Clamp(missile.DurationSeconds, 0.05f, 10f);
                missile.AttackSeconds = Math.Clamp(
                    missile.AttackSeconds, 0.001f, missile.DurationSeconds);
                missile.DecayPower = Math.Clamp(missile.DecayPower, 0.1f, 10f);
                missile.Punch = Math.Clamp(missile.Punch, 0f, 1f);
                break;

            case HighGSettings highG:
                highG.DefaultActivationThreshold = Math.Clamp(
                    highG.DefaultActivationThreshold, 0f, 0.95f);
                highG.Hysteresis = Math.Clamp(highG.Hysteresis, 0f, 0.2f);
                break;

            case List<BindingConfig> bindings:
                foreach (var binding in bindings)
                {
                    binding.Intensity = Math.Clamp(binding.Intensity, 0f, 1f);
                    binding.AxisMin = binding.AxisMin is float min
                        ? Math.Clamp(min, 0f, 1f)
                        : null;
                    binding.AxisMax = binding.AxisMax is float max
                        ? Math.Clamp(max, 0f, 1f)
                        : null;
                    if (binding.AxisMin > binding.AxisMax)
                        (binding.AxisMin, binding.AxisMax) =
                            (binding.AxisMax, binding.AxisMin);
                    binding.ActivationThreshold = binding.ActivationThreshold is float threshold
                        ? Math.Clamp(threshold, 0f, 0.95f)
                        : null;
                    binding.ButtonIndex = binding.ButtonIndex is int button
                        ? Math.Clamp(button, 0, 127)
                        : null;
                }
                break;
        }

        return value;
    }

    private static string SafeRelativePath(string? candidate, string fallback)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
            return fallback;

        string normalized = candidate.Replace('/', Path.DirectorySeparatorChar);
        return normalized.Split(Path.DirectorySeparatorChar)
            .Any(part => part == "..")
            ? fallback
            : normalized;
    }
}
