using System.Text.Json.Serialization;

namespace SwitchNet.Config;

public sealed class AppConfig
{
    /// <summary>
    /// When true, applying a matching Wi‑Fi SSID also applies that Wi‑Fi profile’s routes.
    /// Default false — everyday resting state is wired; use tray actions explicitly.
    /// </summary>
    [JsonPropertyName("autoSwitchOnSsidChange")]
    public bool AutoSwitchOnSsidChange { get; set; }

    /// <summary>
    /// Windows display name of the <b>external USB</b> Wi‑Fi adapter
    /// (often <c>Wi-Fi 2</c>; confirm with <c>netsh interface show interface</c>).
    /// Do not point this at the laptop’s built-in Wi‑Fi.
    /// </summary>
    [JsonPropertyName("wifiInterfaceName")]
    public string WifiInterfaceName { get; set; } = "Wi-Fi 2";

    /// <summary>
    /// Optional Ethernet adapter names. Empty = auto-detect names containing "Ethernet".
    /// </summary>
    [JsonPropertyName("ethernetInterfaceNames")]
    public List<string> EthernetInterfaceNames { get; set; } = new();

    /// <summary>
    /// When applying a <b>Wi‑Fi</b> profile, ensure USB Wi‑Fi beats Ethernet for that session.
    /// Does not change the resting “默认有线” behavior.
    /// </summary>
    [JsonPropertyName("preferWifiOverEthernet")]
    public bool PreferWifiOverEthernet { get; set; } = true;

    /// <summary>
    /// When applying <b>默认有线</b>, disconnect USB Wi‑Fi for a clear back-to-wired state.
    /// </summary>
    [JsonPropertyName("disconnectWifiOnWired")]
    public bool DisconnectWifiOnWired { get; set; } = true;

    /// <summary>Ethernet interface metric used by 默认有线 (lower = preferred).</summary>
    [JsonPropertyName("ethernetMetric")]
    public int EthernetMetric { get; set; } = 10;

    /// <summary>USB Wi‑Fi metric after returning to 默认有线 (higher than Ethernet).</summary>
    [JsonPropertyName("wifiRestingMetric")]
    public int WifiRestingMetric { get; set; } = 50;

    /// <summary>
    /// Added to the Wi‑Fi metric when raising Ethernet during a Wi‑Fi profile apply.
    /// </summary>
    [JsonPropertyName("ethernetMetricPenalty")]
    public int EthernetMetricPenalty { get; set; } = 40;

    /// <summary>
    /// If the Wi‑Fi profile metric would not beat Ethernet, drop it by at least this margin.
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

    /// <summary>
    /// <c>wired</c> = everyday Ethernet default; <c>wifi</c> = temporary GitLab/CRM hop.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "wifi";

    /// <summary>Wi-Fi SSID (ignored for wired profiles).</summary>
    [JsonPropertyName("ssid")]
    public string Ssid { get; set; } = "";

    /// <summary>
    /// Optional Windows WLAN profile name. Leave empty to use <see cref="Ssid"/>.
    /// </summary>
    [JsonPropertyName("wlanProfileName")]
    public string WlanProfileName { get; set; } = "";

    /// <summary>LAN subnet in CIDR form, e.g. 192.168.1.0/24. Wired may be empty (TBD).</summary>
    [JsonPropertyName("subnetCidr")]
    public string SubnetCidr { get; set; } = "";

    /// <summary>Default gateway for this LAN. Wired may be empty (TBD).</summary>
    [JsonPropertyName("gateway")]
    public string Gateway { get; set; } = "";

    /// <summary>
    /// For Wi‑Fi profiles: USB Wi‑Fi metric while active.
    /// For wired: Ethernet metric override (0 = use AppConfig.EthernetMetric).
    /// </summary>
    [JsonPropertyName("interfaceMetric")]
    public int InterfaceMetric { get; set; } = 10;

    /// <summary>When true on Wi‑Fi profiles, also set a 0.0.0.0/0 route via this gateway.</summary>
    [JsonPropertyName("preferDefaultRoute")]
    public bool PreferDefaultRoute { get; set; } = true;

    [JsonIgnore]
    public bool IsWired =>
        string.Equals(Mode, "wired", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsWifi => !IsWired;

    public string EffectiveWlanProfileName =>
        string.IsNullOrWhiteSpace(WlanProfileName) ? Ssid : WlanProfileName;
}
