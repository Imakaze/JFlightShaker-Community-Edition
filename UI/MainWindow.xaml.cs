using JFlightShaker.Audio;
using JFlightShaker.Config;
using JFlightShaker.Core;
using JFlightShaker.Enum;
using JFlightShaker.Helpers;
using JFlightShaker.Input;
using JFlightShaker.Service;
using JFlightShaker.UI;
using NAudio.CoreAudioApi;
using SharpDX.DirectInput;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace JFlightShaker;

public partial class MainWindow : Window
{
    private readonly AudioDeviceService _audioSvc = new();
    private readonly DirectInputService _inputSvc = new();

    private readonly EffectMixer _mixer = new();

    private RumbleEngine? _engine;
    private BindingEvaluator? _eval;
    private readonly ConfigStoreService _store;
    private MultiJoystickPoller? _poller;
    private SharpDX.DirectInput.Keyboard? _keyboard;
    private readonly HashSet<SharpDX.DirectInput.Key> _previousKeyboardKeys = new();
    private readonly GlobalInputLogger _logger;

    public event Action<Guid, int>? ButtonPressedEdge;

    // UI
    private EffectRow? SelectedEffectRow => BindingsGrid.SelectedItem as EffectRow;
    private readonly ObservableCollection<EffectRow> _effectRows = new();
    private bool _isRunning;
    private string _language = "es";

    public bool IsRunning => _isRunning;

    // Thread | Timers
    private readonly object _sync = new();
    private DispatcherTimer? _engineTimer;
    private DispatcherTimer? _connectionTimer;
    private long _lastTick = Environment.TickCount64;

    // Profile
    private ActiveProfile _profile = new();

    // Controller Event States
    private readonly Dictionary<(Guid dev, int btn), RumbleEffect> _activeHolds = new();
    private readonly HashSet<(Guid dev, int btn)> _activeMuteHolds = new();
    private readonly HashSet<(Guid dev, int btn)> _activeHighGHolds = new();
    private readonly Dictionary<(Guid dev, int btn), bool> _highGThresholdStates = new();
    private bool _muteToggleActive;
    private bool _isMuted;
    private long _missileActiveUntilMs;

    // Effect preview state
    private RumbleEffectType? _previewEffect;
    private long _previewStartedAtMs;
    private long _previewUntilMs;
    private bool _previewOwnsEngine;

    // Device States
    private readonly Dictionary<Guid, JoystickState> _latestStateByDevice = new();
    private readonly Dictionary<Guid, bool[]> _prevButtonsByDevice = new();
    private readonly Dictionary<Guid, string> _deviceNamesByGuid = new();
    private readonly Dictionary<Guid, IReadOnlyList<string>> _axisNamesByGuid = new();
    private readonly Dictionary<Guid, IReadOnlyList<int>> _buttonIndicesByGuid = new();
    private readonly Dictionary<Guid, IReadOnlyList<int>> _povIndicesByGuid = new();
    private readonly HashSet<DeviceTestWindow> _deviceTestWindows = new();

    private readonly List<BindingDefinition> _bindings = new();
    private event Action<Guid, JoystickState>? JoystickStateReceived;

    private bool _isRestoringSelections;
    private Guid? _selectedInputDeviceGuid;

    public MainWindow()
    {
        InitializeComponent();
        Title = BuildInfo.FullName;
        InitializeEffects();
        ApplyLanguage("es");
        SetVersionLabel();

        try
        {
            _store = new ConfigStoreService();
            _logger = new GlobalInputLogger(DeviceName);

            BindingsGrid.ItemsSource = _effectRows;

            InitializeActionButtons();

            StartStopBtn.Click += (_, _) => ToggleStartStop();
            OpenConfigBtn.Click += (_, _) => OpenConfigFolder();
            TestDeviceBtn.Click += (_, _) => OpenDeviceTestWindow();
            RefreshDevicesBtn.Click += (_, _) => RefreshDevicesFromUi();
            RefreshAudioBtn.Click += (_, _) => RefreshAudioDevicesFromUi();

            UpdateStartStopUI();

            AudioDevices.SelectionChanged += AudioDevices_SelectionChanged;
            InputDevices.SelectionChanged += (_, _) =>
            {
                if (InputDevices.SelectedItem is DeviceOption option)
                    _selectedInputDeviceGuid = option.Guid;
            };

            RefreshDevices();
            LoadActiveProfileAndApply();
            StartConnectionMonitor();
        }
        catch (Exception ex)
        {
            AppLog.Error("Main window initialization failed.", ex);
            MessageBox.Show(ex.ToString(), "Startup error");
            throw;
        }
    }

    private void SetVersionLabel()
    {
        VersionLabel.Text = BuildInfo.FullName;
    }

    private void LanguageCombo_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsInitialized ||
            LanguageCombo.SelectedItem is not System.Windows.Controls.ComboBoxItem item ||
            item.Tag is not string language)
            return;
        ApplyLanguage(language);
    }

    private void ApplyLanguage(string language)
    {
        _language = language;
        UiText.Language = language;
        var text = language switch
        {
            "en" => (
                Subtitle: "Universal haptic engine for flight simulators", Audio: "AUDIO OUTPUT",
                Input: "INPUT DEVICE", RefreshAudio: "Refresh audio devices",
                RefreshInput: "Refresh input devices", TestDevice: "Test",
                Effect: "Effect", Binding: "Binding", Intensity: "Intensity",
                Test: "Test", Status: "Status", LastInput: "Last input:",
                Folder: "Open configuration folder", Language: "Language:",
                Start: "Start", Stop: "Stop"),
            "ca" => (
                Subtitle: "Motor hàptic universal per a simuladors de vol", Audio: "SORTIDA D'ÀUDIO",
                Input: "DISPOSITIU D'ENTRADA", RefreshAudio: "Actualitza dispositius d'àudio",
                RefreshInput: "Actualitza dispositius d'entrada", TestDevice: "Prova",
                Effect: "Efecte", Binding: "Assignació", Intensity: "Intensitat",
                Test: "Prova", Status: "Estat", LastInput: "Última entrada:",
                Folder: "Obre la carpeta de configuració", Language: "Idioma:",
                Start: "Inicia", Stop: "Atura"),
            "fr" => (
                Subtitle: "Moteur haptique universel pour simulateurs de vol", Audio: "SORTIE AUDIO",
                Input: "PÉRIPHÉRIQUE D'ENTRÉE", RefreshAudio: "Actualiser les périphériques audio",
                RefreshInput: "Actualiser les périphériques d'entrée", TestDevice: "Tester",
                Effect: "Effet", Binding: "Affectation", Intensity: "Intensité",
                Test: "Tester", Status: "État", LastInput: "Dernière entrée :",
                Folder: "Ouvrir le dossier de configuration", Language: "Langue :",
                Start: "Démarrer", Stop: "Arrêter"),
            "de" => (
                Subtitle: "Universelle Haptik-Engine für Flugsimulatoren", Audio: "AUDIOAUSGABE",
                Input: "EINGABEGERÄT", RefreshAudio: "Audiogeräte aktualisieren",
                RefreshInput: "Eingabegeräte aktualisieren", TestDevice: "Testen",
                Effect: "Effekt", Binding: "Belegung", Intensity: "Intensität",
                Test: "Test", Status: "Status", LastInput: "Letzte Eingabe:",
                Folder: "Konfigurationsordner öffnen", Language: "Sprache:",
                Start: "Start", Stop: "Stopp"),
            "it" => (
                Subtitle: "Motore aptico universale per simulatori di volo", Audio: "USCITA AUDIO",
                Input: "DISPOSITIVO DI INPUT", RefreshAudio: "Aggiorna dispositivi audio",
                RefreshInput: "Aggiorna dispositivi di input", TestDevice: "Prova",
                Effect: "Effetto", Binding: "Assegnazione", Intensity: "Intensità",
                Test: "Prova", Status: "Stato", LastInput: "Ultimo input:",
                Folder: "Apri cartella configurazione", Language: "Lingua:",
                Start: "Avvia", Stop: "Ferma"),
            "pt" => (
                Subtitle: "Motor háptico universal para simuladores de voo", Audio: "SAÍDA DE ÁUDIO",
                Input: "DISPOSITIVO DE ENTRADA", RefreshAudio: "Atualizar dispositivos de áudio",
                RefreshInput: "Atualizar dispositivos de entrada", TestDevice: "Testar",
                Effect: "Efeito", Binding: "Atribuição", Intensity: "Intensidade",
                Test: "Testar", Status: "Estado", LastInput: "Última entrada:",
                Folder: "Abrir pasta de configuração", Language: "Idioma:",
                Start: "Iniciar", Stop: "Parar"),
            "pl" => (
                Subtitle: "Uniwersalny silnik haptyczny do symulatorów lotu", Audio: "WYJŚCIE AUDIO",
                Input: "URZĄDZENIE WEJŚCIOWE", RefreshAudio: "Odśwież urządzenia audio",
                RefreshInput: "Odśwież urządzenia wejściowe", TestDevice: "Testuj",
                Effect: "Efekt", Binding: "Przypisanie", Intensity: "Intensywność",
                Test: "Test", Status: "Stan", LastInput: "Ostatnie wejście:",
                Folder: "Otwórz folder konfiguracji", Language: "Język:",
                Start: "Uruchom", Stop: "Zatrzymaj"),
            "zh" => (
                Subtitle: "飞行模拟器通用触觉引擎", Audio: "音频输出",
                Input: "输入设备", RefreshAudio: "刷新音频设备",
                RefreshInput: "刷新输入设备", TestDevice: "测试",
                Effect: "效果", Binding: "绑定", Intensity: "强度",
                Test: "测试", Status: "状态", LastInput: "最近输入：",
                Folder: "打开配置文件夹", Language: "语言：",
                Start: "启动", Stop: "停止"),
            "ja" => (
                Subtitle: "フライトシミュレーター用ユニバーサル触覚エンジン", Audio: "オーディオ出力",
                Input: "入力デバイス", RefreshAudio: "オーディオデバイスを更新",
                RefreshInput: "入力デバイスを更新", TestDevice: "テスト",
                Effect: "効果", Binding: "割り当て", Intensity: "強度",
                Test: "テスト", Status: "状態", LastInput: "最新の入力：",
                Folder: "設定フォルダーを開く", Language: "言語：",
                Start: "開始", Stop: "停止"),
            "ko" => (
                Subtitle: "비행 시뮬레이터용 범용 햅틱 엔진", Audio: "오디오 출력",
                Input: "입력 장치", RefreshAudio: "오디오 장치 새로 고침",
                RefreshInput: "입력 장치 새로 고침", TestDevice: "테스트",
                Effect: "효과", Binding: "할당", Intensity: "강도",
                Test: "테스트", Status: "상태", LastInput: "최근 입력:",
                Folder: "설정 폴더 열기", Language: "언어:",
                Start: "시작", Stop: "중지"),
            _ => (
                Subtitle: "Motor háptico universal para simuladores de vuelo", Audio: "SALIDA DE AUDIO",
                Input: "DISPOSITIVO DE ENTRADA", RefreshAudio: "Refrescar dispositivos de audio",
                RefreshInput: "Refrescar dispositivos de entrada", TestDevice: "Probar",
                Effect: "Efecto", Binding: "Asignación", Intensity: "Intensidad",
                Test: "Prueba", Status: "Estado", LastInput: "Última entrada:",
                Folder: "Abrir carpeta de configuración", Language: "Idioma:",
                Start: "Iniciar", Stop: "Detener")
        };

        SubtitleText.Text = text.Subtitle;
        AudioSectionLabel.Text = text.Audio;
        InputSectionLabel.Text = text.Input;
        RefreshAudioBtn.ToolTip = text.RefreshAudio;
        RefreshDevicesBtn.ToolTip = text.RefreshInput;
        TestDeviceBtn.Content = text.TestDevice;
        EffectColumn.Header = text.Effect;
        BindingColumn.Header = text.Binding;
        IntensityColumn.Header = text.Intensity;
        TestColumn.Header = text.Test;
        StatusColumn.Header = text.Status;
        LastInputCaption.Text = text.LastInput;
        OpenConfigBtn.Content = text.Folder;
        LanguageLabel.Text = text.Language;
        Resources["TestButtonText"] = text.Test;
        EditBindingMenuItem.Header = UiText.Get("Edit");
        UnbindBindingMenuItem.Header = UiText.Get("Unbind");
        foreach (var row in _effectRows)
            row.RefreshLanguage();
        if (_profile.Bindings.Count > 0)
            ApplyBindingsToEffects();
        StartStopBtn.Content = _isRunning ? text.Stop : text.Start;
    }

    private void AudioDevices_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e
    )
    {
        if (_isRestoringSelections) return;
        SaveSelectedAudioDevice();
        UpdateAudioDeviceStatus();
    }

    private void BindingsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedEffectRow == null) return;

        if (BindingsGrid.SelectedItem == null) return;

        EditSelectedEffect();
    }

    private void InitializeEffects()
    {
        // TODO: Later on make this dynamic via reflection or config file
        _effectRows.Clear();

        var effects = new[]
        {
            new EffectRow(RumbleEffectType.ThrottleAxis, "Throttle"),
            new EffectRow(RumbleEffectType.PitchAndRoll, "Pitch & Roll"),
            new EffectRow(RumbleEffectType.Gun, "Gun Fire"),
            new EffectRow(RumbleEffectType.Missile, "Missile"),
            new EffectRow(RumbleEffectType.HighGTurn, "High-G Turn"),
            new EffectRow(RumbleEffectType.MuteEffects, "Mute Effects")
        };

        foreach (var effect in effects)
        {
            effect.IntensityChanged += OnEffectIntensityChanged;
            _effectRows.Add(effect);
        }
    }

    private void OnEffectIntensityChanged(EffectRow row, float intensity)
    {
        if (!row.SupportsIntensity || !row.IsBound)
            return;

        var bindings = _profile.Bindings
            .Where(binding => binding.Effect == row.Effect)
            .ToList();

        if (bindings.Count == 0)
            return;

        foreach (var binding in bindings)
            binding.Intensity = intensity;

        _store.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);
        BuildBindingsFromProfile();
    }

    private void InitializeActionButtons()
    {
    }

    private void UpdateEffectActionButtons()
    {
    }

    private void RefreshDevices()
    {
        var audio = _audioSvc.GetRenderDevices();
        AudioDevices.ItemsSource = audio;
        AudioDevices.DisplayMemberPath = nameof(AudioDeviceOption.FriendlyName);

        var joys = _inputSvc.ListJoysticks();
        var previousInputGuid = (InputDevices.SelectedItem as DeviceOption)?.Guid;

        _deviceNamesByGuid.Clear();
        foreach (var d in joys)
        {
            var name = NormalizeDeviceName(d.InstanceName, d.InstanceGuid);
            _deviceNamesByGuid[d.InstanceGuid] = name;
        }

        var inputOptions = joys
            .Select(device => new DeviceOption
            {
                Guid = device.InstanceGuid,
                Name = NormalizeDeviceName(device.InstanceName, device.InstanceGuid)
            })
            .OrderBy(option => option.Name)
            .ToList();
        InputDevices.ItemsSource = inputOptions;
        var preferredGuid = previousInputGuid ?? _selectedInputDeviceGuid;
        InputDevices.SelectedItem = preferredGuid is Guid previous
            ? inputOptions.FirstOrDefault(option => option.Guid == previous)
            : inputOptions.FirstOrDefault();
    }

    private void RefreshDevicesFromUi()
    {
        var selectedAudioId = (AudioDevices.SelectedItem as AudioDeviceOption)?.Id;
        RefreshDevices();

        if (selectedAudioId != null &&
            AudioDevices.ItemsSource is IEnumerable<AudioDeviceOption> audioDevices)
        {
            AudioDevices.SelectedItem =
                audioDevices.FirstOrDefault(device => device.Id == selectedAudioId);
        }

        UpdateAudioDeviceStatus();
        ReconnectInputDevicesIfNeeded();
    }

    private void RefreshAudioDevicesFromUi()
    {
        var selectedAudioId = (AudioDevices.SelectedItem as AudioDeviceOption)?.Id;
        var audioDevices = _audioSvc.GetRenderDevices();
        AudioDevices.ItemsSource = audioDevices;
        AudioDevices.DisplayMemberPath = nameof(AudioDeviceOption.FriendlyName);
        AudioDevices.SelectedItem = selectedAudioId == null
            ? audioDevices.FirstOrDefault()
            : audioDevices.FirstOrDefault(device => device.Id == selectedAudioId)
              ?? audioDevices.FirstOrDefault();
        UpdateAudioDeviceStatus();
    }

    private void StartConnectionMonitor()
    {
        _connectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _connectionTimer.Tick += (_, _) =>
        {
            UpdateAudioDeviceStatus();
            ReconnectInputDevicesIfNeeded();
        };
        _connectionTimer.Start();
        UpdateAudioDeviceStatus();
    }

    private void UpdateAudioDeviceStatus()
    {
        bool connected;
        try
        {
            connected = AudioDevices.SelectedItem is AudioDeviceOption selected &&
                _audioSvc.GetRenderDevices().Any(device => device.Id == selected.Id);
        }
        catch
        {
            connected = false;
        }

        AudioStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
            connected
                ? System.Windows.Media.Color.FromRgb(62, 181, 91)
                : System.Windows.Media.Color.FromRgb(190, 105, 105));
        AudioStatusIndicator.Stroke = new System.Windows.Media.SolidColorBrush(
            connected
                ? System.Windows.Media.Color.FromRgb(37, 112, 55)
                : System.Windows.Media.Color.FromRgb(112, 64, 64));
        AudioStatusIndicator.ToolTip = connected
            ? "Audio device connected"
            : "Audio device disconnected";
    }

    private void ReconnectInputDevicesIfNeeded()
    {
        if (_poller == null) return;

        try
        {
            var attached = _inputSvc.ListJoysticks();
            var attachedGuids = attached.Select(device => device.InstanceGuid).ToHashSet();
            var polledGuids = _deviceNamesByGuid.Keys.ToHashSet();
            if (attachedGuids.SetEquals(polledGuids)) return;

            _poller.StateReceived -= OnState;
            _poller.Dispose();
            _poller = null;
            try { _keyboard?.Unacquire(); } catch { }
            _keyboard?.Dispose();
            _keyboard = null;

            lock (_sync)
            {
                _mixer.StopAll();
                _latestStateByDevice.Clear();
                _prevButtonsByDevice.Clear();
                _activeHolds.Clear();
                _activeMuteHolds.Clear();
                _activeHighGHolds.Clear();
                _highGThresholdStates.Clear();
                _isMuted = _muteToggleActive;
            }

            StartPollingDevices();
            RefreshInputDeviceOptions(attached);
            LastInputLabel.Text = attached.Count == 0
                ? "Input device disconnected - waiting for reconnection..."
                : "Input devices reconnected";
            AppLog.Info(LastInputLabel.Text);
        }
        catch (Exception ex)
        {
            AppLog.Error("Input reconnection check failed.", ex);
            LastInputLabel.Text = "Reconnection check failed: " + ex.Message;
        }
    }

    private void RefreshInputDeviceOptions(IReadOnlyList<DeviceInstance> devices)
    {
        var options = devices
            .Select(device => new DeviceOption
            {
                Guid = device.InstanceGuid,
                Name = NormalizeDeviceName(device.InstanceName, device.InstanceGuid)
            })
            .OrderBy(option => option.Name)
            .ToList();

        InputDevices.ItemsSource = options;
        InputDevices.SelectedItem = _selectedInputDeviceGuid is Guid selected
            ? options.FirstOrDefault(option => option.Guid == selected)
            : options.FirstOrDefault();
    }

    private void LoadActiveProfileAndApply()
    {
        _profile = LoadActiveProfile();
        ApplyBindingsToEffects();
        ApplySavedSelections();
    }

    private void ApplySavedSelections()
    {
        _isRestoringSelections = true;
        try
        {
            var audio = (IEnumerable<AudioDeviceOption>)AudioDevices.ItemsSource;

            var selected = _profile.AppConfig.SelectedAudioDeviceId is string aid
                ? audio.FirstOrDefault(x => x.Id == aid)
                : null;

            AudioDevices.SelectedItem = selected ?? audio.FirstOrDefault();
        }
        finally
        {
            _isRestoringSelections = false;
        }
    }


    private void ApplyBindingsToEffects()
    {
        foreach (var row in _effectRows)
        {
            var allowedKinds = EffectBindingRules.GetAllowedKinds(row.Effect);
            var bindings = _profile.Bindings
                .Where(b => b.Effect == row.Effect)
                .Where(b => b.DeviceGuid != null)
                .Where(b => allowedKinds.Contains(b.Kind))
                .ToList();

            if (bindings.Count == 0)
            {
                row.SetUnbound();
                continue;
            }

            var kinds = bindings
                .Select(b => b.Kind)
                .Distinct()
                .OrderBy(k => k.ToString())
                .ToList();

            if (kinds.Count == 1)
            {
                var binding = bindings.First();
                var axisName = string.IsNullOrWhiteSpace(binding.AxisName)
                    ? "Axis"
                    : binding.AxisName.Trim();

                string bindingText = binding.Kind switch
                {
                    BindingKind.Axis =>
                        $"{BindingDeviceName(binding)} | {axisName}",

                    BindingKind.Button when binding.DeviceGuid == InputDeviceIds.Keyboard =>
                        $"Keyboard | {GetKeyboardKeyName(binding.ButtonIndex)} ({GetTriggerLabel(binding.Trigger)})",

                    BindingKind.Button when binding.Effect == RumbleEffectType.HighGTurn =>
                        $"{BindingDeviceName(binding)} | {UiText.Get("Button").TrimEnd(':')} {binding.ButtonIndex} | {UiText.Get("Threshold").TrimEnd(':')} {(binding.ActivationThreshold ?? _profile.HighGSettings.DefaultActivationThreshold):0.00}",

                    BindingKind.Button =>
                        $"{BindingDeviceName(binding)} | {UiText.Get("Button").TrimEnd(':')} {binding.ButtonIndex} ({GetTriggerLabel(binding.Trigger)})",

                    _ => "Unknown"
                };

                row.SetBound(bindingText, binding.Intensity, binding.Enabled);
            }
            else
            {
                var kindLabel = string.Join(" + ", kinds);
                row.SetBound(
                    $"Multiple ({kindLabel})",
                    0f,
                    bindings.Any(binding => binding.Enabled));
            }
        }

        UpdateEffectStatuses();
    }

    private string DeviceName(Guid guid)
        => guid == InputDeviceIds.Keyboard
            ? "Keyboard"
            : _deviceNamesByGuid.TryGetValue(guid, out var n)
            ? NormalizeDeviceName(n, guid)
            : guid.ToString();

    private string BindingDeviceName(BindingConfig binding)
    {
        if (binding.DeviceGuid == InputDeviceIds.Keyboard)
            return "Keyboard";
        if (binding.DeviceGuid is Guid guid &&
            _deviceNamesByGuid.TryGetValue(guid, out var connectedName) &&
            !string.IsNullOrWhiteSpace(connectedName))
            return connectedName.Trim();
        if (!string.IsNullOrWhiteSpace(binding.DeviceName) &&
            !Guid.TryParse(binding.DeviceName, out _))
            return binding.DeviceName.Trim();
        return UiText.Get("Device").TrimEnd(':');
    }

    private static string GetKeyboardKeyName(int? keyCode)
        => keyCode is int value &&
           System.Enum.IsDefined(typeof(SharpDX.DirectInput.Key), value)
            ? ((SharpDX.DirectInput.Key)value).ToString()
            : "Unknown key";

    private static string NormalizeDeviceName(string? name, Guid guid)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        return string.IsNullOrEmpty(trimmed) ? guid.ToString() : trimmed;
    }

    private static string GetTriggerLabel(TriggerType trigger)
        => UiText.Get(trigger == TriggerType.Press ? "Press" : "Hold");

    private ActiveProfile LoadActiveProfile()
    {
        var appConfig = _store.LoadAppConfig();
        appConfig.Version = BuildInfo.DisplayVersion;

        var throttleSettings = _store.LoadProfile<ThrottleSettings>(appConfig.ThrottleProfilePath);
        var gunSettings = _store.LoadProfile<GunHoldSettings>(appConfig.GunProfilePath);
        var missileSettings = _store.LoadProfile<MissileSettings>(appConfig.MissileProfilePath);
        var highGSettings = _store.LoadProfile<HighGSettings>(appConfig.HighGProfilePath);
        var bindings = _store.LoadBindings(appConfig.BindingsPath);
        bool bindingsMigrated = false;

        foreach (var binding in bindings.Where(binding =>
                     binding.Effect == RumbleEffectType.ThrottleAxis &&
                     binding.DeviceGuid.HasValue &&
                     binding.AxisName is BindingUiHelper.CombinedThrottleAxis))
        {
            string deviceName = DeviceName(binding.DeviceGuid!.Value);
            bool isX56Throttle =
                (deviceName.Contains("X56", StringComparison.OrdinalIgnoreCase) ||
                 deviceName.Contains("X-56", StringComparison.OrdinalIgnoreCase)) &&
                deviceName.Contains("Throttle", StringComparison.OrdinalIgnoreCase);

            if (!isX56Throttle) continue;
            binding.AxisName = BindingUiHelper.CombinedSlidersAxis;
            bindingsMigrated = true;
        }

        if (bindingsMigrated)
            _store.SaveBindings(appConfig.BindingsPath, bindings);

        return new ActiveProfile
        {
            AppConfig = appConfig,
            ThrottleSettings = throttleSettings,
            GunSettings = gunSettings,
            MissileSettings = missileSettings,
            HighGSettings = highGSettings,
            Bindings = bindings
        };
    }
    private void UpdateStartStopUI()
    {
        ApplyLanguage(_language);
        var color = _isRunning
            ? System.Windows.Media.Color.FromRgb(217, 75, 75)
            : System.Windows.Media.Color.FromRgb(46, 173, 99);
        StartStopBtn.Background = new System.Windows.Media.SolidColorBrush(color);
    }

    private void OpenConfigFolder()
    {
        if (_store == null) return;

        var folder = _store.RootDir;
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = folder,
            UseShellExecute = true
        });
    }

    private void OpenDeviceTestWindow()
    {
        Guid? deviceGuid = (InputDevices.SelectedItem as DeviceOption)?.Guid;

        var attached = _inputSvc.ListJoysticks();

        if (deviceGuid == null)
        {
            MessageBox.Show("Select an input device first.", "Test Device");
            return;
        }

        var device = attached.FirstOrDefault(item => item.InstanceGuid == deviceGuid.Value);
        if (device == null)
        {
            MessageBox.Show("The selected input device is not connected.", "Test Device");
            return;
        }

        StartPollingDevices();
        var axes = _axisNamesByGuid.TryGetValue(deviceGuid.Value, out var detectedAxes)
            ? detectedAxes
            : Array.Empty<string>();
        var buttons = _buttonIndicesByGuid.TryGetValue(deviceGuid.Value, out var detectedButtons)
            ? detectedButtons
            : Array.Empty<int>();
        var povs = _povIndicesByGuid.TryGetValue(deviceGuid.Value, out var detectedPovs)
            ? detectedPovs
            : Array.Empty<int>();
        var name = NormalizeDeviceName(device.InstanceName, device.InstanceGuid);
        var window = new DeviceTestWindow(
            deviceGuid.Value, name, axes, buttons, povs) { Owner = this };

        JoystickStateReceived += window.ReceiveState;
        _deviceTestWindows.Add(window);
        window.Closed += (_, _) =>
        {
            JoystickStateReceived -= window.ReceiveState;
            _deviceTestWindows.Remove(window);
            if (!_isRunning && _deviceTestWindows.Count == 0)
                StopPolling(force: true);
        };
        window.Show();
    }

    private void ToggleStartStop()
    {
        if (_isRunning)
            Stop();
        else
            Start();
    }

    private void Start()
    {
        Stop();

        if (!TryGetSelectedAudioDevice(out var audioDev))
            return;

        try
        {
            _profile = LoadActiveProfile();
            ApplyBindingsToEffects();

            // An empty binding list is valid: start silent and let the user
            // configure effects without requiring a connected controller.
            SetupEngine(audioDev);
            StartPollingDevices();
            BuildBindingsFromProfile();
            SetEffectsRunning(true);
            StartEngineTimer();

            LastInputLabel.Text = "None";
            _isRunning = true;
            UpdateStartStopUI();
            AppLog.Info("Effects started.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to start effects.", ex);
            Stop();
            MessageBox.Show(
                $"Unable to start JFlightShaker.\n\n{ex.Message}",
                "Start error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool TryGetSelectedAudioDevice(out MMDevice audioDevice)
    {
        audioDevice = null!;

        if (AudioDevices.SelectedItem is not AudioDeviceOption audioOption)
        {
            MessageBox.Show("Select an audio device.");
            return false;
        }

        var device = _audioSvc.GetRenderDeviceById(audioOption.Id);
        if (device == null)
        {
            MessageBox.Show("Selected audio device is unavailable.");
            return false;
        }

        audioDevice = device;

        return true;
    }

    private void SetupEngine(MMDevice audioDev)
    {
        _eval = new BindingEvaluator(_profile.ThrottleSettings);

        _engine = new RumbleEngine();
        _engine.Start(audioDev, _profile.ThrottleSettings);
        _engine.Enabled = true;
    }

    private void StartPollingDevices()
    {
        if (_poller != null) return;

        _poller = new MultiJoystickPoller();
        _poller.PollError += ex =>
        {
            AppLog.Error("DirectInput polling error.", ex);
            Dispatcher.Invoke(() => LastInputLabel.Text = "Poll error: " + ex.Message);
        };
        _poller.StateReceived += OnState;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var joys = _inputSvc.ListJoysticks();

        _deviceNamesByGuid.Clear();
        _axisNamesByGuid.Clear();
        _buttonIndicesByGuid.Clear();
        _povIndicesByGuid.Clear();

        foreach (var dev in joys)
        {
            _deviceNamesByGuid[dev.InstanceGuid] = NormalizeDeviceName(dev.InstanceName, dev.InstanceGuid);

            try
            {
                var js = _inputSvc.Open(dev, hwnd);
                _axisNamesByGuid[dev.InstanceGuid] = BindingUiHelper.GetDeviceAxes(js);
                _buttonIndicesByGuid[dev.InstanceGuid] = BindingUiHelper.GetDeviceButtons(js);
                _povIndicesByGuid[dev.InstanceGuid] = BindingUiHelper.GetDevicePovs(js);
                _poller.Add(dev.InstanceGuid, js);
            }
            catch
            {
                // Keep testing/flight input alive for every device that can open.
            }
        }

        _poller.Start(5);

        try
        {
            _keyboard = _inputSvc.OpenKeyboard(hwnd);
            _previousKeyboardKeys.Clear();
        }
        catch
        {
            _keyboard = null;
        }
    }

    private void BuildBindingsFromProfile()
    {
        lock (_sync)
        {
            _bindings.Clear();
            _prevButtonsByDevice.Clear();
            _latestStateByDevice.Clear();

            foreach (var config in _profile.Bindings)
            {
                if (config.DeviceGuid == null) continue;
                if (!EffectBindingRules.IsKindAllowed(config.Effect, config.Kind)) continue;

                _bindings.Add(new BindingDefinition
                {
                    DeviceGuid = config.DeviceGuid.Value,
                    DeviceName = DeviceName(config.DeviceGuid.Value),
                    Kind = config.Kind,
                    AxisName = config.AxisName,
                    AxisMin = config.AxisMin,
                    AxisMax = config.AxisMax,
                    InvertAxis = config.InvertAxis,
                    ButtonIndex = config.ButtonIndex,
                    ActivationThreshold = config.ActivationThreshold,
                    Effect = config.Effect,
                    Intensity = config.Intensity,
                    Enabled = config.Enabled,
                    Trigger = config.Trigger
                });
            }
        }
    }

    private List<BindingConfig> CreateDefaultBindingConfigs(
        DeviceInstance throttleDevice,
        DeviceInstance stickDevice
    )
    {
        var detectedAxes = BindingUiHelper.GetDeviceAxes(
            guid => guid == throttleDevice.InstanceGuid
                ? TryOpenJoystick(guid)
                : null,
            throttleDevice.InstanceGuid);

        // The X56 exposes its linked throttle levers through the two standard
        // slider slots. Keep Z/Rz available as individual axes/rotaries.
        var axisName = detectedAxes.Contains(BindingUiHelper.CombinedSlidersAxis)
            ? BindingUiHelper.CombinedSlidersAxis
            : detectedAxes.Contains(BindingUiHelper.CombinedThrottleAxis)
                ? BindingUiHelper.CombinedThrottleAxis
            : detectedAxes.Contains("Slider0")
                ? "Slider0"
                : _profile.ThrottleSettings.DefaultAxisName;
        var gunButton = _profile.GunSettings.DefaultButtonIndex;

        return new List<BindingConfig>
    {
        new BindingConfig
        {
            DeviceName = "Throttle Axis",
            Kind = BindingKind.Axis,
            DeviceGuid = throttleDevice.InstanceGuid,
            AxisName = axisName,
            Effect = RumbleEffectType.ThrottleAxis,
            Intensity = 1.0f,
            Trigger = TriggerType.Hold
        },
        new BindingConfig
        {
            DeviceName = "Gun Fire",
            Kind = BindingKind.Button,
            DeviceGuid = stickDevice.InstanceGuid,
            ButtonIndex = gunButton,
            Effect = RumbleEffectType.Gun,
            Intensity = 0.5f,
            Trigger = TriggerType.Hold
        }
    };
    }


    private void SetEffectsRunning(bool isRunning)
    {
        UpdateEffectStatuses(isRunning);
    }

    private void UpdateEffectStatuses(bool? runningOverride = null)
    {
        var running = runningOverride ?? _isRunning;
        var muted = _isMuted;

        foreach (var row in _effectRows)
        {
            if (row.IsBound && !row.IsEffectEnabled)
            {
                row.SetStatus("Disabled");
                continue;
            }

            if (!running)
            {
                row.SetStatus("Stopped");
                continue;
            }

            if (row.Effect == RumbleEffectType.MuteEffects)
            {
                row.SetStatus(muted ? "On" : "Off");
                continue;
            }

            if (muted)
            {
                row.SetStatus("Muted");
                continue;
            }

            row.SetStatus("Running");
        }
    }

    private void UpdateEffectActivity(
        bool throttleActive,
        bool pitchRollActive,
        bool gunActive,
        bool missileActive,
        bool highGActive)
    {
        foreach (var row in _effectRows)
        {
            bool previewing = _previewEffect == row.Effect;
            if (previewing)
            {
                row.SetStatus("Active");
                continue;
            }

            if (row.IsBound && !row.IsEffectEnabled)
            {
                row.SetStatus("Disabled");
                continue;
            }

            bool active = row.Effect switch
            {
                RumbleEffectType.ThrottleAxis => throttleActive,
                RumbleEffectType.PitchAndRoll => pitchRollActive,
                RumbleEffectType.Gun => gunActive,
                RumbleEffectType.Missile => missileActive,
                RumbleEffectType.HighGTurn => highGActive,
                RumbleEffectType.MuteEffects => _isMuted,
                _ => false
            };

            if (active)
                row.SetStatus("Active");
            else if (row.Effect == RumbleEffectType.MuteEffects)
                row.SetStatus(_isRunning ? "Off" : "Stopped");
            else if (_isMuted && _isRunning)
                row.SetStatus("Muted");
            else
                row.SetStatus(_isRunning ? "Running" : "Stopped");
        }
    }

    private void TestEffectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: EffectRow row } || !row.CanTest)
            return;

        try
        {
            if (_engine == null)
            {
                if (!TryGetSelectedAudioDevice(out var audioDevice))
                    return;

                _profile = LoadActiveProfile();
                SetupEngine(audioDevice);
                _previewOwnsEngine = true;
                StartEngineTimer();
            }

            var now = Environment.TickCount64;
            _previewEffect = row.Effect;
            _previewStartedAtMs = now;
            _previewUntilMs = now + 1500;
            UpdateEffectActivity(false, false, false, false, false);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to preview effect.", ex);
            EndEffectPreview();
            MessageBox.Show(
                $"Unable to preview this effect.\n\n{ex.Message}",
                "Effect test",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private (float left, float right) CalculatePreviewAmplitude(long now)
    {
        if (_previewEffect == null || now >= _previewUntilMs)
            return (0f, 0f);

        var row = _effectRows.First(effect => effect.Effect == _previewEffect);
        float intensity = row.Intensity;
        float elapsed = (now - _previewStartedAtMs) / 1000f;

        return _previewEffect switch
        {
            RumbleEffectType.ThrottleAxis => (0.65f * intensity, 0.65f * intensity),
            RumbleEffectType.PitchAndRoll =>
                ((0.25f + (0.45f * MathF.Max(0f, MathF.Sin(elapsed * 5f)))) * intensity,
                 (0.25f + (0.45f * MathF.Max(0f, -MathF.Sin(elapsed * 5f)))) * intensity),
            RumbleEffectType.Gun =>
                ((0.65f + (0.35f * (MathF.Sin(elapsed * 115f) >= 0f ? 1f : 0f))) * intensity,
                 (0.65f + (0.35f * (MathF.Sin(elapsed * 115f) >= 0f ? 1f : 0f))) * intensity),
            RumbleEffectType.Missile =>
                (Math.Clamp((1f - (elapsed / 1.5f)) * 1.15f, 0f, 1f) * intensity,
                 Math.Clamp((1f - (elapsed / 1.5f)) * 1.15f, 0f, 1f) * intensity),
            RumbleEffectType.HighGTurn => (0.8f * intensity, 0.8f * intensity),
            _ => (0f, 0f)
        };
    }

    private void EndEffectPreview()
    {
        _previewEffect = null;
        _previewStartedAtMs = 0;
        _previewUntilMs = 0;

        if (_previewOwnsEngine)
        {
            _previewOwnsEngine = false;
            StopEngineTimer();
            DisposeEngine();
        }

        UpdateEffectStatuses();
    }

    private void StartEngineTimer()
    {
        _engineTimer?.Stop();
        _engineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        _engineTimer.Tick += (_, _) => EngineTick();
        _engineTimer.Start();

        _lastTick = Environment.TickCount64;
    }

    private void OnState(Guid deviceGuid, JoystickState state)
    {
        lock (_sync)
        {
            _latestStateByDevice[deviceGuid] = state;
            HandleTransitionsAndLogger(deviceGuid, state);
        }

        JoystickStateReceived?.Invoke(deviceGuid, state);

        Dispatcher.BeginInvoke(() =>
        {
            LastInputLabel.Text = _logger.LastText;
        });
    }

    private void HandleTransitionsAndLogger(Guid deviceGuid, JoystickState state)
    {
        var buttons = state.Buttons ?? Array.Empty<bool>();

        if (!_prevButtonsByDevice.TryGetValue(deviceGuid, out var prev) || prev.Length != buttons.Length)
        {
            prev = new bool[buttons.Length];
            _prevButtonsByDevice[deviceGuid] = prev;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            bool isDown = buttons[i];
            bool wasDown = prev[i];

            if (isDown && !wasDown)
            {
                _logger.LogButton(deviceGuid, i, true);
                ButtonPressedEdge?.Invoke(deviceGuid, i);
                OnButtonPressed(deviceGuid, i);
            }
            else if (!isDown && wasDown)
            {
                _logger.LogButton(deviceGuid, i, false);
                OnButtonReleased(deviceGuid, i);
            }
        }

        Array.Copy(buttons, prev, buttons.Length);
    }

    private void OnButtonPressed(
        Guid deviceGuid,
        int buttonIndex
    )
    {
        List<BindingDefinition> matches;
        bool muted;

        lock (_sync)
        {
            matches = _bindings.Where(x =>
                x.Kind == BindingKind.Button &&
                x.Enabled &&
                x.DeviceGuid == deviceGuid &&
                x.ButtonIndex == buttonIndex &&
                (x.Effect == RumbleEffectType.Gun ||
                 x.Effect == RumbleEffectType.Missile ||
                 x.Effect == RumbleEffectType.HighGTurn ||
                 x.Effect == RumbleEffectType.MuteEffects)
            ).ToList();

            foreach (var b in matches)
            {
                var key = (deviceGuid, buttonIndex);
                if (b.Effect == RumbleEffectType.Gun)
                {
                    if (_activeHolds.ContainsKey(key)) continue;

                    var fx = new GunHoldEffect(b.Intensity, _profile.GunSettings);
                    _activeHolds[key] = fx;
                    _mixer.Add(fx);
                }
                else if (b.Effect == RumbleEffectType.Missile)
                {
                    // Missile is a one-shot effect: every button press starts
                    // one complete launch envelope, independent of release.
                    _mixer.Add(new MissileEffect(b.Intensity, _profile.MissileSettings));
                    _missileActiveUntilMs = Math.Max(
                        _missileActiveUntilMs,
                        Environment.TickCount64 +
                        (long)(Math.Max(0.05f, _profile.MissileSettings.DurationSeconds) * 1000f));
                }
                else if (b.Effect == RumbleEffectType.HighGTurn)
                {
                    _activeHighGHolds.Add(key);
                }
                else if (b.Effect == RumbleEffectType.MuteEffects)
                {
                    if (b.Trigger == TriggerType.Press)
                    {
                        _muteToggleActive = !_muteToggleActive;
                    }
                    else
                    {
                        _activeMuteHolds.Add(key);
                    }
                }
            }

            muted = _muteToggleActive || _activeMuteHolds.Count > 0;
        }

        UpdateMuteState(muted);
    }
    private void OnButtonReleased(Guid deviceGuid, int buttonIndex)
    {
        bool muted;

        lock (_sync)
        {
            var key = (deviceGuid, buttonIndex);

            if (_activeHolds.TryGetValue(key, out var fx))
            {
                fx.Stop();
                _activeHolds.Remove(key);
            }

            if (_activeMuteHolds.Contains(key))
                _activeMuteHolds.Remove(key);
            _activeHighGHolds.Remove(key);
            _highGThresholdStates.Remove(key);

            muted = _muteToggleActive || _activeMuteHolds.Count > 0;
        }

        UpdateMuteState(muted);
    }

    private void UpdateMuteState(bool muted)
    {
        if (_isMuted == muted) return;
        _isMuted = muted;
        Dispatcher.BeginInvoke(() => UpdateEffectStatuses());
    }

    private float CalculateHighGAmplitude()
    {
        float total = 0f;
        var pitchBinding = _bindings.FirstOrDefault(binding =>
            binding.Effect == RumbleEffectType.PitchAndRoll &&
            binding.Kind == BindingKind.Axis);

        foreach (var binding in _bindings.Where(binding =>
                     binding.Effect == RumbleEffectType.HighGTurn &&
                     binding.Enabled &&
                     binding.Kind == BindingKind.Button &&
                     binding.ButtonIndex.HasValue))
        {
            var key = (binding.DeviceGuid, binding.ButtonIndex!.Value);
            if (!_activeHighGHolds.Contains(key))
                continue;

            Guid stickGuid = pitchBinding?.DeviceGuid ?? binding.DeviceGuid;
            if (!_latestStateByDevice.TryGetValue(stickGuid, out var state))
                continue;

            // A High-G pull/push is driven by pitch. Banking by itself must
            // not activate the effect.
            float inclination = EffectMath.CenteredAxisMagnitude(state.Y);
            float threshold = Math.Clamp(
                binding.ActivationThreshold ??
                _profile.HighGSettings.DefaultActivationThreshold,
                0f, 0.95f);
            float hysteresis = Math.Clamp(_profile.HighGSettings.Hysteresis, 0f, 0.2f);

            _highGThresholdStates.TryGetValue(key, out bool thresholdActive);
            thresholdActive = EffectMath.UpdateThresholdState(
                inclination, threshold, hysteresis, thresholdActive);
            _highGThresholdStates[key] = thresholdActive;

            if (!thresholdActive)
                continue;

            total += EffectMath.ScaleAboveThreshold(
                inclination, threshold, binding.Intensity);
        }

        return Math.Clamp(total, 0f, 1f);
    }

    private void PollKeyboard()
    {
        if (_keyboard == null) return;

        try
        {
            _keyboard.Poll();
            var pressed = _keyboard.GetCurrentState().PressedKeys.ToHashSet();

            foreach (var key in pressed.Except(_previousKeyboardKeys).ToList())
                OnButtonPressed(InputDeviceIds.Keyboard, (int)key);

            foreach (var key in _previousKeyboardKeys.Except(pressed).ToList())
                OnButtonReleased(InputDeviceIds.Keyboard, (int)key);

            _previousKeyboardKeys.Clear();
            _previousKeyboardKeys.UnionWith(pressed);
        }
        catch
        {
            try { _keyboard.Acquire(); } catch { }
        }
    }

    private void EngineTick()
    {
        if (_engine == null || _eval == null) return;
        PollKeyboard();

        var now = Environment.TickCount64;
        if (_previewEffect != null && now >= _previewUntilMs)
        {
            EndEffectPreview();
            return;
        }

        var dt = (now - _lastTick) / 1000f;
        if (dt <= 0f) dt = 0.005f;
        if (dt > 0.1f) dt = 0.1f;
        _lastTick = now;

        float throttle = 0f;
        float throttleSpeedProxy = 0f;
        bool hasThrottleInput = false;
        float movementLeft = 0f;
        float movementRight = 0f;
        bool enabled = true;
        float effects;
        float highGAmplitude;
        bool gunActive;
        bool muted;

        lock (_sync)
        {
            foreach (var b in _bindings.Where(x => x.Kind == BindingKind.Axis))
            {
                if (b.AxisName == null) continue;
                if (!_latestStateByDevice.TryGetValue(b.DeviceGuid, out var s)) continue;

                if (b.Effect == RumbleEffectType.ThrottleAxis)
                {
                    // Keep reading the configured throttle as a speed proxy
                    // even when its own engine-rumble row is disabled.
                    var (amp, en) = _eval.Evaluate(b.DeviceGuid, s, b);
                    throttleSpeedProxy = Math.Max(throttleSpeedProxy, amp);
                    hasThrottleInput = true;
                    if (b.Enabled)
                        throttle += amp * b.Intensity;
                    enabled = enabled && en;
                }
                else if (!b.Enabled)
                {
                    continue;
                }
                else if (b.Effect == RumbleEffectType.PitchAndRoll)
                {
                    var (left, right) = _eval.EvaluatePitchAndRoll(b.DeviceGuid, s, b);
                    movementLeft += left * b.Intensity;
                    movementRight += right * b.Intensity;
                }
                else
                {
                    var (amp, en) = _eval.Evaluate(b.DeviceGuid, s, b);
                    float weightedAmp = amp * b.Intensity;
                    movementLeft += weightedAmp;
                    movementRight += weightedAmp;
                    enabled = enabled && en;
                }
            }

            highGAmplitude = CalculateHighGAmplitude();
            effects = Math.Clamp(_mixer.Update(dt) + highGAmplitude, 0f, 1f);
            gunActive = _activeHolds.Count > 0;
            muted = _isMuted;
        }

        // Throttle is an arcade approximation of airspeed: retain most of the
        // stick feel at idle, match the previous strength at high cruise, and
        // add only a modest amount at maximum power.
        float speedFactor = EffectMath.PitchRollSpeedFactor(
            throttleSpeedProxy, hasThrottleInput);
        movementLeft *= speedFactor;
        movementRight *= speedFactor;

        float movement = Math.Max(movementLeft, movementRight);

        // Button effects take priority. Stick movement only ducks the engine
        // progressively, and small corrections do not duck it at all.
        float movementDuck = Math.Clamp((movement - 0.12f) / 0.43f, 0f, 1f);
        float normalThrottleGain = 1f - (0.45f * movementDuck);
        float effectDuck = Math.Clamp(effects / 0.30f, 0f, 1f);
        float throttleGain = normalThrottleGain +
            ((0.20f - normalThrottleGain) * effectDuck);
        float leftTotal = Math.Clamp(
            (throttle * throttleGain) + movementLeft + effects, 0f, 1f);
        float rightTotal = Math.Clamp(
            (throttle * throttleGain) + movementRight + effects, 0f, 1f);
        var (previewLeft, previewRight) = CalculatePreviewAmplitude(now);
        leftTotal = Math.Clamp(leftTotal + previewLeft, 0f, 1f);
        rightTotal = Math.Clamp(rightTotal + previewRight, 0f, 1f);
        if (muted)
        {
            leftTotal = 0f;
            rightTotal = 0f;
        }

        _engine.Enabled = enabled;
        _engine.SetTargetAmplitudes(leftTotal, rightTotal);
        UpdateEffectActivity(
            throttle > 0.01f,
            movement > 0.01f,
            gunActive,
            now < _missileActiveUntilMs,
            highGAmplitude > 0.01f);
    }

    private void Stop()
    {
        SetEffectsRunning(false);
        StopEngineTimer();
        StopPolling();
        ClearRuntimeState();
        DisposeEngine();

        LastInputLabel.Text = "None";

        _isRunning = false;
        UpdateStartStopUI();
    }


    private void StopEngineTimer()
    {
        _engineTimer?.Stop();
        _engineTimer = null;
    }

    private void StopPolling(bool force = false)
    {
        if (!force && _deviceTestWindows.Count > 0) return;
        if (_poller == null) return;

        _poller.StateReceived -= OnState;
        _poller.Dispose();
        _poller = null;
        try { _keyboard?.Unacquire(); } catch { }
        _keyboard?.Dispose();
        _keyboard = null;
        _previousKeyboardKeys.Clear();
    }

    private void ClearRuntimeState()
    {
        lock (_sync)
        {
            _mixer.StopAll();
            _activeHolds.Clear();
            _activeMuteHolds.Clear();
            _activeHighGHolds.Clear();
            _highGThresholdStates.Clear();
            _muteToggleActive = false;
            _isMuted = false;
            _missileActiveUntilMs = 0;
            _previewEffect = null;
            _previewStartedAtMs = 0;
            _previewUntilMs = 0;
            _previewOwnsEngine = false;

            _latestStateByDevice.Clear();
            _prevButtonsByDevice.Clear();
            _bindings.Clear();
        }
    }

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
        _eval = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _connectionTimer?.Stop();
        _connectionTimer = null;
        SaveProfile();
        Stop();
        StopPolling(force: true);
        _inputSvc.Dispose();
        base.OnClosed(e);
    }

    private void SaveProfile()
    {
        _store.SaveAppConfig(_profile.AppConfig);
        _store.SaveProfile(_profile.AppConfig.ThrottleProfilePath, _profile.ThrottleSettings);
        _store.SaveProfile(_profile.AppConfig.GunProfilePath, _profile.GunSettings);
        _store.SaveProfile(_profile.AppConfig.MissileProfilePath, _profile.MissileSettings);
        _store.SaveProfile(_profile.AppConfig.HighGProfilePath, _profile.HighGSettings);
        _store.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);
    }

    private void SaveSelectedAudioDevice()
    {
        if (AudioDevices.SelectedItem is AudioDeviceOption audio)
        {
            _profile.AppConfig.SelectedAudioDeviceId = audio.Id;
            _store.SaveAppConfig(_profile.AppConfig);
        }
    }

    private void UnbindSelectedEffects()
    {
        var rows = BindingsGrid.SelectedItems
            .OfType<EffectRow>()
            .ToList();
        if (rows.Count == 0) return;

        var effects = rows.Select(row => row.Effect).ToHashSet();
        var bindings = _profile.Bindings.Where(b => effects.Contains(b.Effect)).ToList();
        if (bindings.Count == 0) return;

        foreach (var binding in bindings)
        {
            // Clear Binding
            binding.DeviceGuid = null;
            binding.AxisName = null;
            binding.ButtonIndex = null;
        }

        _store?.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);

        BuildBindingsFromProfile();
        ApplyBindingsToEffects();
        UpdateEffectActionButtons();
    }

    private void EditBindingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (BindingsGrid.SelectedItems.Count != 1)
        {
            MessageBox.Show(UiText.Get("SelectOne"));
            return;
        }
        EditSelectedEffect();
    }

    private void UnbindMenuItem_Click(object sender, RoutedEventArgs e)
        => UnbindSelectedEffects();

    private void BindingsGrid_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not System.Windows.Controls.DataGridRow)
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);

        if (source is not System.Windows.Controls.DataGridRow row)
            return;

        if (!row.IsSelected)
        {
            BindingsGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
        row.Focus();
    }

    private void BindingsGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not System.Windows.Controls.DataGridRow)
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);

        if (source is not System.Windows.Controls.DataGridRow dataGridRow ||
            dataGridRow.Item is not EffectRow effectRow ||
            !effectRow.IsBound)
            return;

        bool enabled = !effectRow.IsEffectEnabled;
        foreach (var binding in _profile.Bindings.Where(binding =>
                     binding.Effect == effectRow.Effect))
        {
            binding.Enabled = enabled;
        }

        effectRow.SetEffectEnabled(enabled);
        _store.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);
        BuildBindingsFromProfile();
        UpdateEffectStatuses();
        e.Handled = true;
    }

    private void EditSelectedEffect()
    {
        var row = SelectedEffectRow;
        if (row == null) return;

        var allowedKinds = EffectBindingRules.GetAllowedKinds(row.Effect);
        var defaultKind = allowedKinds.First();

        var binding = _profile.Bindings.FirstOrDefault(b => b.Effect == row.Effect);
        if (binding == null)
        {
            binding = new BindingConfig
            {
                Effect = row.Effect,
                Kind = defaultKind,
                Intensity = 1f,
                Trigger = EffectBindingRules.GetAllowedTriggers(row.Effect).First(),
                ActivationThreshold = row.Effect == RumbleEffectType.HighGTurn
                    ? _profile.HighGSettings.DefaultActivationThreshold
                    : null
            };
            _profile.Bindings.Add(binding);
        }
        else if (!allowedKinds.Contains(binding.Kind))
        {
            binding.Kind = defaultKind;
            binding.AxisName = null;
            binding.AxisMin = null;
            binding.AxisMax = null;
            binding.ButtonIndex = null;
        }

        var devices = _deviceNamesByGuid
            .Select(kv => new DeviceOption { Guid = kv.Key, Name = kv.Value })
            .OrderBy(x => x.Name)
            .ToList();

        if (row.Effect == RumbleEffectType.MuteEffects)
        {
            devices.Add(new DeviceOption
            {
                Guid = InputDeviceIds.Keyboard,
                Name = "Keyboard"
            });
        }

        var win = new EditBindingWindow(
            devices,
            TryOpenJoystick,
            binding,
            allowedKinds,
            row.EffectName,
            _profile.ThrottleSettings.DefaultAxisName,
            new HighGSettings().DefaultActivationThreshold
        )
        {
            Owner = this
        };

        var ok = win.ShowDialog() == true;
        if (!ok) return;

        if (win.ResetDefaultsRequested)
            ResetEffectProfileDefaults(row.Effect);

        _store?.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);
        SaveProfile();

        BuildBindingsFromProfile();
        ApplyBindingsToEffects();
        UpdateEffectActionButtons();
    }

    private void ResetEffectProfileDefaults(RumbleEffectType effect)
    {
        if (effect == RumbleEffectType.ThrottleAxis)
        {
            var defaults = new ThrottleSettings();
            _profile.ThrottleSettings.SampleRate = defaults.SampleRate;
            _profile.ThrottleSettings.Channels = defaults.Channels;
            _profile.ThrottleSettings.Deadzone = defaults.Deadzone;
            _profile.ThrottleSettings.BaselineAmp = defaults.BaselineAmp;
            _profile.ThrottleSettings.TopAmp = defaults.TopAmp;
            _profile.ThrottleSettings.AmpSmoothing = defaults.AmpSmoothing;
            _profile.ThrottleSettings.InvertAxis = defaults.InvertAxis;
            _profile.ThrottleSettings.ResponseCurve = defaults.ResponseCurve;
            _profile.ThrottleSettings.DefaultAxisName = defaults.DefaultAxisName;
        }
        else if (effect == RumbleEffectType.Gun)
        {
            var defaults = new GunHoldSettings();
            _profile.GunSettings.PulseHz = defaults.PulseHz;
            _profile.GunSettings.Punch = defaults.Punch;
            _profile.GunSettings.Jitter = defaults.Jitter;
            _profile.GunSettings.Floor = defaults.Floor;
            _profile.GunSettings.DefaultButtonIndex = defaults.DefaultButtonIndex;
        }
        else if (effect == RumbleEffectType.Missile)
        {
            var defaults = new MissileSettings();
            _profile.MissileSettings.DurationSeconds = defaults.DurationSeconds;
            _profile.MissileSettings.AttackSeconds = defaults.AttackSeconds;
            _profile.MissileSettings.DecayPower = defaults.DecayPower;
            _profile.MissileSettings.Punch = defaults.Punch;
        }
        else if (effect == RumbleEffectType.HighGTurn)
        {
            var defaults = new HighGSettings();
            _profile.HighGSettings.DefaultActivationThreshold =
                defaults.DefaultActivationThreshold;
            _profile.HighGSettings.Hysteresis = defaults.Hysteresis;
        }
    }


    private Joystick? TryOpenJoystick(Guid guid)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var di = _inputSvc.ListJoysticks().FirstOrDefault(x => x.InstanceGuid == guid);
            if (di == null) return null;

            return _inputSvc.Open(di, hwnd);
        }
        catch
        {
            return null;
        }
    }

}
