namespace JFlightShaker.Config;

public sealed class MissileSettings
{
    // Duration of the complete launch kick and rumble tail.
    public float DurationSeconds { get; set; } = 0.65f;
    public float AttackSeconds { get; set; } = 0.025f;
    public float DecayPower { get; set; } = 2.2f;
    public float Punch { get; set; } = 0.35f;
}
