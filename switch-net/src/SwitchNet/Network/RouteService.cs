using System.Net;
using System.Text.RegularExpressions;
using SwitchNet.Config;

namespace SwitchNet.Network;

public sealed class RouteService
{
    /// <summary>
    /// Apply interface metric and routes for the active LAN profile.
    /// Requires elevation (Administrator).
    /// </summary>
    public SwitchResult Apply(AppConfig config, NetworkProfile profile)
    {
        var iface = config.WifiInterfaceName;
        var steps = new List<string>();

        // Prefer this Wi-Fi NIC for outbound traffic.
        var metricResult = ProcessRunner.Run(
            "netsh",
            $"interface ipv4 set interface \"{Escape(iface)}\" metric={profile.InterfaceMetric}");
        steps.Add($"interface metric → {profile.InterfaceMetric}: {(metricResult.Success ? "ok" : metricResult.CombinedOutput)}");

        if (!TryParseCidr(profile.SubnetCidr, out var network, out var mask))
        {
            return SwitchResult.Fail(
                $"Invalid subnetCidr '{profile.SubnetCidr}'. Use form like 192.168.1.0/24.",
                steps);
        }

        // Ensure LAN subnet route via the profile gateway.
        // delete first to avoid duplicates, then add.
        _ = ProcessRunner.Run("route", $"delete {network} mask {mask}");
        var ifIndex = GetInterfaceIndex(iface);
        var lanArgs = ifIndex is not null
            ? $"add {network} mask {mask} {profile.Gateway} metric 1 if {ifIndex}"
            : $"add {network} mask {mask} {profile.Gateway} metric 1";
        var lanAdd = ProcessRunner.Run("route", lanArgs);

        // Retry without IF if interface-bound add failed.
        if (!lanAdd.Success && ifIndex is not null)
        {
            lanAdd = ProcessRunner.Run(
                "route",
                $"add {network} mask {mask} {profile.Gateway} metric 1");
        }

        steps.Add($"LAN route {profile.SubnetCidr} via {profile.Gateway}: {(lanAdd.Success ? "ok" : lanAdd.CombinedOutput)}");

        if (profile.PreferDefaultRoute)
        {
            // Lower-metric default route via this gateway to prefer the active LAN.
            _ = ProcessRunner.Run("route", $"delete 0.0.0.0 mask 0.0.0.0 {profile.Gateway}");
            var defAdd = ProcessRunner.Run(
                "route",
                $"add 0.0.0.0 mask 0.0.0.0 {profile.Gateway} metric {Math.Max(1, profile.InterfaceMetric)}");
            steps.Add($"default route via {profile.Gateway}: {(defAdd.Success ? "ok" : defAdd.CombinedOutput)}");
        }

        var ok = metricResult.Success || lanAdd.Success;
        var message = ok
            ? $"Applied routing for {profile.DisplayName}."
            : $"Partial failure applying routes for {profile.DisplayName}.";

        return new SwitchResult(ok, message, steps);
    }

    public string? GetInterfaceIndex(string interfaceName)
    {
        var result = ProcessRunner.Run("netsh", "interface ipv4 show interfaces");
        if (string.IsNullOrWhiteSpace(result.StdOut))
            return null;

        // Idx  Met  MTU   State        Name
        // 12   25   1500  connected    Wi-Fi
        foreach (var line in result.StdOut.Split('\n'))
        {
            if (!line.Contains(interfaceName, StringComparison.OrdinalIgnoreCase))
                continue;

            var match = Regex.Match(line.Trim(), @"^(\d+)\s+");
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

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

public readonly record struct SwitchResult(bool Success, string Message, IReadOnlyList<string> Steps)
{
    public static SwitchResult Fail(string message, IReadOnlyList<string>? steps = null)
        => new(false, message, steps ?? Array.Empty<string>());

    public static SwitchResult Ok(string message, IReadOnlyList<string>? steps = null)
        => new(true, message, steps ?? Array.Empty<string>());
}
