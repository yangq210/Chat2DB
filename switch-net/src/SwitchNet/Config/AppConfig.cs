using System.Text.Json.Serialization;

namespace SwitchNet.Config;

public sealed class AppConfig
{
    [JsonPropertyName("autoSwitchOnSsidChange")]
    public bool AutoSwitchOnSsidChange { get; set; } = true;

    [JsonPropertyName("wifiInterfaceName")]
    public string WifiInterfaceName { get; set; } = "Wi-Fi";

    [JsonPropertyName("pollIntervalSeconds")]
    public int PollIntervalSeconds { get; set; } = 15;

    [JsonPropertyName("profiles")]
    public List<NetworkProfile> Profiles { get; set; } = new();
}

public sealed class NetworkProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    /// <summary>Wi-Fi SSID to connect to (placeholder until you set the real company SSID).</summary>
    [JsonPropertyName("ssid")]
    public string Ssid { get; set; } = "";

    /// <summary>
    /// Optional Windows WLAN profile name. Leave empty to use <see cref="Ssid"/>.
    /// Required if the saved profile name differs from the SSID.
    /// </summary>
    [JsonPropertyName("wlanProfileName")]
    public string WlanProfileName { get; set; } = "";

    /// <summary>LAN subnet in CIDR form, e.g. 192.168.1.0/24.</summary>
    [JsonPropertyName("subnetCidr")]
    public string SubnetCidr { get; set; } = "";

    /// <summary>Default gateway for this LAN, e.g. 192.168.1.1.</summary>
    [JsonPropertyName("gateway")]
    public string Gateway { get; set; } = "";

    /// <summary>Lower = preferred. Applied to the Wi-Fi interface when this profile is active.</summary>
    [JsonPropertyName("interfaceMetric")]
    public int InterfaceMetric { get; set; } = 10;

    /// <summary>When true, also set a 0.0.0.0/0 route via this profile's gateway (elevated).</summary>
    [JsonPropertyName("preferDefaultRoute")]
    public bool PreferDefaultRoute { get; set; } = true;

    public string EffectiveWlanProfileName =>
        string.IsNullOrWhiteSpace(WlanProfileName) ? Ssid : WlanProfileName;
}
