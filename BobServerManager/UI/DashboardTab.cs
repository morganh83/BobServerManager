using BobServerManager.Core;
using BobServerManager.Models;

namespace BobServerManager.UI;

public class DashboardTab : UserControl
{
    private readonly AppSettings _settings;
    private readonly ServerProcessManager _server;
    private readonly RebootScheduler _scheduler;
    private readonly NotificationDispatcher _dispatcher;

    private readonly Label _statusLabel = new();
    private readonly Label _nextRebootLabel = new();
    private readonly TextBox _log = new();
    private readonly Button _startBtn = new();
    private readonly Button _stopBtn = new();
    private readonly Button _restartNowBtn = new();
    private readonly Button _updateBtn = new();
    private readonly TextBox _testMessageBox = new();
    private readonly System.Windows.Forms.Timer _uiTimer = new();

    public DashboardTab(AppSettings settings, ServerProcessManager server, RebootScheduler scheduler, NotificationDispatcher dispatcher)
    {
        _settings = settings;
        _server = server;
        _scheduler = scheduler;
        _dispatcher = dispatcher;

        Dock = DockStyle.Fill;
        BuildUi();

        _server.OutputReceived += line => AppendLog(line);
        _server.ProcessExited += code => AppendLog($"[manager] Server process exited (code {code}).");
        _scheduler.Log += line => AppendLog(line);

        _uiTimer.Interval = 1000;
        _uiTimer.Tick += (_, _) => RefreshStatus();
        _uiTimer.Start();

        RefreshStatus();
    }

    private void BuildUi()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8) };

        _statusLabel.AutoSize = true;
        _statusLabel.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
        _statusLabel.Margin = new Padding(4, 8, 20, 4);

        _startBtn.Text = "Start Server";
        _startBtn.Width = 110;
        _startBtn.Click += (_, _) => StartServer();

        _stopBtn.Text = "Stop Server";
        _stopBtn.Width = 110;
        _stopBtn.Click += async (_, _) => await StopServerAsync();

        _restartNowBtn.Text = "Restart Now";
        _restartNowBtn.Width = 110;
        _restartNowBtn.Click += async (_, _) => await _scheduler.PerformRebootAsync();

        top.Controls.Add(_statusLabel);
        top.SetFlowBreak(_statusLabel, true);
        top.Controls.Add(_startBtn);
        top.Controls.Add(_stopBtn);
        _updateBtn.Text = "Update Server";
        _updateBtn.Width = 110;
        _updateBtn.Click += async (_, _) => await UpdateServerAsync();

        top.Controls.Add(_restartNowBtn);
        top.Controls.Add(_updateBtn);

        _nextRebootLabel.AutoSize = true;
        _nextRebootLabel.Margin = new Padding(4, 10, 4, 4);
        top.Controls.Add(_nextRebootLabel);

        var testPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 0, 8, 4) };
        _testMessageBox.Width = 400;
        _testMessageBox.Text = "This is a test announcement from Bob Server Manager.";
        var testBtn = new Button { Text = "Send Test Message", Width = 150 };
        testBtn.Click += async (_, _) => await _dispatcher.SendAsync(_settings.Notifications, _testMessageBox.Text);
        testPanel.Controls.Add(new Label { Text = "Test message:", AutoSize = true, Margin = new Padding(4, 8, 4, 4) });
        testPanel.Controls.Add(_testMessageBox);
        testPanel.Controls.Add(testBtn);

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Dock = DockStyle.Fill;
        _log.Font = new Font(FontFamily.GenericMonospace, 9);
        _log.WordWrap = false;

        Controls.Add(_log);
        Controls.Add(testPanel);
        Controls.Add(top);
    }

    private void StartServer()
    {
        try
        {
            var s = _settings.Startup;
            if (string.IsNullOrWhiteSpace(s.ExePath) || !File.Exists(s.ExePath))
            {
                MessageBox.Show("Set a valid server executable path on the Startup tab first.", "Bob Server Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _server.Start(s.ExePath, s.BuildArguments());
        }
        catch (Exception ex)
        {
            AppendLog("[manager] Failed to start: " + ex.Message);
        }
    }

    private async Task StopServerAsync()
    {
        await _server.StopGracefullyAsync(_settings.Reboot.ShutdownCommand, _settings.Reboot.ShutdownTimeoutSeconds);
    }

    private async Task UpdateServerAsync()
    {
        var u = _settings.Update;
        if (string.IsNullOrWhiteSpace(u.SteamCmdPath) || !File.Exists(u.SteamCmdPath))
        {
            MessageBox.Show("Set the SteamCMD path on the Server Updates tab first.", "Bob Server Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_server.IsRunning)
        {
            var result = MessageBox.Show(
                "The server is running. It will be stopped, updated through SteamCMD, and started again.\n\n" +
                "Players will be disconnected. Continue?",
                "Bob Server Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
        }

        await _scheduler.PerformUpdateAsync();
    }

    private void RefreshStatus()
    {
        _statusLabel.Text = _server.IsRunning
            ? $"● RUNNING (PID {_server.ProcessId}, since {_server.StartedAtUtc?.ToLocalTime():t})"
            : "● STOPPED";
        _statusLabel.ForeColor = _server.IsRunning ? Color.ForestGreen : Color.Firebrick;

        var busy = _scheduler.IsBusy;
        _stopBtn.Enabled = _server.IsRunning && !busy;
        _startBtn.Enabled = !_server.IsRunning && !busy;
        _restartNowBtn.Enabled = !busy;
        _updateBtn.Enabled = !busy;

        if (_settings.Reboot.Enabled && _scheduler.NextRebootUtc is { } next)
        {
            var remaining = next - DateTime.UtcNow;
            _nextRebootLabel.Text = remaining > TimeSpan.Zero
                ? $"Next scheduled reboot: {next.ToLocalTime():g}  (in {FormatSpan(remaining)})"
                : "Reboot due now...";
        }
        else
        {
            _nextRebootLabel.Text = "Automatic reboot is disabled.";
        }
    }

    private static string FormatSpan(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";

    private void AppendLog(string line)
    {
        if (InvokeRequired) { BeginInvoke(() => AppendLog(line)); return; }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}\r\n");

        try { File.AppendAllText(SettingsStore.LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}\r\n"); }
        catch { /* best-effort logging only */ }
    }
}
