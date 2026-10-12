using System.Drawing;
using System.Windows.Forms;
using SwitchNet.Config;
using SwitchNet.Network;
using SwitchNet.UI;
using Application = System.Windows.Application;

namespace SwitchNet.Tray;

public sealed class TrayIconManager : IDisposable
{
    private readonly ConfigStore _configStore;
    private readonly ProfileSwitcher _switcher;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private bool _disposed;

    public event EventHandler? ExitRequested;

    public TrayIconManager(ConfigStore configStore, ProfileSwitcher switcher)
    {
        _configStore = configStore;
        _switcher = switcher;
        _icon = TrayIconFactory.Create();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "SwitchNet",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        _switcher.SwitchCompleted += OnSwitchCompleted;
    }

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var config = _configStore.Get();

        var header = new ToolStripLabel("SwitchNet")
        {
            Font = new Font(SystemFonts.MenuFont, FontStyle.Bold),
        };
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripLabel("默认有线 = everyday") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        // Prefer wired → GitLab → CRM order in the menu.
        foreach (var profile in config.Profiles
                     .OrderBy(p => p.IsWired ? 0 : 1)
                     .ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
        {
            var captured = profile;
            var item = new ToolStripMenuItem(captured.DisplayName);
            item.Click += async (_, _) => await SwitchAsync(captured).ConfigureAwait(true);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var settings = new ToolStripMenuItem("Settings…");
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);

        var openConfig = new ToolStripMenuItem("Open config folder");
        openConfig.Click += (_, _) =>
        {
            var dir = Path.GetDirectoryName(_configStore.UserConfigPath)!;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true,
            });
        };
        menu.Items.Add(openConfig);

        var refresh = new ToolStripMenuItem("Reload config");
        refresh.Click += (_, _) =>
        {
            _configStore.Reload();
            _notifyIcon.ContextMenuStrip = BuildMenu();
            ShowBalloon("SwitchNet", "Config reloaded.");
        };
        menu.Items.Add(refresh);

        menu.Items.Add(new ToolStripSeparator());

        var elev = ElevationHelper.IsElevated()
            ? "Running elevated"
            : "Routes need UAC (admin)";
        menu.Items.Add(new ToolStripLabel(elev) { Enabled = false });

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exit);

        return menu;
    }

    private async Task SwitchAsync(NetworkProfile profile)
    {
        _notifyIcon.Text = $"SwitchNet — switching to {profile.DisplayName}…";
        SwitchResult result;
        try
        {
            result = await Task.Run(() => _switcher.SwitchTo(profile)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowBalloon("SwitchNet", $"Error: {ex.Message}");
            return;
        }

        _notifyIcon.Text = result.Success
            ? $"SwitchNet — {profile.DisplayName}"
            : "SwitchNet";
        ShowBalloon("SwitchNet", result.Message);
    }

    private void OnSwitchCompleted(object? sender, SwitchResult result)
    {
        // Balloon already shown from explicit menu switches; SSID watcher path:
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(() =>
            {
                if (!result.Success)
                    ShowBalloon("SwitchNet", result.Message);
            });
        }
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_configStore);
        window.ConfigSaved += (_, _) =>
        {
            _notifyIcon.ContextMenuStrip = BuildMenu();
            ShowBalloon("SwitchNet", "Settings saved.");
        };
        window.Show();
        window.Activate();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _switcher.SwitchCompleted -= OnSwitchCompleted;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}

internal static class TrayIconFactory
{
    /// <summary>Draw a simple green/blue network glyph so we don't ship a binary .ico dependency.</summary>
    public static Icon Create()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var brush = new SolidBrush(Color.FromArgb(255, 16, 120, 72));
            g.FillEllipse(brush, 2, 2, size - 4, size - 4);

            using var pen = new Pen(Color.White, 2.2f);
            g.DrawArc(pen, 8, 8, 16, 16, 200, 140);
            g.DrawArc(pen, 11, 11, 10, 10, 200, 140);
            g.FillEllipse(Brushes.White, 14, 14, 4, 4);
        }

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
