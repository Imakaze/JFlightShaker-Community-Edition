namespace JFlightShaker.Config;

public sealed class HighGSettings
{
    public float DefaultActivationThreshold { get; set; } = 0.65f;
    public float Hysteresis { get; set; } = 0.035f;
}
