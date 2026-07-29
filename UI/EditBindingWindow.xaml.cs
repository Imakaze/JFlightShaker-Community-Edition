using JFlightShaker.Config;
using JFlightShaker.Enum;
using JFlightShaker.Helpers;
using JFlightShaker.Input;
using SharpDX.DirectInput;
using System.Windows;

namespace JFlightShaker.UI;

public sealed class DeviceOption
{
    public Guid Guid { get; init; }
    public string Name { get; init; } = "";

    public override string ToString() => Name;
}

public sealed class KindOption
{
    public BindingKind Kind { get; init; }
    public string Label { get; init; } = "";

    public override string ToString() => Label;
}

public partial class EditBindingWindow : Window
{
    private readonly IReadOnlyList<DeviceOption> _devices;
    private readonly Func<Guid, Joystick?> _openJoystick;
    private readonly BindingConfig _binding;
    private readonly BindingConfig _originalBinding;
    private readonly IReadOnlyList<BindingKind> _allowedKinds;
    private readonly string _defaultAxisName;
    private readonly float _defaultHighGThreshold;
    public bool ResetDefaultsRequested { get; private set; }

    public EditBindingWindow(
        IReadOnlyList<DeviceOption> devices,
        Func<Guid, Joystick?> openJoystick,
        BindingConfig binding,
        IReadOnlyList<BindingKind> allowedKinds,
        string effectName,
        string defaultAxisName,
        float defaultHighGThreshold
    )
    {
        InitializeComponent();

        Title = $"{UiText.Get("EditBinding")} - {effectName}";
        DeviceLabel.Text = UiText.Get("Device");
        TypeLabel.Text = UiText.Get("Type");
        EditControlBtn.Content = UiText.Get("EditControl");
        ResetDefaultsBtn.Content = UiText.Get("Reset");
        CancelBtn.Content = UiText.Get("Cancel");
        SaveBtn.Content = UiText.Get("Save");

        _devices = devices;
        _openJoystick = openJoystick;
        _binding = binding;
        _originalBinding = CopyOf(binding);
        _allowedKinds = allowedKinds;
        _defaultAxisName = defaultAxisName;
        _defaultHighGThreshold = defaultHighGThreshold;

        DeviceCombo.ItemsSource = _devices;
        RefreshKindOptions();

        DeviceCombo.SelectionChanged += (_, _) => OnDeviceChanged();
        KindCombo.SelectionChanged += (_, _) => OnKindChanged();
        EditControlBtn.Click += (_, _) => OnEditControl();
        ResetDefaultsBtn.Click += (_, _) => OnResetDefaults();

        CancelBtn.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        SaveBtn.Click += (_, _) => OnSave();

        LoadInitialState();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DialogResult != true)
        {
            CopyTo(_originalBinding, _binding);
            ResetDefaultsRequested = false;
        }

        base.OnClosed(e);
    }

    private static BindingConfig CopyOf(BindingConfig source)
    {
        var copy = new BindingConfig();
        CopyTo(source, copy);
        return copy;
    }

    private static void CopyTo(BindingConfig source, BindingConfig target)
    {
        target.DeviceGuid = source.DeviceGuid;
        target.DeviceName = source.DeviceName;
        target.Kind = source.Kind;
        target.AxisName = source.AxisName;
        target.AxisMin = source.AxisMin;
        target.AxisMax = source.AxisMax;
        target.InvertAxis = source.InvertAxis;
        target.ButtonIndex = source.ButtonIndex;
        target.Trigger = source.Trigger;
        target.ActivationThreshold = source.ActivationThreshold;
        target.Effect = source.Effect;
        target.Intensity = source.Intensity;
        target.Enabled = source.Enabled;
    }

    private void OnResetDefaults()
    {
        var result = MessageBox.Show(
            "Restore the default settings for this effect?\n\nThe selected device and control will be kept.",
            "Reset defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _binding.Intensity = 1f;
        _binding.Enabled = true;
        _binding.AxisMin = _binding.Kind == BindingKind.Axis ? 0f : null;
        _binding.AxisMax = _binding.Kind == BindingKind.Axis ? 1f : null;
        _binding.InvertAxis = false;
        _binding.Trigger = EffectBindingRules.GetAllowedTriggers(_binding.Effect).FirstOrDefault();
        _binding.ActivationThreshold = _binding.Effect == RumbleEffectType.HighGTurn
            ? _defaultHighGThreshold
            : null;
        ResetDefaultsRequested = true;

        RefreshKindOptions();
        MessageBox.Show(
            "Defaults restored. Press Save to apply them.",
            "Reset defaults",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void LoadInitialState()
    {
        // Device
        if (_binding.DeviceGuid is Guid g)
        {
            DeviceCombo.SelectedItem = _devices.FirstOrDefault(x => x.Guid == g);
        }

        if (DeviceCombo.SelectedItem == null && _devices.Count > 0)
            DeviceCombo.SelectedIndex = 0;

        // Kind
        var initialKind = _allowedKinds.Contains(_binding.Kind)
            ? _binding.Kind
            : _allowedKinds.FirstOrDefault();
        SelectKind(initialKind);

        // Load axes for current device
        OnDeviceChanged();
    }

    private void OnKindChanged()
    {
        var kind = GetSelectedKind();
        if (kind is null) return;

        if (kind == BindingKind.Axis)
        {
            _binding.ButtonIndex = null;
            _binding.Trigger = TriggerType.Hold;
        }
        else
        {
            _binding.AxisName = null;
            _binding.AxisMin = null;
            _binding.AxisMax = null;
            ClampTrigger();
        }
    }

    private void OnEditControl()
    {
        if (DeviceCombo.SelectedItem is not DeviceOption dev)
        {
            MessageBox.Show("Select a device.");
            return;
        }

        var kind = GetSelectedKind();
        if (kind is null)
        {
            MessageBox.Show("Select a type.");
            return;
        }

        _binding.DeviceGuid = dev.Guid;
        _binding.DeviceName = dev.Name.Trim();
        _binding.Kind = kind.Value;

        Window? editor = kind.Value switch
        {
            BindingKind.Axis => new AxisBindingWindow(_devices, _openJoystick, _binding) { Owner = this },

            BindingKind.Button when dev.Guid == InputDeviceIds.Keyboard =>
                new KeyboardBindingWindow(_binding) { Owner = this },

            BindingKind.Button => new ButtonBindingWindow(
                _binding,
                dev.Guid
            )
            { Owner = this },

            _ => null
        };

        if (editor == null) return;

        var ok = editor.ShowDialog() == true;
        if (!ok) return;

        if (kind.Value == BindingKind.Axis)
        {
            _binding.ButtonIndex = null;
        }
        else
        {
            _binding.AxisName = null;
            _binding.AxisMin = null;
            _binding.AxisMax = null;
            _binding.InvertAxis = false;
            ClampTrigger();
        }

        RefreshKindOptions();
    }

    private void OnDeviceChanged()
    {
        // TODO: for binding display info
    }

    private List<string> GetDeviceAxes(Guid deviceGuid)
    {
        return BindingUiHelper.GetDeviceAxes(_openJoystick, deviceGuid);
    }

    private void OnSave()
    {
        if (DeviceCombo.SelectedItem is not DeviceOption dev)
        {
            MessageBox.Show("Select a device.");
            return;
        }

        var kind = GetSelectedKind();
        if (kind is null)
        {
            MessageBox.Show("Select a type.");
            return;
        }

        _binding.DeviceGuid = dev.Guid;
        _binding.DeviceName = dev.Name.Trim();
        _binding.Kind = kind.Value;
        if (kind.Value == BindingKind.Axis)
        {
            _binding.ButtonIndex = null;

            if (string.IsNullOrWhiteSpace(_binding.AxisName))
            {
                MessageBox.Show("Select an axis with Edit Control.");
                return;
            }
        }
        else
        {
            _binding.AxisMin = null;
            _binding.AxisMax = null;
            _binding.InvertAxis = false;
            ClampTrigger();

            if (_binding.ButtonIndex is null)
            {
                MessageBox.Show("Select a button with Edit Control.");
                return;
            }
        }

        DialogResult = true;
        Close();
    }

    private void RefreshKindOptions()
    {
        var selectedKind = GetSelectedKind();
        var options = _allowedKinds
            .Select(kind => new KindOption
            {
                Kind = kind,
                Label = kind == BindingKind.Axis
                    ? GetAxisKindLabel()
                    : UiText.Get("Button").TrimEnd(':')
            })
            .ToList();

        KindCombo.ItemsSource = options;
        SelectKind(selectedKind ?? _binding.Kind);
    }

    private void SelectKind(BindingKind? kind)
    {
        if (kind is null) return;
        if (KindCombo.ItemsSource is not IEnumerable<KindOption> options) return;
        KindCombo.SelectedItem = options.FirstOrDefault(option => option.Kind == kind);
    }

    private BindingKind? GetSelectedKind()
    {
        return KindCombo.SelectedItem is KindOption option ? option.Kind : null;
    }

    private string GetAxisKindLabel()
    {
        var axisName = string.IsNullOrWhiteSpace(_binding.AxisName)
            ? _defaultAxisName
            : _binding.AxisName;

        axisName = string.IsNullOrWhiteSpace(axisName) ? "RotationX" : axisName;
        return $"{UiText.Get("Axis")} | {axisName}";
    }

    private void ClampTrigger()
    {
        var allowed = EffectBindingRules.GetAllowedTriggers(_binding.Effect);
        if (!allowed.Contains(_binding.Trigger))
            _binding.Trigger = allowed.FirstOrDefault();
    }
}
