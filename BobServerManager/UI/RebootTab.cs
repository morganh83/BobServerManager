using BobServerManager.Models;

namespace BobServerManager.UI;

public class RebootTab : UserControl
{
    private readonly AppSettings _settings;
    private readonly Action _onSaved;
    private readonly Action _onScheduleChanged;

    private readonly CheckBox _enabled = new() { Text = "Automatically reboot the server" };
    private readonly RadioButton _modeInterval = new() { Text = "Every" };
    private readonly RadioButton _modeDaily = new() { Text = "Every day at these times (local time):", AutoSize = true };
    private readonly NumericUpDown _intervalHours = new() { Minimum = 1, Maximum = 168, DecimalPlaces = 1, Increment = 1, Width = 80 };
    private readonly DateTimePicker _dailyTimePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 70 };
    private readonly Button _addTimeBtn = new() { Text = "Add", Width = 70 };
    private readonly Button _removeTimeBtn = new() { Text = "Remove", Width = 70 };
    private readonly ListBox _dailyTimes = new() { Width = 120, Height = 80 };
    private readonly FlowLayoutPanel _dailyPanel = new() { AutoSize = true, WrapContents = false, Margin = new Padding(20, 0, 0, 0) };

    private readonly NumericUpDown _leadMinutes = new() { Minimum = 1, Maximum = 180, Width = 80 };
    private readonly TextBox _extraWarnings = new() { Width = 200 };
    private readonly TextBox _messageTemplate = new() { Multiline = true, Height = 60, Width = 460 };

    private readonly CheckBox _forceSave = new() { Text = "Run ForceServerSave before stopping" };
    private readonly TextBox _shutdownCommand = new() { Width = 150 };
    private readonly NumericUpDown _shutdownTimeout = new() { Minimum = 5, Maximum = 600, Width = 80 };

    private readonly CheckBox _notifyConsole = new() { Text = "In-game console announce (via server console/stdin)" };
    private readonly TextBox _announceTemplate = new() { Width = 260 };
    private readonly CheckBox _notifyDiscord = new() { Text = "Discord webhook" };
    private readonly TextBox _discordUrl = new() { Width = 400 };
    private readonly CheckBox _notifyRcon = new() { Text = "RCON (experimental - BoB's built-in RCON is Beta and may be output-only)" };
    private readonly TextBox _rconHost = new() { Width = 120 };
    private readonly NumericUpDown _rconPort = new() { Minimum = 1, Maximum = 65535, Width = 80 };
    private readonly TextBox _rconPassword = new() { Width = 150, UseSystemPasswordChar = true };

    public RebootTab(AppSettings settings, Action onSaved, Action onScheduleChanged)
    {
        _settings = settings;
        _onSaved = onSaved;
        _onScheduleChanged = onScheduleChanged;
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BuildUi();
        LoadFromSettings();
    }

    private void BuildUi()
    {
        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12), WrapContents = false };

        var scheduleGroup = new GroupBox { Text = "Reboot schedule", AutoSize = true, Padding = new Padding(10), Width = 700 };
        var schedLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        schedLayout.Controls.Add(_enabled);

        var intervalRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        intervalRow.Controls.Add(_modeInterval);
        intervalRow.Controls.Add(_intervalHours);
        intervalRow.Controls.Add(new Label { Text = "hours (default 24)", AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
        schedLayout.Controls.Add(intervalRow);

        var dailyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        dailyRow.Controls.Add(_modeDaily);
        schedLayout.Controls.Add(dailyRow);

        _dailyTimePicker.Value = DateTime.Today.AddHours(4);
        _addTimeBtn.Click += (_, _) => AddDailyTime(_dailyTimePicker.Value.ToString("HH:mm"));
        _removeTimeBtn.Click += (_, _) => { if (_dailyTimes.SelectedIndex >= 0) _dailyTimes.Items.RemoveAt(_dailyTimes.SelectedIndex); };
        var pickerCol = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        pickerCol.Controls.Add(_dailyTimePicker);
        pickerCol.Controls.Add(_addTimeBtn);
        pickerCol.Controls.Add(_removeTimeBtn);
        _dailyPanel.Controls.Add(pickerCol);
        _dailyPanel.Controls.Add(_dailyTimes);
        schedLayout.Controls.Add(_dailyPanel);

        scheduleGroup.Controls.Add(schedLayout);
        root.Controls.Add(scheduleGroup);

        var warnGroup = new GroupBox { Text = "Reboot warning", AutoSize = true, Padding = new Padding(10), Width = 700 };
        var warnLayout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        void AddWarnRow(string label, Control c)
        {
            int r = warnLayout.RowCount++;
            warnLayout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, 0, r);
            warnLayout.Controls.Add(c, 1, r);
        }
        AddWarnRow("Warn this many minutes before reboot:", _leadMinutes);
        AddWarnRow("Extra warning checkpoints (minutes, comma-separated, optional):", _extraWarnings);
        AddWarnRow("Message sent to the server (placeholders: {minutes}, {time}):", _messageTemplate);
        warnGroup.Controls.Add(warnLayout);
        root.Controls.Add(warnGroup);

        var shutdownGroup = new GroupBox { Text = "Shutdown behavior", AutoSize = true, Padding = new Padding(10), Width = 700 };
        var shutdownLayout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        void AddShutdownRow(string label, Control c)
        {
            int r = shutdownLayout.RowCount++;
            shutdownLayout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, 0, r);
            shutdownLayout.Controls.Add(c, 1, r);
        }
        shutdownLayout.Controls.Add(_forceSave, 0, shutdownLayout.RowCount++);
        shutdownLayout.SetColumnSpan(_forceSave, 2);
        AddShutdownRow("Graceful shutdown console command:", _shutdownCommand);
        AddShutdownRow("Force-kill after (seconds) if it hasn't exited:", _shutdownTimeout);
        shutdownGroup.Controls.Add(shutdownLayout);
        root.Controls.Add(shutdownGroup);

        var notifyGroup = new GroupBox { Text = "Notification channels (used for the reboot warning and the manual test message)", AutoSize = true, Padding = new Padding(10), Width = 700 };
        var notifyLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };

        notifyLayout.Controls.Add(_notifyConsole);
        var announceRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(20, 0, 0, 6) };
        announceRow.Controls.Add(new Label { Text = "Command template:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        announceRow.Controls.Add(_announceTemplate);
        notifyLayout.Controls.Add(announceRow);

        notifyLayout.Controls.Add(_notifyDiscord);
        var discordRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(20, 0, 0, 6) };
        discordRow.Controls.Add(new Label { Text = "Webhook URL:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        discordRow.Controls.Add(_discordUrl);
        notifyLayout.Controls.Add(discordRow);

        notifyLayout.Controls.Add(_notifyRcon);
        var rconRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(20, 0, 0, 0) };
        rconRow.Controls.Add(new Label { Text = "Host:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        rconRow.Controls.Add(_rconHost);
        rconRow.Controls.Add(new Label { Text = "Port:", AutoSize = true, Margin = new Padding(8, 6, 4, 0) });
        rconRow.Controls.Add(_rconPort);
        rconRow.Controls.Add(new Label { Text = "Password:", AutoSize = true, Margin = new Padding(8, 6, 4, 0) });
        rconRow.Controls.Add(_rconPassword);
        notifyLayout.Controls.Add(rconRow);

        notifyGroup.Controls.Add(notifyLayout);
        root.Controls.Add(notifyGroup);

        var saveBtn = new Button { Text = "Save Reboot Settings", Width = 180, Height = 30 };
        saveBtn.Click += (_, _) => SaveToSettings();
        root.Controls.Add(saveBtn);

        Controls.Add(root);

        // The two radio buttons live in different panels, so WinForms won't group them automatically.
        _modeInterval.CheckedChanged += (_, _) =>
        {
            _intervalHours.Enabled = _modeInterval.Checked;
            if (_modeInterval.Checked) _modeDaily.Checked = false;
        };
        _modeDaily.CheckedChanged += (_, _) =>
        {
            _dailyPanel.Enabled = _modeDaily.Checked;
            if (_modeDaily.Checked) _modeInterval.Checked = false;
        };
    }

    /// <summary>Adds a normalized "HH:mm" entry, keeping the list sorted and free of duplicates.</summary>
    private void AddDailyTime(string entry)
    {
        if (!TimeSpan.TryParse(entry, out var tod) || tod < TimeSpan.Zero || tod >= TimeSpan.FromDays(1)) return;
        var normalized = $"{tod.Hours:00}:{tod.Minutes:00}";
        if (_dailyTimes.Items.Contains(normalized)) return;

        var sorted = _dailyTimes.Items.Cast<string>().Append(normalized).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        _dailyTimes.Items.Clear();
        _dailyTimes.Items.AddRange(sorted);
    }

    private void LoadFromSettings()
    {
        var r = _settings.Reboot;
        var n = _settings.Notifications;

        _enabled.Checked = r.Enabled;
        _modeInterval.Checked = r.Mode == RebootMode.Interval;
        _modeDaily.Checked = r.Mode == RebootMode.DailyTimes;
        _intervalHours.Value = (decimal)Math.Clamp(r.IntervalHours, (double)_intervalHours.Minimum, (double)_intervalHours.Maximum);
        _dailyTimes.Items.Clear();
        foreach (var t in r.DailyTimes) AddDailyTime(t);
        _dailyPanel.Enabled = _modeDaily.Checked;
        _intervalHours.Enabled = _modeInterval.Checked;

        _leadMinutes.Value = Math.Clamp(r.WarningLeadMinutes, (int)_leadMinutes.Minimum, (int)_leadMinutes.Maximum);
        _extraWarnings.Text = string.Join(", ", r.ExtraWarningMinutes);
        _messageTemplate.Text = r.WarningMessageTemplate;

        _forceSave.Checked = r.ForceSaveBeforeRestart;
        _shutdownCommand.Text = r.ShutdownCommand;
        _shutdownTimeout.Value = Math.Clamp(r.ShutdownTimeoutSeconds, (int)_shutdownTimeout.Minimum, (int)_shutdownTimeout.Maximum);

        _notifyConsole.Checked = n.UseConsoleAnnounce;
        _announceTemplate.Text = n.AnnounceCommandTemplate;
        _notifyDiscord.Checked = n.UseDiscordWebhook;
        _discordUrl.Text = n.DiscordWebhookUrl;
        _notifyRcon.Checked = n.UseRcon;
        _rconHost.Text = n.RconHost;
        _rconPort.Value = Math.Clamp(n.RconPort, (int)_rconPort.Minimum, (int)_rconPort.Maximum);
        _rconPassword.Text = n.RconPassword;
    }

    private void SaveToSettings()
    {
        if (_modeDaily.Checked && _dailyTimes.Items.Count == 0)
        {
            MessageBox.Show("Add at least one daily reboot time.", "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var r = _settings.Reboot;
        var n = _settings.Notifications;

        r.Enabled = _enabled.Checked;
        r.Mode = _modeDaily.Checked ? RebootMode.DailyTimes : RebootMode.Interval;
        r.IntervalHours = (double)_intervalHours.Value;
        r.DailyTimes = _dailyTimes.Items.Cast<string>().ToList();

        r.WarningLeadMinutes = (int)_leadMinutes.Value;
        r.ExtraWarningMinutes = _extraWarnings.Text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var v) ? v : -1)
            .Where(v => v > 0)
            .ToList();
        r.WarningMessageTemplate = _messageTemplate.Text;

        r.ForceSaveBeforeRestart = _forceSave.Checked;
        r.ShutdownCommand = string.IsNullOrWhiteSpace(_shutdownCommand.Text) ? "Exit" : _shutdownCommand.Text.Trim();
        r.ShutdownTimeoutSeconds = (int)_shutdownTimeout.Value;

        n.UseConsoleAnnounce = _notifyConsole.Checked;
        n.AnnounceCommandTemplate = string.IsNullOrWhiteSpace(_announceTemplate.Text) ? "Announce {message}" : _announceTemplate.Text;
        n.UseDiscordWebhook = _notifyDiscord.Checked;
        n.DiscordWebhookUrl = _discordUrl.Text.Trim();
        n.UseRcon = _notifyRcon.Checked;
        n.RconHost = _rconHost.Text.Trim();
        n.RconPort = (int)_rconPort.Value;
        n.RconPassword = _rconPassword.Text;

        _onSaved();
        _onScheduleChanged();
        MessageBox.Show("Reboot settings saved.", "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
