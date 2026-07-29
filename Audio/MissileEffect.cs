using JFlightShaker.Config;

namespace JFlightShaker.Audio;

public sealed class MissileEffect : RumbleEffect
{
    private float _elapsed;
    private bool _stopped;

    public bool Finished { get; private set; }
    public float Intensity { get; }
    public MissileSettings Settings { get; }

    public MissileEffect(float intensity, MissileSettings? settings = null)
    {
        Intensity = Math.Clamp(intensity, 0f, 1f);
        Settings = settings ?? new MissileSettings();
    }

    public float UpdateAndGetAmp(float dtSeconds)
    {
        if (_stopped || Finished)
            return 0f;

        dtSeconds = Math.Clamp(dtSeconds, 0f, 0.1f);
        _elapsed += dtSeconds;

        float duration = Math.Max(0.05f, Settings.DurationSeconds);
        if (_elapsed >= duration)
        {
            Finished = true;
            return 0f;
        }

        float attack = Math.Clamp(Settings.AttackSeconds, 0.001f, duration);
        float envelope;

        if (_elapsed < attack)
        {
            envelope = _elapsed / attack;
        }
        else
        {
            float decayProgress = (_elapsed - attack) / Math.Max(0.001f, duration - attack);
            float decayPower = Math.Max(0.1f, Settings.DecayPower);
            envelope = (float)Math.Pow(1f - decayProgress, decayPower);
        }

        // A short initial launch kick layered over the decaying rumble tail.
        float punchLength = Math.Min(0.12f, duration);
        float punch = _elapsed < punchLength
            ? (1f - (_elapsed / punchLength)) * Math.Clamp(Settings.Punch, 0f, 1f)
            : 0f;

        return Math.Clamp(Intensity * (envelope + punch), 0f, 1f);
    }

    public void Stop()
    {
        _stopped = true;
        Finished = true;
    }
}
