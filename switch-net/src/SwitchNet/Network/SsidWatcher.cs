using SwitchNet.Config;

namespace SwitchNet.Network;

/// <summary>
/// Polls the current Wi-Fi SSID and applies the matching profile's routes when it changes.
/// </summary>
public sealed class SsidWatcher : IDisposable
{
    private readonly ConfigStore _configStore;
    private readonly ProfileSwitcher _switcher;
    private readonly WifiService _wifi = new();
    private readonly System.Windows.Forms.Timer _timer;
    private string? _lastSsid;
    private bool _disposed;

    public SsidWatcher(ConfigStore configStore, ProfileSwitcher switcher)
    {
        _configStore = configStore;
        _switcher = switcher;

        var seconds = Math.Clamp(_configStore.Get().PollIntervalSeconds, 5, 300);
        _timer = new System.Windows.Forms.Timer { Interval = seconds * 1000 };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        _lastSsid = _wifi.GetCurrentSsid();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_configStore.Get().AutoSwitchOnSsidChange)
            return;

        var ssid = _wifi.GetCurrentSsid();
        if (string.IsNullOrWhiteSpace(ssid) || string.Equals(ssid, _lastSsid, StringComparison.OrdinalIgnoreCase))
            return;

        _lastSsid = ssid;
        var profile = _switcher.FindBySsid(ssid);
        if (profile is null)
            return;

        if (string.Equals(_switcher.LastAppliedProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
            return;

        // Already on the right SSID — only fix routes/metrics.
        _ = _switcher.SwitchTo(profile, skipWifiConnect: true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
    }
}
