using JFlightShaker.Config;
using JFlightShaker.Helpers;
using SharpDX.DirectInput;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace JFlightShaker.UI;

public partial class DeviceTestWindow : Window
{
    private sealed record AxisControls(ProgressBar Bar, TextBlock Value);
    private sealed record SelectorControls(Border Box, TextBlock Value);

    private readonly Guid _deviceGuid;
    private readonly IReadOnlyList<string> _axes;
    private readonly IReadOnlyList<int> _buttonIndices;
    private readonly IReadOnlyList<int> _povIndices;
    private readonly Dictionary<string, AxisControls> _axisControls = new();
    private readonly Dictionary<int, Border> _buttonIndicators = new();
    private readonly Dictionary<int, TextBlock> _povValues = new();
    private readonly Dictionary<VirtualHatDefinition, TextBlock> _virtualHatValues = new();
    private readonly Dictionary<ButtonSelectorDefinition, SelectorControls> _selectorValues = new();
    private readonly DispatcherTimer _timer;
    private readonly object _stateLock = new();
    private JoystickState? _latestState;
    private int[] _previousAxes = Array.Empty<int>();
    private bool[] _previousButtons = Array.Empty<bool>();
    private int[] _previousPovs = Array.Empty<int>();
    private bool _controlsCreated;
    private JoystickTestProfile _profile = JoystickTestProfile.Generic;

    public DeviceTestWindow(
        Guid deviceGuid,
        string deviceName,
        IReadOnlyList<string> axes,
        IReadOnlyList<int> buttonIndices,
        IReadOnlyList<int> povIndices)
    {
        InitializeComponent();
        _deviceGuid = deviceGuid;
        _axes = axes.Where(a => a != BindingUiHelper.CombinedSlidersAxis &&
                                a != BindingUiHelper.CombinedThrottleAxis &&
                                a != BindingUiHelper.PitchAndRollAxis).ToList();
        _buttonIndices = buttonIndices;
        _povIndices = povIndices;

        Title = $"Test Device - {deviceName}";
        DeviceNameText.Text = deviceName;
        GuidText.Text = $"GUID: {deviceGuid}";

        var profiles = new[]
        {
            JoystickTestProfile.Generic,
            JoystickTestProfile.CreateX56Stick(),
            JoystickTestProfile.CreateX56Throttle()
        };
        ProfileCombo.ItemsSource = profiles;
        ProfileCombo.SelectedItem = profiles[0];
        ProfileCombo.SelectionChanged += (_, _) =>
        {
            _profile = ProfileCombo.SelectedItem as JoystickTestProfile
                ?? JoystickTestProfile.Generic;
            RebuildControls();
        };
        _profile = ProfileCombo.SelectedItem as JoystickTestProfile
            ?? JoystickTestProfile.Generic;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => RefreshUi();
        _timer.Start();
    }

    public void ReceiveState(Guid deviceGuid, JoystickState state)
    {
        if (deviceGuid != _deviceGuid) return;
        lock (_stateLock)
            _latestState = state;
    }

    private void RefreshUi()
    {
        JoystickState? state;
        lock (_stateLock)
            state = _latestState;
        if (state == null) return;

        if (!_controlsCreated)
            CreateControls(state);

        var axisValues = _axes.Select(a => BindingUiHelper.GetAxisRaw(state, a)).ToArray();
        for (int i = 0; i < _axes.Count; i++)
        {
            int raw = Math.Clamp(axisValues[i], 0, 65535);
            double normalized = raw / 65535.0;
            var controls = _axisControls[_axes[i]];
            controls.Bar.Value = raw;
            controls.Value.Text = _profile.ByteAxes.Contains(_axes[i])
                ? $"{(int)Math.Round(normalized * 255.0)} / 255"
                : $"{raw}  ({normalized.ToString("0.000", CultureInfo.InvariantCulture)})";

            if (_previousAxes.Length == axisValues.Length && Math.Abs(raw - _previousAxes[i]) > 256)
                LastInputText.Text = $"Last input: {_axes[i]} = {normalized:0.000}";
        }

        var buttons = state.Buttons ?? Array.Empty<bool>();
        foreach (int i in _buttonIndices)
        {
            if (i < 0 || i >= buttons.Length || !_buttonIndicators.TryGetValue(i, out var indicator))
                continue;

            indicator.Background = buttons[i]
                ? new SolidColorBrush(Color.FromRgb(74, 170, 92))
                : new SolidColorBrush(Color.FromRgb(68, 68, 68));
            if (_previousButtons.Length == buttons.Length && buttons[i] != _previousButtons[i])
                LastInputText.Text = $"Last input: Button {i + 1} {(buttons[i] ? "Down" : "Up")}";
        }

        var povs = state.PointOfViewControllers ?? Array.Empty<int>();
        foreach (int i in _povIndices)
        {
            if (i < 0 || i >= povs.Length || !_povValues.TryGetValue(i, out var povValue))
                continue;

            povValue.Text = $"POV {i}: {GetPovDirection(povs[i])}";
            if (_previousPovs.Length == povs.Length && povs[i] != _previousPovs[i])
                LastInputText.Text = $"Last input: POV {i} = {GetPovDirection(povs[i])}";
        }

        foreach (var pair in _virtualHatValues)
        {
            string direction = GetVirtualHatDirection(pair.Key, buttons);
            pair.Value.Text = $"{pair.Key.Name}: {direction}";
            if (_previousButtons.Length == buttons.Length)
            {
                string previousDirection = GetVirtualHatDirection(pair.Key, _previousButtons);
                if (direction != previousDirection)
                    LastInputText.Text = $"Last input: {pair.Key.Name} = {direction}";
            }
        }

        foreach (var pair in _selectorValues)
        {
            string position = pair.Key.ShowDigitalValues
                ? string.Join("  |  ", pair.Key.Positions.Select(item =>
                    $"{item.Value}: {(IsPressed(item.Key, buttons) ? 255 : 0)}"))
                : pair.Key.Positions
                    .Where(item => IsPressed(item.Key, buttons))
                    .Select(item => item.Value)
                    .FirstOrDefault() ?? "Centered";
            pair.Value.Value.Text = position;
            pair.Value.Box.Background = GetSelectorColor(pair.Key.Name, position);
        }

        _previousAxes = axisValues;
        _previousButtons = buttons.ToArray();
        _previousPovs = povs.ToArray();
    }

    private void CreateControls(JoystickState state)
    {
        foreach (var axis in _axes)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            string axisLabel = _profile.AxisNames.TryGetValue(axis, out var friendlyAxis)
                ? friendlyAxis
                : axis;
            var name = new TextBlock { Text = axisLabel, VerticalAlignment = VerticalAlignment.Center };
            var bar = new ProgressBar { Minimum = 0, Maximum = 65535, Height = 16, Margin = new Thickness(6, 0, 8, 0) };
            var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 0); Grid.SetColumn(bar, 1); Grid.SetColumn(value, 2);
            grid.Children.Add(name); grid.Children.Add(bar); grid.Children.Add(value);
            AxesPanel.Children.Add(grid);
            _axisControls[axis] = new AxisControls(bar, value);
        }

        foreach (int i in _buttonIndices)
        {
            if (_profile.HiddenButtons.Contains(i))
                continue;
            if (_profile.VirtualHats.Any(hat =>
                    hat.Up == i || hat.Right == i || hat.Down == i || hat.Left == i))
                continue;
            if (_profile.ButtonSelectors.Any(selector => selector.Positions.ContainsKey(i)))
                continue;

            string label = _profile.ButtonNames.TryGetValue(i, out var friendlyName)
                ? $"{i + 1} — {friendlyName}"
                : $"Button {i + 1}";
            var border = new Border {
                MinWidth = 82, Height = 30, Margin = new Thickness(2),
                Padding = new Thickness(6, 0, 6, 0),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                Child = new TextBlock {
                    Text = label, Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            ButtonsPanel.Children.Add(border);
            _buttonIndicators[i] = border;
        }

        foreach (int i in _povIndices)
        {
            var text = new TextBlock { Margin = new Thickness(0, 2, 0, 2) };
            PovPanel.Children.Add(text);
            _povValues[i] = text;
        }

        foreach (var hat in _profile.VirtualHats)
        {
            var text = new TextBlock { Margin = new Thickness(0, 2, 0, 2) };
            PovPanel.Children.Add(text);
            _virtualHatValues[hat] = text;
        }
        foreach (var selector in _profile.ButtonSelectors)
        {
            var label = new TextBlock {
                Text = selector.Name,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var value = new TextBlock {
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var box = new Border {
                Width = selector.ShowDigitalValues ? 190 : 86,
                Height = 28,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                Child = value
            };
            var panel = new StackPanel {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(3, 3, 14, 3)
            };
            panel.Children.Add(label);
            panel.Children.Add(box);
            SelectorsPanel.Children.Add(panel);
            _selectorValues[selector] = new SelectorControls(box, value);
        }
        _controlsCreated = true;
    }

    private void RebuildControls()
    {
        AxesPanel.Children.Clear();
        ButtonsPanel.Children.Clear();
        SelectorsPanel.Children.Clear();
        PovPanel.Children.Clear();
        _axisControls.Clear();
        _buttonIndicators.Clear();
        _povValues.Clear();
        _virtualHatValues.Clear();
        _selectorValues.Clear();
        _controlsCreated = false;
    }

    private static string GetVirtualHatDirection(
        VirtualHatDefinition hat,
        IReadOnlyList<bool> buttons)
    {
        bool up = IsPressed(hat.Up, buttons);
        bool right = IsPressed(hat.Right, buttons);
        bool down = IsPressed(hat.Down, buttons);
        bool left = IsPressed(hat.Left, buttons);

        if (up && right) return "NE";
        if (down && right) return "SE";
        if (down && left) return "SW";
        if (up && left) return "NW";
        if (up) return "N";
        if (right) return "E";
        if (down) return "S";
        if (left) return "W";
        return "Centered";
    }

    private static bool IsPressed(int index, IReadOnlyList<bool> buttons)
        => index >= 0 && index < buttons.Count && buttons[index];

    private static Brush GetSelectorColor(string selectorName, string position)
    {
        if (!selectorName.Equals("Mode", StringComparison.OrdinalIgnoreCase))
            return position != "Centered" && position.Contains(": 255", StringComparison.Ordinal)
                ? new SolidColorBrush(Color.FromRgb(80, 125, 150))
                : new SolidColorBrush(Color.FromRgb(68, 68, 68));

        return position switch
        {
            "Mode 1" => new SolidColorBrush(Color.FromRgb(70, 115, 165)),
            "Mode 2" => new SolidColorBrush(Color.FromRgb(76, 145, 94)),
            "Mode 3" => new SolidColorBrush(Color.FromRgb(180, 116, 62)),
            _ => new SolidColorBrush(Color.FromRgb(68, 68, 68))
        };
    }

    private static string GetPovDirection(int value)
    {
        if (value < 0) return "Centered";
        string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int index = (int)Math.Round(value / 4500.0) % 8;
        return $"{directions[index]} ({value / 100.0:0}°)";
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
