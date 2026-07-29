using JFlightShaker.Audio;
using JFlightShaker.Config;
using JFlightShaker.Core;

namespace JFlightShaker.Tests;

public sealed class EffectLifecycleTests
{
    [Fact]
    public void Missile_ProducesOutputThenFinishesWithoutResidualAmplitude()
    {
        var missile = new MissileEffect(
            1f,
            new MissileSettings { DurationSeconds = 0.25f });

        Assert.True(missile.UpdateAndGetAmp(0.02f) > 0f);
        for (int i = 0; i < 10; i++)
            missile.UpdateAndGetAmp(0.05f);

        Assert.True(missile.Finished);
        Assert.Equal(0f, missile.UpdateAndGetAmp(0.05f));
    }

    [Fact]
    public void Mixer_ClampsOverlappingEffectsAndClearsStoppedEffects()
    {
        var mixer = new EffectMixer();
        mixer.Add(new ConstantEffect(0.7f));
        mixer.Add(new ConstantEffect(0.7f));

        Assert.Equal(1f, mixer.Update(0.01f));
        mixer.StopAll();
        Assert.Equal(0f, mixer.Update(0.01f));
    }

    private sealed class ConstantEffect(float amplitude) : RumbleEffect
    {
        public bool Finished { get; private set; }
        public float UpdateAndGetAmp(float dtSeconds) => Finished ? 0f : amplitude;
        public void Stop() => Finished = true;
    }
}
