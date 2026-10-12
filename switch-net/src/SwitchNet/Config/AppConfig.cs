using System.Text.Json.Serialization;

namespace SwitchNet.Config;

public sealed class AppConfig
{
    [JsonPropertyName("autoSwitchOnSsidChange")]
    public bool AutoSwitchOnSsidChange { get; set; } = true;

    /// <summary>
    /// Windows display name of the <b>external USB</b> Wi‑Fi adapter
    /// (often <c>Wi-Fi 2</c>; confirm with <c>netsh interface show interface</c>).
    /// Do not point this at the laptop’s built-in Wi‑Fi.
    /// </summary>
    [JsonPropertyName("wifiInterfaceName")]
    public string WifiInterfaceName { get; set; } = "Wi-Fi 2";

    /// <summary>
    /// Optional Ethernet adapter names. Empty = auto-detect names containing "Ethernet".
    /// Used to raise their metrics so USB Wi‑Fi wins while both stay connected.
    /// </summary>
    [JsonPropertyName("ethernetInterfaceNames")]
    public List<string> EthernetInterfaceNames { get; set; } = new();

    /// <summary>
    /// When true (default), ensure USB Wi‑Fi metric is lower than Ethernet and raise
    /// competing Ethernet metrics so Win11 does not keep preferring the wired NIC.
    /// </summary>
    [JsonPropertyName("preferWifiOverEthernet")]
    public bool PreferWifiOverEthernet { get; set; } = true;

    /// <summary>
    /// Added to the Wi‑Fi metric when raising Ethernet (Ethernet metric becomes wifi + this).
    /// </summary>
    [JsonPropertyName("ethernetMetricPenalty")]
    public int EthernetMetricPenalty { get; set; } = 40;

    /// <summary>
    /// If the profile Wi‑Fi metric would not beat Ethernet, drop it by at least this margin.
    /// </summary>
    [JsonPropertyName("wifiMetricMargin")]
    public int WifiMetricMargin { get; set; } = 5;

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
