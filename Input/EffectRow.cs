using JFlightShaker.Enum;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JFlightShaker.UI;

public sealed class EffectRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<EffectRow, float>? IntensityChanged;

    public RumbleEffectType Effect { get; }
    public string EffectName { get; private set; }
    public string Description { get; }
    public bool CanTest => Effect != RumbleEffectType.MuteEffects;

    private string _bindingText = "Not Set";
    public string BindingText
    {
        get => _bindingText;
        private set => Set(ref _bindingText, value);
    }

    private string _statusText = "Stopped";
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusBrush)));
        }
    }
    public string DisplayStatus => UiText.Get($"Status.{StatusText}");

    public System.Windows.Media.Brush StatusBrush => StatusText switch
    {
        "Active" => new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(42, 205, 76)),
        "Running" or "On" => new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(92, 158, 105)),
        _ => new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(190, 105, 105))
    };

    private float _intensity;
    public float Intensity
    {
        get => _intensity;
        set
        {
            value = Math.Clamp(value, 0f, 1f);
            if (Equals(_intensity, value)) return;
            _intensity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Intensity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntensityPercent)));
            IntensityChanged?.Invoke(this, value);
        }
    }
    public string IntensityPercent => $"{Math.Round(Intensity * 100f):0} %";

    public bool SupportsIntensity => Effect != RumbleEffectType.MuteEffects;
    public bool IsMuteEffect => Effect == RumbleEffectType.MuteEffects;
    public bool CanAdjustIntensity => SupportsIntensity && IsBound;

    private bool _isEffectEnabled = true;
    public bool IsEffectEnabled
    {
        get => _isEffectEnabled;
        private set => Set(ref _isEffectEnabled, value);
    }

    private bool _isBound;
    public bool IsBound
    {
        get => _isBound;
        private set
        {
            if (_isBound == value) return;
            _isBound = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBound)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAdjustIntensity)));
        }
    }

    public EffectRow(RumbleEffectType effect, string effectName)
    {
        Effect = effect;
        EffectName = effectName;
        Description = effect switch
        {
            RumbleEffectType.ThrottleAxis =>
                "Engine vibration follows the throttle position. Above 85% it adds a strong afterburner sensation.",
            RumbleEffectType.PitchAndRoll =>
                "Vibrates with combined stick inclination. Roll follows the corresponding shaker, and strength increases moderately with throttle as a speed estimate.",
            RumbleEffectType.Gun =>
                "Continuous, aggressive gun vibration while the configured button is held.",
            RumbleEffectType.Missile =>
                "Plays a short missile-launch pulse whenever the configured button is pressed.",
            RumbleEffectType.HighGTurn =>
                "Activates while its button is held and pitch exceeds the configured threshold. Roll does not activate it.",
            RumbleEffectType.MuteEffects =>
                "Silences all rumble while the configured controller button or keyboard key is active.",
            _ => effectName
        };

        SetUnbound();
    }

    public void SetBound(string bindingText, float intensity, bool enabled = true)
    {
        BindingText = bindingText;
        Intensity = intensity;
        IsEffectEnabled = enabled;
        IsBound = true;
    }

    public void SetUnbound()
    {
        BindingText = "Not Set";
        IsBound = false;
        IsEffectEnabled = true;
        Intensity = SupportsIntensity ? 1f : 0f;
        StatusText = "Stopped";
    }

    public void SetRunning(bool running)
    {
        StatusText = running ? "Running" : "Stopped";
    }

    public void SetStatus(string status)
    {
        StatusText = status;
    }

    public void SetEffectEnabled(bool enabled)
    {
        IsEffectEnabled = enabled;
    }

    public void RefreshLanguage()
    {
        EffectName = UiText.Get($"Effect.{Effect}");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayStatus)));
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
