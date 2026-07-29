using JFlightShaker.Config;
using JFlightShaker.Core;
using JFlightShaker.Enum;

namespace JFlightShaker.Tests;

public sealed class EffectMathTests
{
    [Theory]
    [InlineData(65535, 65535, 0.0)]
    [InlineData(0, 65535, 0.5)]
    [InlineData(65535, 0, 0.5)]
    [InlineData(0, 0, 1.0)]
    public void X56DualThrottle_EachLeverContributesHalf(
        int leftRaw,
        int rightRaw,
        double expected)
    {
        var result = EffectMath.CombineDualThrottle(
            leftRaw,
            rightRaw,
            axisMin: 0f,
            axisMax: 1f,
            invert: true,
            deadzone: 0f,
            responseCurve: 2.2f);

        Assert.Equal(expected, result.shaped, precision: 3);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(32767, 0.0)]
    [InlineData(32768, 0.0)]
    [InlineData(65535, 1.0)]
    public void CenteredAxisMagnitude_HandlesBothPitchDirections(
        int raw,
        double expected)
    {
        Assert.Equal(
            expected,
            EffectMath.CenteredAxisMagnitude(raw),
            precision: 3);
    }

    [Fact]
    public void HighG_HysteresisPreventsThresholdFlicker()
    {
        Assert.True(EffectMath.UpdateThresholdState(
            value: 0.76f, threshold: 0.75f, hysteresis: 0.035f, wasActive: false));
        Assert.True(EffectMath.UpdateThresholdState(
            value: 0.73f, threshold: 0.75f, hysteresis: 0.035f, wasActive: true));
        Assert.False(EffectMath.UpdateThresholdState(
            value: 0.70f, threshold: 0.75f, hysteresis: 0.035f, wasActive: true));
    }

    [Fact]
    public void HighG_ScalesFromThresholdToFullPitch()
    {
        Assert.Equal(0f, EffectMath.ScaleAboveThreshold(0.65f, 0.65f, 1f));
        Assert.InRange(
            EffectMath.ScaleAboveThreshold(0.825f, 0.65f, 1f),
            0.49f,
            0.51f);
        Assert.Equal(1f, EffectMath.ScaleAboveThreshold(1f, 0.65f, 1f));
    }

    [Theory]
    [InlineData(0.0, 0.65)]
    [InlineData(0.7, 1.0)]
    [InlineData(1.0, 1.15)]
    public void PitchRollSpeedFactor_IsModerate(float throttle, double expected)
    {
        Assert.Equal(
            expected,
            EffectMath.PitchRollSpeedFactor(throttle, hasThrottleInput: true),
            precision: 3);
    }

    [Fact]
    public void PitchRollSpeedFactor_PreservesOldStrengthWithoutThrottle()
    {
        Assert.Equal(
            1f,
            EffectMath.PitchRollSpeedFactor(0f, hasThrottleInput: false));
    }

    [Fact]
    public void PitchRoll_DeadzoneAndMaximumRemainBounded()
    {
        Assert.Equal(0f, EffectMath.ShapePitchAndRoll(0.04f, 0.05f));
        Assert.InRange(EffectMath.ShapePitchAndRoll(1f, 0.05f), 0.549f, 0.551f);
    }

    [Fact]
    public void MuteEffects_DefaultsToTriggerButStillAllowsHold()
    {
        var triggers = EffectBindingRules.GetAllowedTriggers(
            RumbleEffectType.MuteEffects);
        Assert.Equal(TriggerType.Press, triggers[0]);
        Assert.Contains(TriggerType.Hold, triggers);
    }
}
