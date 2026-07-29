using JFlightShaker.Config;
using JFlightShaker.Enum;
using SharpDX.DirectInput;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace JFlightShaker.UI;

public partial class KeyboardBindingWindow : Window
{
    private readonly BindingConfig _binding;
    private bool _isListening;
    private SharpDX.DirectInput.Key? _selectedKey;

    public KeyboardBindingWindow(BindingConfig binding)
    {
        InitializeComponent();
        _binding = binding;

        if (binding.ButtonIndex is int keyCode &&
            System.Enum.IsDefined(typeof(SharpDX.DirectInput.Key), keyCode))
        {
            _selectedKey = (SharpDX.DirectInput.Key)keyCode;
            KeyTextBox.Text = _selectedKey.ToString();
        }
        TriggerCombo.SelectedIndex = binding.Trigger == TriggerType.Hold ? 1 : 0;

        ListenBtn.Click += (_, _) =>
        {
            _isListening = !_isListening;
            UpdateListenUi();
            if (_isListening) Focus();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        CancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        SaveBtn.Click += (_, _) => Save();
        UpdateListenUi();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isListening) return;
        var wpfKey = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        string name = wpfKey.ToString();

        if (!System.Enum.TryParse<SharpDX.DirectInput.Key>(name, out var directInputKey))
        {
            MessageBox.Show($"The key {name} is not supported by DirectInput.");
            return;
        }

        _selectedKey = directInputKey;
        KeyTextBox.Text = directInputKey.ToString();
        _isListening = false;
        UpdateListenUi();
        e.Handled = true;
    }

    private void Save()
    {
        if (_selectedKey is not SharpDX.DirectInput.Key key)
        {
            MessageBox.Show("Press Listen and choose a key.");
            return;
        }

        _binding.ButtonIndex = (int)key;
        _binding.Trigger = TriggerCombo.SelectedIndex == 1
            ? TriggerType.Hold
            : TriggerType.Press;
        _binding.AxisName = null;
        _binding.AxisMin = null;
        _binding.AxisMax = null;
        _binding.InvertAxis = false;
        DialogResult = true;
        Close();
    }

    private void UpdateListenUi()
    {
        ListenBtn.Content = _isListening ? "Listening..." : "Listen";
        ListenBtn.Background = _isListening
            ? new SolidColorBrush(Color.FromRgb(230, 164, 52))
            : SystemColors.ControlBrush;
    }
}
