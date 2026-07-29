namespace JFlightShaker.Config;

public sealed record VirtualHatDefinition(
    string Name,
    int Up,
    int Right,
    int Down,
    int Left);

public sealed record ButtonSelectorDefinition(
    string Name,
    IReadOnlyDictionary<int, string> Positions,
    bool ShowDigitalValues = false);

public sealed class JoystickTestProfile
{
    public string Name { get; init; } = "Generic";
    public IReadOnlyDictionary<int, string> ButtonNames { get; init; }
        = new Dictionary<int, string>();
    public IReadOnlyDictionary<string, string> AxisNames { get; init; }
        = new Dictionary<string, string>();
    public IReadOnlyList<VirtualHatDefinition> VirtualHats { get; init; }
        = Array.Empty<VirtualHatDefinition>();
    public IReadOnlyList<ButtonSelectorDefinition> ButtonSelectors { get; init; }
        = Array.Empty<ButtonSelectorDefinition>();
    public IReadOnlySet<int> HiddenButtons { get; init; } = new HashSet<int>();
    public IReadOnlySet<string> ByteAxes { get; init; } = new HashSet<string>();

    public static JoystickTestProfile Generic { get; } = new();

    public static JoystickTestProfile CreateX56(string deviceName)
        => deviceName.Contains("Throttle", StringComparison.OrdinalIgnoreCase)
            ? CreateX56Throttle()
            : CreateX56Stick();

    public static JoystickTestProfile CreateX56Stick() => new()
    {
        Name = "Logitech/Saitek X56 Stick",
        ButtonNames = new Dictionary<int, string>
        {
            [0] = "Trigger",
            [1] = "A",
            [2] = "B",
            [3] = "C Push",
            [4] = "D",
            [5] = "Pinkie"
        },
        AxisNames = new Dictionary<string, string>
        {
            ["X"] = "X — Roll",
            ["Y"] = "Y — Pitch",
            ["RotationX"] = "Rx — C Left/Right",
            ["RotationY"] = "Ry — C Up/Down",
            ["RotationZ"] = "Rz — Twist"
        },
        VirtualHats = new[]
        {
            // Diagram JOY numbers are 1-based; runtime indices are 0-based.
            new VirtualHatDefinition("Hat 1", 6, 9, 7, 8),
            new VirtualHatDefinition("Hat 2", 10, 11, 12, 13)
        },
        // The driver advertises these unused slots on the Stick.
        HiddenButtons = new HashSet<int> { 13, 14, 15, 16 }
    };

    public static JoystickTestProfile CreateX56Throttle() => new()
    {
        Name = "Logitech/Saitek X56 Throttle",
        ButtonNames = new Dictionary<int, string>
        {
            [0] = "E",
            [1] = "Rotary 1 Push",
            [2] = "Rotary 2 Push",
            [3] = "I",
            [4] = "H",
            [27] = "K1 Up",
            [28] = "K1 Down",
            [29] = "Scroll Up",
            [30] = "Scroll Down",
            [31] = "Throttle Stick Push",
            [32] = "SLD"
        },
        AxisNames = new Dictionary<string, string>
        {
            ["Slider0"] = "Rotary 3",
            ["Slider1"] = "Rotary 4",
            ["RotationZ"] = "Rotary 2"
        },
        ByteAxes = new HashSet<string> { "Slider0", "Slider1" },
        VirtualHats = new[]
        {
            new VirtualHatDefinition("Hat 3", 23, 24, 25, 26),
            new VirtualHatDefinition("Hat 4", 19, 20, 21, 22)
        },
        ButtonSelectors = new[]
        {
            new ButtonSelectorDefinition("Mode", new Dictionary<int, string>
            {
                [33] = "Mode 1",
                [34] = "Mode 2",
                [35] = "Mode 3"
            })
        }
    };

    public override string ToString() => Name;
}
