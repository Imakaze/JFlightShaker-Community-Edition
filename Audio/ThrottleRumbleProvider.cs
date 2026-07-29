using JFlightShaker.Config;
using NAudio.Wave;

namespace JFlightShaker.Audio;

public sealed class ThrottleRumbleProvider : WaveProvider32
{
    private readonly ThrottleSettings _s;

    private float _leftAmp;
    private float _rightAmp;
    private volatile float _leftTarget;
    private volatile float _rightTarget;

    private double _phase;
    private readonly Random _rng = new(1);

    // simple 1st-order high-pass state
    private float _leftHpXPrev;
    private float _leftHpYPrev;
    private float _rightHpXPrev;
    private float _rightHpYPrev;

    public bool Enabled { get; set; } = true;

    public ThrottleRumbleProvider(int sampleRate, int channels, ThrottleSettings settings)
        : base(sampleRate, channels)
    {
        _s = settings;
    }

    public void SetTargetAmplitude(float a) => SetTargetAmplitudes(a, a);

    public void SetTargetAmplitudes(float left, float right)
    {
        _leftTarget = left;
        _rightTarget = right;
    }

    public override int Read(float[] buffer, int offset, int sampleCount)
    {
        float leftTarget = Enabled ? _leftTarget : 0f;
        float rightTarget = Enabled ? _rightTarget : 0f;
        int ch = WaveFormat.Channels;
        int sr = WaveFormat.SampleRate;

        // Base rumble freq
        const float baseHz = 45f;

        // High-pass around ~10 Hz to strip DC (RC filter)
        const float hpCut = 10f;
        float rc = 1f / (2f * (float)Math.PI * hpCut);
        float dt = 1f / sr;
        float alpha = rc / (rc + dt);

        for (int i = 0; i < sampleCount; i += ch)
        {
            _leftAmp += (leftTarget - _leftAmp) * _s.AmpSmoothing;
            _rightAmp += (rightTarget - _rightAmp) * _s.AmpSmoothing;

            // sine
            _phase += (2.0 * Math.PI * baseHz) / sr;
            if (_phase > Math.PI * 2.0) _phase -= Math.PI * 2.0;
            float s = (float)Math.Sin(_phase);

            // noise in [-1, 1]
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0f);

            // mix
            float raw = (0.8f * s) + (0.2f * noise);

            // amplitude envelope
            float leftSignal = raw * _leftAmp;
            float left = alpha * (_leftHpYPrev + leftSignal - _leftHpXPrev);
            _leftHpXPrev = leftSignal;
            _leftHpYPrev = left;
            buffer[offset + i] = left;

            if (ch > 1)
            {
                float rightSignal = raw * _rightAmp;
                float right = alpha * (_rightHpYPrev + rightSignal - _rightHpXPrev);
                _rightHpXPrev = rightSignal;
                _rightHpYPrev = right;
                buffer[offset + i + 1] = right;
            }
        }

        return sampleCount;
    }
}
