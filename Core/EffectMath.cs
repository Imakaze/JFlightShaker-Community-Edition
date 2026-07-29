namespace JFlightShaker.Core;

public static class EffectMath
{
    public static float NormalizeAxis(
        int raw,
        float axisMin,
        float axisMax,
        bool invert,
        float deadzone)
    {
        float value = Math.Clamp(raw / 65535f, 0f, 1f);
        value = axisMax > axisMin
            ? Math.Clamp((value - axisMin) / (axisMax - axisMin), 0f, 1f)
            : 0f;

        if (invert)
            value = 1f - value;

        return value <= deadzone
            ? 0f
            : (value - deadzone) / (1f - deadzone);
    }

    public static (float physical, float shaped) CombineDualThrottle(
        int leftRaw,
        int rightRaw,
        float axisMin,
        float axisMax,
        bool invert,
        float deadzone,
        float responseCurve)
    {
        float left = NormalizeAxis(leftRaw, axisMin, axisMax, invert, deadzone);
        float right = NormalizeAxis(rightRaw, axisMin, axisMax, invert, deadzone);
        float curve = responseCurve <= 0f ? 1f : responseCurve;
        return (
            (left + right) * 0.5f,
            ((float)Math.Pow(left, curve) + (float)Math.Pow(right, curve)) * 0.5f);
    }

    public static float CenteredAxisMagnitude(int raw)
    {
        const float center = 32767.5f;
        return Math.Abs((Math.Clamp(raw, 0, 65535) - center) / center);
    }

    public static bool UpdateThresholdState(
        float value,
        float threshold,
        float hysteresis,
        bool wasActive)
    {
        threshold = Math.Clamp(threshold, 0f, 0.95f);
        hysteresis = Math.Clamp(hysteresis, 0f, 0.2f);
        return wasActive
            ? value > Math.Max(0f, threshold - hysteresis)
            : value >= threshold;
    }

    public static float ScaleAboveThreshold(
        float value,
        float threshold,
        float intensity)
    {
        threshold = Math.Clamp(threshold, 0f, 0.95f);
        float scaled = Math.Clamp(
            (value - threshold) / Math.Max(0.001f, 1f - threshold),
            0f,
            1f);
        float smooth = scaled * scaled * (3f - (2f * scaled));
        return Math.Clamp(smooth * intensity, 0f, 1f);
    }

    public static float PitchRollSpeedFactor(float throttle, bool hasThrottleInput)
        => hasThrottleInput
            ? 0.65f + (0.50f * Math.Clamp(throttle, 0f, 1f))
            : 1f;

    public static float ShapePitchAndRoll(float value, float deadzone)
    {
        deadzone = Math.Clamp(deadzone, 0f, 0.95f);
        value = Math.Abs(value);
        if (value <= deadzone)
            return 0f;

        value = (value - deadzone) / (1f - deadzone);
        return 0.04f + (0.51f * (float)Math.Pow(value, 1.30f));
    }
}
