using JFlightShaker.Config;
using JFlightShaker.Enum;
using JFlightShaker.Helpers;
using JFlightShaker.Input;
using SharpDX.DirectInput;

namespace JFlightShaker.Core;

public sealed class BindingEvaluator
{
    private readonly ThrottleSettings _settings;

    // smoothing per (device + axis)
    private readonly Dictionary<(Guid dev, string axis), float> _smoothed = new();

    public BindingEvaluator(ThrottleSettings settings) => _settings = settings;

    public (float amp, bool enabled) Evaluate(Guid deviceGuid, JoystickState state, BindingDefinition binding)
    {
        if (binding.Kind != BindingKind.Axis || string.IsNullOrWhiteSpace(binding.AxisName))
            return (0f, true);

        float axisMin = Math.Clamp(binding.AxisMin ?? 0f, 0f, 1f);
        float axisMax = Math.Clamp(binding.AxisMax ?? 1f, 0f, 1f);
        float deadzone = Math.Clamp(_settings.Deadzone, 0f, 0.95f);
        bool invert = _settings.InvertAxis || binding.InvertAxis;
        bool isX56DualThrottle = IsX56DualThrottle(binding);
        float physicalPosition;
        float norm;

        if (isX56DualThrottle)
        {
            // The X56 throttle reports its left and right levers as X and Y,
            // although the binding editor exposes X as the throttle control.
            // Shape each lever independently before averaging so either lever
            // at 100% contributes exactly half of the final engine effect.
            (physicalPosition, norm) = EffectMath.CombineDualThrottle(
                state.X,
                state.Y,
                axisMin,
                axisMax,
                invert,
                deadzone,
                _settings.ResponseCurve);
        }
        else
        {
            int raw = BindingUiHelper.GetAxisRaw(state, binding.AxisName);
            norm = EffectMath.NormalizeAxis(raw, axisMin, axisMax, invert, deadzone);
            physicalPosition = norm;
        }

        if (binding.Effect == RumbleEffectType.PitchAndRoll)
        {
            // Bring the effect in a little earlier, while keeping hard stick
            // deflection below the throttle/button effects.
            norm = norm <= 0f
                ? 0f
                : 0.04f + (0.51f * (float)Math.Pow(norm, 1.30f));
        }
        else
        {
            if (!isX56DualThrottle)
            {
                float curve = _settings.ResponseCurve <= 0f ? 1f : _settings.ResponseCurve;
                norm = (float)Math.Pow(norm, curve);
            }

            if (binding.Effect == RumbleEffectType.ThrottleAxis)
            {
                // Afterburner detent: a pronounced continuous band from 85%.
                const float afterburnerStart = 0.85f;
                if (physicalPosition >= afterburnerStart)
                {
                    float afterburner = (physicalPosition - afterburnerStart) / (1f - afterburnerStart);
                    norm = Math.Max(norm, 0.78f + (0.22f * afterburner));
                }
            }
        }

        // Binding intensity is the maximum output. The throttle axis itself
        // must map exactly from no effect at 0% to full effect at 100%.
        float target = norm;

        float smoothing = Math.Clamp(_settings.AmpSmoothing, 0f, 0.999f);
        var key = (deviceGuid, binding.AxisName);

        _smoothed.TryGetValue(key, out var prev);
        // Do not leave a residual rumble at the idle stop.
        float smoothed = target <= 0f
            ? 0f
            : (prev * smoothing) + (target * (1f - smoothing));
        _smoothed[key] = smoothed;

        return (Math.Clamp(smoothed, 0f, 1f), true);
    }

    private static bool IsX56DualThrottle(BindingDefinition binding)
    {
        if (binding.Effect != RumbleEffectType.ThrottleAxis ||
            !string.Equals(binding.AxisName, "X", StringComparison.OrdinalIgnoreCase))
            return false;

        string name = binding.DeviceName;
        bool isThrottle = name.Contains("Throttle", StringComparison.OrdinalIgnoreCase);
        bool isX56 = name.Contains("X56", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("X-56", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Rhino", StringComparison.OrdinalIgnoreCase);
        return isThrottle && isX56;
    }

    public (float left, float right) EvaluatePitchAndRoll(
        Guid deviceGuid,
        JoystickState state,
        BindingDefinition binding)
    {
        const float center = 32767.5f;
        float roll = (Math.Clamp(state.X, 0, 65535) - center) / center;
        float pitch = (Math.Clamp(state.Y, 0, 65535) - center) / center;

        if (_settings.InvertAxis || binding.InvertAxis)
            roll = -roll;

        float pitchAmp = EffectMath.ShapePitchAndRoll(
            Math.Abs(pitch), _settings.Deadzone);
        float leftRoll = EffectMath.ShapePitchAndRoll(
            Math.Max(0f, -roll), _settings.Deadzone);
        float rightRoll = EffectMath.ShapePitchAndRoll(
            Math.Max(0f, roll), _settings.Deadzone);

        float leftTarget = Math.Max(pitchAmp, leftRoll);
        float rightTarget = Math.Max(pitchAmp, rightRoll);
        float smoothing = Math.Clamp(_settings.AmpSmoothing, 0f, 0.999f);

        float left = Smooth((deviceGuid, binding.AxisName + ":Left"), leftTarget, smoothing);
        float right = Smooth((deviceGuid, binding.AxisName + ":Right"), rightTarget, smoothing);
        return (left, right);
    }

    private float Smooth((Guid dev, string axis) key, float target, float smoothing)
    {
        _smoothed.TryGetValue(key, out var previous);
        float value = target <= 0f
            ? 0f
            : (previous * smoothing) + (target * (1f - smoothing));
        _smoothed[key] = value;
        return Math.Clamp(value, 0f, 1f);
    }
}
