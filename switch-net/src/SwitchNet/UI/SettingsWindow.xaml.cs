using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SwitchNet.Config;

namespace SwitchNet.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigStore _configStore;
    private readonly List<ProfileEditor> _editors = new();

    public event EventHandler? ConfigSaved;

    public SettingsWindow(ConfigStore configStore)
    {
        _configStore = configStore;
        InitializeComponent();
        LoadFromConfig(_configStore.Get());
    }

    private void LoadFromConfig(AppConfig config)
    {
        AutoSwitchCheck.IsChecked = config.AutoSwitchOnSsidChange;
        PreferWifiCheck.IsChecked = config.PreferWifiOverEthernet;
        InterfaceNameBox.Text = config.WifiInterfaceName;
        PollIntervalBox.Text = config.PollIntervalSeconds.ToString();

        ProfilesHost.Items.Clear();
        _editors.Clear();

        foreach (var profile in config.Profiles)
        {
            var editor = new ProfileEditor(profile);
            _editors.Add(editor);
            ProfilesHost.Items.Add(editor.Root);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PollIntervalBox.Text.Trim(), out var poll) || poll < 5)
        {
            MessageBox.Show(this, "Poll interval must be an integer ≥ 5.", "SwitchNet");
            return;
        }

        var config = _configStore.Get();
        config.AutoSwitchOnSsidChange = AutoSwitchCheck.IsChecked == true;
        config.PreferWifiOverEthernet = PreferWifiCheck.IsChecked == true;
        config.WifiInterfaceName = InterfaceNameBox.Text.Trim();
        config.PollIntervalSeconds = poll;
        config.Profiles = _editors.Select(ed => ed.ToProfile()).ToList();

        _configStore.Save(config);
        ConfigSaved?.Invoke(this, EventArgs.Empty);
        MessageBox.Show(this, "Saved to:\n" + _configStore.UserConfigPath, "SwitchNet");
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _configStore.UserConfigPath,
            UseShellExecute = true,
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class ProfileEditor
    {
        private readonly string _id;
        private readonly TextBox _displayName = new() { Margin = new Thickness(0, 0, 0, 4) };
        private readonly TextBox _ssid = new() { Margin = new Thickness(0, 0, 0, 4) };
        private readonly TextBox _wlan = new() { Margin = new Thickness(0, 0, 0, 4) };
        private readonly TextBox _subnet = new() { Margin = new Thickness(0, 0, 0, 4) };
        private readonly TextBox _gateway = new() { Margin = new Thickness(0, 0, 0, 4) };
        private readonly TextBox _metric = new() { Margin = new Thickness(0, 0, 0, 4), Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly CheckBox _preferDefault = new() { Content = "Prefer default route via this gateway", Margin = new Thickness(0, 4, 0, 0) };

        public Border Root { get; }

        public ProfileEditor(NetworkProfile profile)
        {
            _id = profile.Id;
            _displayName.Text = profile.DisplayName;
            _ssid.Text = profile.Ssid;
            _wlan.Text = profile.WlanProfileName;
            _subnet.Text = profile.SubnetCidr;
            _gateway.Text = profile.Gateway;
            _metric.Text = profile.InterfaceMetric.ToString();
            _preferDefault.IsChecked = profile.PreferDefaultRoute;

            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(Labeled("Display name", _displayName));
            panel.Children.Add(Labeled("SSID", _ssid));
            panel.Children.Add(Labeled("WLAN profile name (optional)", _wlan));
            panel.Children.Add(Labeled("Subnet (CIDR)", _subnet));
            panel.Children.Add(Labeled("Gateway", _gateway));
            panel.Children.Add(Labeled("Interface metric", _metric));
            panel.Children.Add(_preferDefault);

            Root = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xD5, 0xD0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 12),
                Child = panel,
            };
        }

        public NetworkProfile ToProfile()
        {
            _ = int.TryParse(_metric.Text.Trim(), out var metric);
            if (metric <= 0) metric = 10;

            return new NetworkProfile
            {
                Id = _id,
                DisplayName = _displayName.Text.Trim(),
                Ssid = _ssid.Text.Trim(),
                WlanProfileName = _wlan.Text.Trim(),
                SubnetCidr = _subnet.Text.Trim(),
                Gateway = _gateway.Text.Trim(),
                InterfaceMetric = metric,
                PreferDefaultRoute = _preferDefault.IsChecked == true,
            };
        }

        private static UIElement Labeled(string label, UIElement field)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x66, 0x60)),
            });
            stack.Children.Add(field);
            return stack;
        }
    }
}
