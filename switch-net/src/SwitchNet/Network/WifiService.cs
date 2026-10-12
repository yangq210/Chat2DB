using System.Text.RegularExpressions;
using SwitchNet.Config;

namespace SwitchNet.Network;

public sealed class WifiService
{
    /// <summary>
    /// Connect to the profile SSID using netsh wlan.
    /// The WLAN profile must already exist (user has connected once and saved the password).
    /// </summary>
    public CommandResult Connect(NetworkProfile profile)
    {
        var name = EscapeArg(profile.EffectiveWlanProfileName);
        var ssid = EscapeArg(profile.Ssid);

        // Prefer explicit ssid + name when they may differ.
        var args = string.Equals(profile.EffectiveWlanProfileName, profile.Ssid, StringComparison.Ordinal)
            ? $"wlan connect name=\"{name}\""
            : $"wlan connect name=\"{name}\" ssid=\"{ssid}\"";

        return ProcessRunner.Run("netsh", args);
    }

    public string? GetCurrentSsid()
    {
        var result = ProcessRunner.Run("netsh", "wlan show interfaces");
        if (!result.Success && string.IsNullOrWhiteSpace(result.StdOut))
            return null;

        // SSID                   : MyNetwork
        var match = Regex.Match(
            result.StdOut,
            @"^\s*SSID\s*:\s*(.+)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        var value = match.Groups[1].Value.Trim();
        // netsh also prints "BSSID" lines; the interface SSID line is usually the non-BSSID one.
        // Prefer the first SSID that is not empty and not a BSSID-looking field already filtered by ^SSID.
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public bool IsProfileKnown(string wlanProfileName)
    {
        var result = ProcessRunner.Run("netsh", "wlan show profiles");
        if (string.IsNullOrWhiteSpace(result.StdOut))
            return false;

        return result.StdOut.Contains(wlanProfileName, StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeArg(string value) =>
        value.Replace("\"", "\\\"", StringComparison.Ordinal);
}
