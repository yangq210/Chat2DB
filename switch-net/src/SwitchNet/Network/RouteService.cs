using System.Net;
using System.Text.RegularExpressions;
using SwitchNet.Config;

namespace SwitchNet.Network;

public sealed class RouteService
{
    /// <summary>
    /// Apply metrics/routes for a profile. Wired restores Ethernet as everyday default;
    /// Wi‑Fi temporarily prefers USB for that LAN.
    /// Requires elevation (Administrator).
    /// </summary>
    public SwitchResult Apply(AppConfig config, NetworkProfile profile)
    {
        return profile.IsWired
            ? RestoreWired(config, profile)
            : PreferWifi(config, profile);
    }

    /// <summary>
    /// Everyday default: Ethernet metric lower than USB Wi‑Fi; remove temporary Wi‑Fi routes.
    /// </summary>
    public SwitchResult RestoreWired(AppConfig config, NetworkProfile profile)
    {
        var steps = new List<string>();
        var interfaces = ListInterfaces();
        var ethernet = FindEthernetInterfaces(interfaces, config);
        var wifiName = config.WifiInterfaceName;
        var ethMetric = profile.InterfaceMetric > 0 ? profile.InterfaceMetric : Math.Max(1, config.EthernetMetric);
        var wifiResting = Math.Max(ethMetric + 1, config.WifiRestingMetric);

        if (ethernet.Count == 0)
            steps.Add("No Ethernet interface found (check ethernetInterfaceNames / cable).");

        var ethOk = false;
        foreach (var eth in ethernet)
        {
            var ethResult = ProcessRunner.Run(
                "netsh",
                $"interface ipv4 set interface \"{Escape(eth.Name)}\" metric={ethMetric}");
            ethOk |= ethResult.Success;
            steps.Add(
                $"Ethernet '{eth.Name}' metric → {ethMetric}: " +
                (ethResult.Success ? "ok" : ethResult.CombinedOutput));
        }

        var wifiResult = ProcessRunner.Run(
            "netsh",
            $"interface ipv4 set interface \"{Escape(wifiName)}\" metric={wifiResting}");
        steps.Add(
            $"USB Wi‑Fi '{wifiName}' metric → {wifiResting} (resting): " +
            (wifiResult.Success ? "ok" : wifiResult.CombinedOutput));

        // Remove temporary GitLab/CRM LAN + default routes we may have added.
        foreach (var wifiProfile in config.Profiles.Where(p => p.IsWifi))
            ClearWifiProfileRoutes(wifiProfile, steps);

        var ok = ethernet.Count == 0 ? wifiResult.Success : (ethOk || wifiResult.Success);

        return new SwitchResult(
            ok,
            ok
                ? "Restored 默认有线 — Ethernet preferred; Wi‑Fi routes cleared."
                : "Partial failure restoring wired default.",
            steps);
    }

    /// <summary>
    /// Temporary hop: USB Wi‑Fi wins over Ethernet for this profile’s LAN (and optional default).
    /// </summary>
    public SwitchResult PreferWifi(AppConfig config, NetworkProfile profile)
    {
        var wifiName = config.WifiInterfaceName;
        var steps = new List<string>();
        var interfaces = ListInterfaces();

        var wifi = FindInterface(interfaces, wifiName);
        if (wifi is null)
            steps.Add($"Wi‑Fi interface '{wifiName}' not found. Check wifiInterfaceName (USB adapter).");

        var ethernet = FindEthernetInterfaces(interfaces, config);
        var wifiMetric = ResolveWifiMetric(config, profile, ethernet);
        steps.Add($"Target USB Wi‑Fi metric: {wifiMetric}");

        // Clear other Wi‑Fi profiles’ forced routes first.
        foreach (var other in config.Profiles.Where(p => p.IsWifi && !string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase)))
            ClearWifiProfileRoutes(other, steps);

        if (config.PreferWifiOverEthernet)
        {
            var ethFloor = Math.Max(wifiMetric + Math.Max(1, config.EthernetMetricPenalty), wifiMetric + 1);
            foreach (var eth in ethernet)
            {
                if (eth.Metric <= wifiMetric)
                {
                    var ethResult = ProcessRunner.Run(
                        "netsh",
                        $"interface ipv4 set interface \"{Escape(eth.Name)}\" metric={ethFloor}");
                    steps.Add(
                        $"Ethernet '{eth.Name}' metric {eth.Metric} → {ethFloor}: " +
                        (ethResult.Success ? "ok" : ethResult.CombinedOutput));
                }
                else
                {
                    steps.Add($"Ethernet '{eth.Name}' metric {eth.Metric} already > Wi‑Fi; left unchanged");
                }
            }
        }

        var metricResult = ProcessRunner.Run(
            "netsh",
            $"interface ipv4 set interface \"{Escape(wifiName)}\" metric={wifiMetric}");
        steps.Add(
            $"USB Wi‑Fi '{wifiName}' metric → {wifiMetric}: " +
            (metricResult.Success ? "ok" : metricResult.CombinedOutput));

        if (!TryParseCidr(profile.SubnetCidr, out var network, out var mask))
        {
            return SwitchResult.Fail(
                $"Invalid subnetCidr '{profile.SubnetCidr}'. Use form like 192.168.1.0/24.",
                steps);
        }

        _ = ProcessRunner.Run("route", $"delete {network} mask {mask}");
        var ifIndex = wifi?.Index.ToString() ?? GetInterfaceIndex(wifiName);
        var lanArgs = ifIndex is not null
            ? $"add {network} mask {mask} {profile.Gateway} metric 1 if {ifIndex}"
            : $"add {network} mask {mask} {profile.Gateway} metric 1";
        var lanAdd = ProcessRunner.Run("route", lanArgs);

        if (!lanAdd.Success && ifIndex is not null)
        {
            lanAdd = ProcessRunner.Run(
                "route",
                $"add {network} mask {mask} {profile.Gateway} metric 1");
        }

        steps.Add(
            $"LAN route {profile.SubnetCidr} via {profile.Gateway}" +
            (ifIndex is not null ? $" (if {ifIndex})" : "") +
            $": {(lanAdd.Success ? "ok" : lanAdd.CombinedOutput)}");

        if (profile.PreferDefaultRoute)
        {
            _ = ProcessRunner.Run("route", $"delete 0.0.0.0 mask 0.0.0.0 {profile.Gateway}");
            var defMetric = Math.Max(1, wifiMetric);
            var defArgs = ifIndex is not null
                ? $"add 0.0.0.0 mask 0.0.0.0 {profile.Gateway} metric {defMetric} if {ifIndex}"
                : $"add 0.0.0.0 mask 0.0.0.0 {profile.Gateway} metric {defMetric}";
            var defAdd = ProcessRunner.Run("route", defArgs);
            if (!defAdd.Success && ifIndex is not null)
            {
                defAdd = ProcessRunner.Run(
                    "route",
                    $"add 0.0.0.0 mask 0.0.0.0 {profile.Gateway} metric {defMetric}");
            }

            steps.Add(
                $"default route via {profile.Gateway} metric {defMetric}: " +
                (defAdd.Success ? "ok" : defAdd.CombinedOutput));
        }

        var ok = metricResult.Success || lanAdd.Success;
        return new SwitchResult(
            ok,
            ok
                ? $"Applied {profile.DisplayName} (USB Wi‑Fi preferred until 默认有线)."
                : $"Partial failure applying routes for {profile.DisplayName}.",
            steps);
    }

    private static void ClearWifiProfileRoutes(NetworkProfile wifiProfile, List<string> steps)
    {
        if (!string.IsNullOrWhiteSpace(wifiProfile.Gateway))
        {
            var delDef = ProcessRunner.Run("route", $"delete 0.0.0.0 mask 0.0.0.0 {wifiProfile.Gateway}");
            steps.Add(
                $"clear default via {wifiProfile.Gateway}: " +
                (delDef.Success || delDef.CombinedOutput.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    ? "ok"
                    : delDef.CombinedOutput));
        }

        if (TryParseCidr(wifiProfile.SubnetCidr, out var network, out var mask))
        {
            var delLan = ProcessRunner.Run("route", $"delete {network} mask {mask}");
            steps.Add(
                $"clear LAN {wifiProfile.SubnetCidr}: " +
                (delLan.Success || delLan.CombinedOutput.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    ? "ok"
                    : delLan.CombinedOutput));
        }
    }

    private static int ResolveWifiMetric(
        AppConfig config,
        NetworkProfile profile,
        IReadOnlyList<InterfaceInfo> ethernet)
    {
        var desired = Math.Clamp(profile.InterfaceMetric, 1, 9999);
        if (!config.PreferWifiOverEthernet || ethernet.Count == 0)
            return desired;

        var minEth = ethernet.Min(e => e.Metric);
        if (desired >= minEth)
            desired = Math.Max(1, minEth - Math.Max(1, config.WifiMetricMargin));

        return desired;
    }

    private static List<InterfaceInfo> FindEthernetInterfaces(
        IReadOnlyList<InterfaceInfo> interfaces,
        AppConfig config)
    {
        var named = config.EthernetInterfaceNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return interfaces
            .Where(i =>
            {
                if (string.Equals(i.Name, config.WifiInterfaceName, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (named.Count > 0)
                    return named.Contains(i.Name);

                return i.Name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    private static InterfaceInfo? FindInterface(IReadOnlyList<InterfaceInfo> interfaces, string name) =>
        interfaces.FirstOrDefault(i =>
            string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<InterfaceInfo> ListInterfaces()
    {
        var result = ProcessRunner.Run("netsh", "interface ipv4 show interfaces");
        var list = new List<InterfaceInfo>();
        if (string.IsNullOrWhiteSpace(result.StdOut))
            return list;

        foreach (var raw in result.StdOut.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var match = Regex.Match(
                line.Trim(),
                @"^(?<idx>\d+)\s+(?<met>\d+)\s+(?<mtu>\d+)\s+(?<state>\S+)\s+(?<name>.+?)\s*$");
            if (!match.Success)
                continue;

            list.Add(new InterfaceInfo(
                int.Parse(match.Groups["idx"].Value),
                int.Parse(match.Groups["met"].Value),
                match.Groups["state"].Value,
                match.Groups["name"].Value.Trim()));
        }

        return list;
    }

    public string? GetInterfaceIndex(string interfaceName) =>
        ListInterfaces()
            .FirstOrDefault(i => string.Equals(i.Name, interfaceName, StringComparison.OrdinalIgnoreCase))
            ?.Index.ToString();

    public static bool TryParseCidr(string cidr, out string network, out string mask)
    {
        network = "";
        mask = "";
        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        var parts = cidr.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix) || prefix is < 0 or > 32)
            return false;

        if (!IPAddress.TryParse(parts[0], out _))
            return false;

        network = parts[0];
        mask = PrefixToMask(prefix);
        return true;
    }

    private static string PrefixToMask(int prefix)
    {
        uint bits = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        return $"{(bits >> 24) & 0xff}.{(bits >> 16) & 0xff}.{(bits >> 8) & 0xff}.{bits & 0xff}";
    }

    private static string Escape(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);
}

public sealed record InterfaceInfo(int Index, int Metric, string State, string Name);

public readonly record struct SwitchResult(bool Success, string Message, IReadOnlyList<string> Steps)
{
    public static SwitchResult Fail(string message, IReadOnlyList<string>? steps = null)
        => new(false, message, steps ?? Array.Empty<string>());

    public static SwitchResult Ok(string message, IReadOnlyList<string>? steps = null)
        => new(true, message, steps ?? Array.Empty<string>());
}
