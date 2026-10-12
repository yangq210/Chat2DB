using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace SwitchNet.Network;

public static class ElevationHelper
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Relaunch this executable elevated with --apply &lt;profileId&gt;.
    /// Triggers a UAC prompt. Returns false if the user cancels UAC.
    /// </summary>
    public static bool RelaunchElevatedToApply(string profileId)
    {
        var exe = Environment.ProcessPath
                  ?? Path.Combine(AppContext.BaseDirectory, "SwitchNet.exe");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--apply \"{profileId}\"",
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return false;

            process.WaitForExit(60_000);
            return process.ExitCode == 0 || process.HasExited;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled the UAC dialog.
            return false;
        }
    }
}
