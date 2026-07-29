using JFlightShaker.Config;

public sealed class ActiveProfile
{
    public AppConfig AppConfig { get; init; } = new();
    public ThrottleSettings ThrottleSettings { get; init; } = new();
    public GunHoldSettings GunSettings { get; init; } = new();
    public MissileSettings MissileSettings { get; init; } = new();
    public HighGSettings HighGSettings { get; init; } = new();
    public List<BindingConfig> Bindings { get; set; } = new();
}
