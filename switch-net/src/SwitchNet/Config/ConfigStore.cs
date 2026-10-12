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
        if (_cached.Profiles.Count == 0)
            _cached.Profiles = CreateDefault().Profiles;

        return _cached;
    }

    public void Save(AppConfig config)
    {
        Write(_userConfigPath, config);
        _cached = config;
    }

    public void Reload()
    {
        _cached = null;
        LoadOrCreate();
    }

    public AppConfig Get() => LoadOrCreate();

    private static AppConfig CreateDefault() => new()
    {
        AutoSwitchOnSsidChange = true,
        WifiInterfaceName = "Wi-Fi 2",
        PreferWifiOverEthernet = true,
        EthernetMetricPenalty = 40,
        WifiMetricMargin = 5,
        PollIntervalSeconds = 15,
        Profiles =
        [
            new NetworkProfile
            {
                Id = "gitlab-lan",
                DisplayName = "GitLab LAN (.1)",
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
                DisplayName = "CRM LAN (.3)",
                Ssid = "COMPANY-CRM-WIFI",
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
