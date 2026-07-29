using SharpDX.DirectInput;

namespace JFlightShaker.Helpers;

public static class BindingUiHelper
{
    public const string CombinedSlidersAxis = "Slider0 + Slider1";
    public const string CombinedThrottleAxis = "Throttle (Z + Rz)";
    public const string PitchAndRollAxis = "Pitch + Roll (X + Y)";

    public static List<string> GetDeviceAxes(Func<Guid, Joystick?> openJoystick, Guid deviceGuid)
    {
        var js = openJoystick(deviceGuid);
        if (js == null) return new List<string>();

        try
        {
            return GetDeviceAxes(js);
        }
        catch
        {
            return new List<string>();
        }
        finally
        {
            js.Dispose();
        }
    }

    public static List<string> GetDeviceAxes(Joystick js)
    {
        try
        {
            var axisObjects = js.GetObjects()
                .Where(IsAxisObject)
                .ToList();

            var axes = new List<string>();
            int sliderOrdinal = 0;

            foreach (var obj in axisObjects)
            {
                // Names are driver-defined, localized, and the two slider names
                // are often identical. Offset is the authoritative location in
                // the JoystickState packet.
                string? axisName;
                if (obj.ObjectType == ObjectGuid.Slider)
                {
                    axisName = MapAxisObject(obj)
                        ?? $"Slider{Math.Min(sliderOrdinal, 1)}";
                    sliderOrdinal++;
                }
                else
                {
                    axisName = MapAxisObject(obj)
                        ?? MapAxisOffset(obj.Offset)
                        ?? (string.IsNullOrWhiteSpace(obj.Name) ? null : MapAxisName(obj.Name));
                }

                if (axisName != null &&
                    !axes.Contains(axisName, StringComparer.OrdinalIgnoreCase))
                    axes.Add(axisName);
            }

            if (axes.Contains("Slider0") && axes.Contains("Slider1"))
                axes.Insert(0, CombinedSlidersAxis);

            if (axes.Contains("Z") && axes.Contains("RotationZ"))
                axes.Insert(0, CombinedThrottleAxis);

            if (axes.Contains("X") && axes.Contains("Y"))
                axes.Insert(0, PitchAndRollAxis);

            return axes;
        }
        catch
        {
            return new List<string>();
        }
    }

    private static bool IsAxisObject(DeviceObjectInstance obj)
        => (obj.ObjectId.Flags & DeviceObjectTypeFlags.Axis) != 0
           || obj.ObjectType == ObjectGuid.XAxis
           || obj.ObjectType == ObjectGuid.YAxis
           || obj.ObjectType == ObjectGuid.ZAxis
           || obj.ObjectType == ObjectGuid.RxAxis
           || obj.ObjectType == ObjectGuid.RyAxis
           || obj.ObjectType == ObjectGuid.RzAxis
           || obj.ObjectType == ObjectGuid.Slider;

    public static List<int> GetDeviceButtons(Joystick js)
    {
        try
        {
            return js.GetObjects()
                .Where(o => o.ObjectType == ObjectGuid.Button)
                .Select(o => o.ObjectId.InstanceNumber)
                .Where(index => index >= 0 && index < 128)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
        }
        catch
        {
            return new List<int>();
        }
    }

    public static List<int> GetDevicePovs(Joystick js)
    {
        try
        {
            return js.GetObjects()
                .Where(o => o.ObjectType == ObjectGuid.PovController)
                .Select(o => o.ObjectId.InstanceNumber)
                .Where(index => index >= 0 && index < 4)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
        }
        catch
        {
            return new List<int>();
        }
    }

    private static string? MapAxisObject(DeviceObjectInstance obj)
    {
        if (obj.ObjectType == ObjectGuid.XAxis) return "X";
        if (obj.ObjectType == ObjectGuid.YAxis) return "Y";
        if (obj.ObjectType == ObjectGuid.ZAxis) return "Z";
        if (obj.ObjectType == ObjectGuid.RxAxis) return "RotationX";
        if (obj.ObjectType == ObjectGuid.RyAxis) return "RotationY";
        if (obj.ObjectType == ObjectGuid.RzAxis) return "RotationZ";
        if (obj.ObjectType == ObjectGuid.Slider)
        {
            return (JoystickOffset)obj.Offset switch
            {
                JoystickOffset.Sliders0 => "Slider0",
                JoystickOffset.Sliders1 => "Slider1",
                _ => null
            };
        }
        return null;
    }

    private static string? MapAxisOffset(int offset) => (JoystickOffset)offset switch
    {
        JoystickOffset.X => "X",
        JoystickOffset.Y => "Y",
        JoystickOffset.Z => "Z",
        JoystickOffset.RotationX => "RotationX",
        JoystickOffset.RotationY => "RotationY",
        JoystickOffset.RotationZ => "RotationZ",
        JoystickOffset.Sliders0 => "Slider0",
        JoystickOffset.Sliders1 => "Slider1",
        _ => null
    };

    public static string MapAxisName(string directInputName)
    {
        var n = directInputName.Trim();

        if (n.Contains("X Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationX";
        if (n.Contains("Y Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationY";
        if (n.Contains("Z Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationZ";

        if (n.Contains("X Axis", StringComparison.OrdinalIgnoreCase)) return "X";
        if (n.Contains("Y Axis", StringComparison.OrdinalIgnoreCase)) return "Y";
        if (n.Contains("Z Axis", StringComparison.OrdinalIgnoreCase)) return "Z";

        if (n.Contains("Slider", StringComparison.OrdinalIgnoreCase)) return "Slider0";

        return n;
    }

    public static int GetAxisRaw(JoystickState state, string axisName) => axisName switch
    {
        "X" => state.X,
        "Y" => state.Y,
        "Z" => state.Z,
        "RotationX" => state.RotationX,
        "RotationY" => state.RotationY,
        "RotationZ" => state.RotationZ,
        "Slider0" => GetSlider(state, 0),
        "Slider1" => GetSlider(state, 1),
        CombinedSlidersAxis => (GetSlider(state, 0) + GetSlider(state, 1)) / 2,
        CombinedThrottleAxis => (state.Z + state.RotationZ) / 2,
        PitchAndRollAxis => GetCenteredStickMagnitude(state),
        _ => 0
    };

    private static int GetSlider(JoystickState state, int index)
        => state.Sliders != null && state.Sliders.Length > index
            ? state.Sliders[index]
            : 0;

    private static int GetCenteredStickMagnitude(JoystickState state)
    {
        const double center = 32767.5;
        double x = (Math.Clamp(state.X, 0, 65535) - center) / center;
        double y = (Math.Clamp(state.Y, 0, 65535) - center) / center;

        // Radial displacement: center = 0 and any full axis edge = 1.
        double magnitude = Math.Clamp(Math.Sqrt((x * x) + (y * y)), 0.0, 1.0);
        return (int)Math.Round(magnitude * 65535.0);
    }
}
