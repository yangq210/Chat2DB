using SwitchNet.Config;

namespace SwitchNet.Network;

public sealed class ProfileSwitcher
{
    private readonly ConfigStore _configStore;
    private readonly WifiService _wifi = new();
    private readonly RouteService _routes = new();
    private string? _lastAppliedProfileId;

    public ProfileSwitcher(ConfigStore configStore)
    {
        _configStore = configStore;
    }

    public string? LastAppliedProfileId => _lastAppliedProfileId;

    public event EventHandler<SwitchResult>? SwitchCompleted;

    /// <summary>
    /// Connect Wi-Fi (no admin required), then apply routes/metrics (admin via UAC if needed).
    /// </summary>
    public SwitchResult SwitchTo(NetworkProfile profile, bool skipWifiConnect = false)
    {
        var config = _configStore.Get();
        var steps = new List<string>();

        if (!skipWifiConnect)
        {
            if (!_wifi.IsProfileKnown(profile.EffectiveWlanProfileName))
            {
                var warn =
                    $"Windows has no saved WLAN profile named '{profile.EffectiveWlanProfileName}'. " +
                    "Connect once manually (and save the password), or set wlanProfileName in config.";
                steps.Add(warn);
                // Still attempt connect — netsh may work if the name matches differently.
            }

            var wifiResult = _wifi.Connect(profile);
            steps.Add(
                wifiResult.Success
                    ? $"Wi-Fi connect to '{profile.Ssid}': ok"
                    : $"Wi-Fi connect to '{profile.Ssid}': {wifiResult.CombinedOutput}");
        }

        SwitchResult routeResult;
        if (ElevationHelper.IsElevated())
        {
            routeResult = _routes.Apply(config, profile);
        }
        else
        {
            var elevated = ElevationHelper.RelaunchElevatedToApply(profile.Id);
            routeResult = elevated
                ? SwitchResult.Ok(
                    $"Applied routing for {profile.DisplayName} (elevated).",
                    new[] { "UAC elevation succeeded; routes applied in elevated process." })
                : SwitchResult.Fail(
                    "Route changes require Administrator. UAC was cancelled or elevation failed.",
                    new[] { "UAC elevation failed or cancelled." });
        }

        steps.AddRange(routeResult.Steps);
        var combined = new SwitchResult(
            routeResult.Success,
            routeResult.Success
                ? $"Switched to {profile.DisplayName}."
                : routeResult.Message,
            steps);

        if (combined.Success)
            _lastAppliedProfileId = profile.Id;

        SwitchCompleted?.Invoke(this, combined);
        return combined;
    }

    /// <summary>Called from elevated --apply process (routes only).</summary>
    public static SwitchResult ApplyElevatedOperations(AppConfig config, NetworkProfile profile)
    {
        var routes = new RouteService();
        return routes.Apply(config, profile);
    }

    public NetworkProfile? FindBySsid(string? ssid)
    {
        if (string.IsNullOrWhiteSpace(ssid))
            return null;

        return _configStore.Get().Profiles.FirstOrDefault(p =>
            string.Equals(p.Ssid, ssid, StringComparison.OrdinalIgnoreCase));
    }
}
