using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchNet.Config;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _userConfigPath;
    private readonly string _bundledConfigPath;
    private AppConfig? _cached;

    public ConfigStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "SwitchNet");
        Directory.CreateDirectory(dir);
        _userConfigPath = Path.Combine(dir, "profiles.json");
        _bundledConfigPath = Path.Combine(AppContext.BaseDirectory, "Config", "profiles.json");
    }

    public string UserConfigPath => _userConfigPath;

    public AppConfig LoadOrCreate()
    {
        if (_cached is not null)
            return _cached;

        if (!File.Exists(_userConfigPath))
        {
            if (File.Exists(_bundledConfigPath))
                File.Copy(_bundledConfigPath, _userConfigPath);
            else
                Write(_userConfigPath, CreateDefault());
        }

        _cached = Read(_userConfigPath);
        EnsureShape(_cached);
        return _cached;
    }

    public void Save(AppConfig config)
    {
        EnsureShape(config);
        Write(_userConfigPath, config);
        _cached = config;
    }

    public void Reload()
    {
        _cached = null;
        LoadOrCreate();
    }

    public AppConfig Get() => LoadOrCreate();

    private static void EnsureShape(AppConfig config)
    {
        if (config.Profiles.Count == 0)
            config.Profiles = CreateDefault().Profiles;

        if (!config.Profiles.Any(p => p.IsWired))
            config.Profiles.Insert(0, CreateWiredProfile());

        // Normalize known Wi‑Fi profiles if an older user config still has placeholders.
        foreach (var p in config.Profiles)
        {
            if (string.Equals(p.Id, "gitlab-lan", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(p.Mode)) p.Mode = "wifi";
                if (string.IsNullOrWhiteSpace(p.Ssid) || p.Ssid.Contains("COMPANY", StringComparison.OrdinalIgnoreCase))
                    p.Ssid = "1505";
                if (string.IsNullOrWhiteSpace(p.WlanProfileName)) p.WlanProfileName = "1505";
                if (string.IsNullOrWhiteSpace(p.SubnetCidr)) p.SubnetCidr = "192.168.1.0/24";
                if (string.IsNullOrWhiteSpace(p.Gateway)) p.Gateway = "192.168.1.1";
                if (string.IsNullOrWhiteSpace(p.DisplayName) ||
                    string.Equals(p.DisplayName, "GitLab LAN (.1)", StringComparison.Ordinal))
                    p.DisplayName = "GitLab";
            }
            else if (string.Equals(p.Id, "crm-lan", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(p.Mode)) p.Mode = "wifi";
                if (string.IsNullOrWhiteSpace(p.Ssid) || p.Ssid.Contains("COMPANY", StringComparison.OrdinalIgnoreCase))
                    p.Ssid = "CU_bDXR";
                if (string.IsNullOrWhiteSpace(p.WlanProfileName)) p.WlanProfileName = "CU_bDXR";
                if (string.IsNullOrWhiteSpace(p.SubnetCidr)) p.SubnetCidr = "192.168.3.0/24";
                if (string.IsNullOrWhiteSpace(p.Gateway)) p.Gateway = "192.168.3.1";
                if (string.IsNullOrWhiteSpace(p.DisplayName) ||
                    string.Equals(p.DisplayName, "CRM LAN (.3)", StringComparison.Ordinal))
                    p.DisplayName = "CRM";
            }
            else if (string.Equals(p.Id, "default-wired", StringComparison.OrdinalIgnoreCase))
            {
                p.Mode = "wired";
                if (string.IsNullOrWhiteSpace(p.DisplayName))
                    p.DisplayName = "默认有线";
            }
            else if (string.IsNullOrWhiteSpace(p.Mode))
            {
                p.Mode = string.IsNullOrWhiteSpace(p.Ssid) ? "wired" : "wifi";
            }
        }
    }

    private static NetworkProfile CreateWiredProfile() => new()
    {
        Id = "default-wired",
        DisplayName = "默认有线",
        Mode = "wired",
        InterfaceMetric = 10,
        PreferDefaultRoute = false,
    };

    private static AppConfig CreateDefault() => new()
    {
        AutoSwitchOnSsidChange = false,
        WifiInterfaceName = "Wi-Fi 2",
        PreferWifiOverEthernet = true,
        DisconnectWifiOnWired = true,
        EthernetMetric = 10,
        WifiRestingMetric = 50,
        EthernetMetricPenalty = 40,
        WifiMetricMargin = 5,
        PollIntervalSeconds = 15,
        Profiles =
        [
            CreateWiredProfile(),
            new NetworkProfile
            {
                Id = "gitlab-lan",
                DisplayName = "GitLab",
                Mode = "wifi",
                Ssid = "1505",
                WlanProfileName = "1505",
                SubnetCidr = "192.168.1.0/24",
                Gateway = "192.168.1.1",
                InterfaceMetric = 10,
                PreferDefaultRoute = true,
            },
            new NetworkProfile
            {
                Id = "crm-lan",
                DisplayName = "CRM",
                Mode = "wifi",
                Ssid = "CU_bDXR",
                WlanProfileName = "CU_bDXR",
                SubnetCidr = "192.168.3.0/24",
                Gateway = "192.168.3.1",
                InterfaceMetric = 10,
                PreferDefaultRoute = true,
            },
        ],
    };

    private static AppConfig Read(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
    }

    private static void Write(string path, AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }
}
