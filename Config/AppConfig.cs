namespace JFlightShaker.Config;

public sealed class AppConfig
{
    public string Version { get; set; } = JFlightShaker.BuildInfo.DisplayVersion;

    // Devices
    public string? SelectedAudioDeviceId { get; set; }

    // Configs
    public string ThrottleProfilePath { get; set; } = @"profiles\throttle_effect.json";
    public string GunProfilePath { get; set; } = @"profiles\gun_effect.json";
    public string MissileProfilePath { get; set; } = @"profiles\missile_effect.json";
    public string HighGProfilePath { get; set; } = @"profiles\high_g_effect.json";

    public string BindingsPath { get; set; } = "bindings.json";
}
