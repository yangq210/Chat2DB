using System.Windows;
using SwitchNet.Config;
using SwitchNet.Network;
using SwitchNet.Tray;

namespace SwitchNet;

public partial class App : System.Windows.Application
{
    private TrayIconManager? _tray;
    private SsidWatcher? _ssidWatcher;
    private ProfileSwitcher? _switcher;
    private ConfigStore? _configStore;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _configStore = new ConfigStore();
        var config = _configStore.LoadOrCreate();

        // CLI: SwitchNet.exe --apply <profile-id>
        // Used after UAC elevation to apply routes without showing the tray again.
        if (TryHandleApplyArgs(e.Args, config, out var applyExitCode))
        {
            Shutdown(applyExitCode);
            return;
        }

        _switcher = new ProfileSwitcher(_configStore);
        _tray = new TrayIconManager(_configStore, _switcher);
        _tray.ExitRequested += (_, _) => Shutdown();

        if (config.AutoSwitchOnSsidChange)
        {
            _ssidWatcher = new SsidWatcher(_configStore, _switcher);
            _ssidWatcher.Start();
        }

        _tray.ShowBalloon(
            "SwitchNet",
            "Running in the tray. Right-click to pick GitLab LAN (.1) or CRM LAN (.3).");
    }

    private static bool TryHandleApplyArgs(string[] args, AppConfig config, out int exitCode)
    {
        exitCode = 0;
        if (args.Length < 2 || !string.Equals(args[0], "--apply", StringComparison.OrdinalIgnoreCase))
            return false;

        var profileId = args[1].Trim('"');
        var profile = config.Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            MessageBox.Show(
                $"Unknown profile id: {profileId}",
                "SwitchNet",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            exitCode = 1;
            return true;
        }

        try
        {
            var result = ProfileSwitcher.ApplyElevatedOperations(config, profile);
            if (!result.Success)
            {
                MessageBox.Show(
                    result.Message,
                    "SwitchNet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                exitCode = 1;
                return true;
            }

            exitCode = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to apply profile '{profile.DisplayName}':\n{ex.Message}",
                "SwitchNet",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            exitCode = 1;
        }

        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _ssidWatcher?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
