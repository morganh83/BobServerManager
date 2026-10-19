using BobServerManager.Core;
using BobServerManager.Models;

namespace BobServerManager.UI;

public class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly ServerProcessManager _server = new();
    private readonly NotificationDispatcher _dispatcher;
    private readonly RebootScheduler _scheduler;
    private readonly System.Windows.Forms.Timer _schedulerTimer = new();

    public MainForm()
    {
        _settings = SettingsStore.Load();
        _dispatcher = new NotificationDispatcher(_server);
        _scheduler = new RebootScheduler(_settings, _server, _dispatcher, BuildLaunch);
        _scheduler.RecomputeNext();

        Text = "Beasts of Bermuda Server Manager";
        Width = 900;
        Height = 700;
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(new TabPage("Dashboard") { Controls = { new DashboardTab(_settings, _server, _scheduler, _dispatcher) } });
        tabs.TabPages.Add(new TabPage("Startup Switches") { Controls = { new StartupTab(_settings, SaveSettings) } });
        tabs.TabPages.Add(new TabPage("Reboot Schedule") { Controls = { new RebootTab(_settings, SaveSettings, () => _scheduler.RecomputeNext()) } });
        tabs.TabPages.Add(new TabPage("Server Updates") { Controls = { new UpdateTab(_settings, SaveSettings) } });
        tabs.TabPages.Add(new TabPage("Config Editor") { Controls = { new ConfigEditorTab(_settings, SaveSettings) } });
        Controls.Add(tabs);

        _schedulerTimer.Interval = 5000;
        _schedulerTimer.Tick += async (_, _) => await _scheduler.TickAsync();
        _schedulerTimer.Start();

        if (_settings.AutoStartServerOnAppLaunch && !string.IsNullOrWhiteSpace(_settings.Startup.ExePath) && File.Exists(_settings.Startup.ExePath))
        {
            try { _server.Start(_settings.Startup.ExePath, _settings.Startup.BuildArguments()); }
            catch { /* surfaced via the dashboard log instead of a startup dialog */ }
        }

        FormClosing += OnFormClosing;
    }

    private (string exePath, string args) BuildLaunch() => (_settings.Startup.ExePath, _settings.Startup.BuildArguments());

    private void SaveSettings() => SettingsStore.Save(_settings);

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        SaveSettings();

        if (_server.IsRunning)
        {
            var result = MessageBox.Show(
                "The Beasts of Bermuda server is still running. Stop it before closing this tool?\n\n" +
                "Choosing \"No\" leaves the server running in the background.",
                "Bob Server Manager", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) { e.Cancel = true; return; }
            if (result == DialogResult.Yes)
            {
                e.Cancel = true; // hold the form open long enough to shut down cleanly
                _ = ShutdownThenCloseAsync();
            }
        }
    }

    private async Task ShutdownThenCloseAsync()
    {
        await _server.StopGracefullyAsync(_settings.Reboot.ShutdownCommand, _settings.Reboot.ShutdownTimeoutSeconds);
        FormClosing -= OnFormClosing;
        Close();
    }
}
