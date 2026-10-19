using BobServerManager.Core;
using BobServerManager.Models;

namespace BobServerManager.UI;

public class UpdateTab : UserControl
{
    private readonly AppSettings _settings;
    private readonly Action _onSaved;

    private readonly TextBox _steamCmdPath = new() { Width = 400 };
    private readonly Button _downloadBtn = new() { Text = "Download SteamCMD", Width = 140 };
    private readonly TextBox _installDir = new() { Width = 400 };
    private readonly Label _resolvedDir = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _appId = new() { Width = 100 };
    private readonly TextBox _betaBranch = new() { Width = 150 };
    private readonly CheckBox _validate = new() { Text = "Validate files (slower, repairs missing/corrupt files)", AutoSize = true };
    private readonly CheckBox _updateOnReboot = new() { Text = "Update the server during every scheduled reboot", AutoSize = true };
    private readonly TextBox _updateMessage = new() { Width = 400 };
    private readonly TextBox _preview = new() { Multiline = true, ReadOnly = true, Height = 50, Width = 640, Font = new Font(FontFamily.GenericMonospace, 9) };

    public UpdateTab(AppSettings settings, Action onSaved)
    {
        _settings = settings;
        _onSaved = onSaved;
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BuildUi();
        LoadFromSettings();
        UpdatePreview();
    }

    private void BuildUi()
    {
        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12), WrapContents = false };

        var group = new GroupBox { Text = "SteamCMD", AutoSize = true, Padding = new Padding(10), Width = 700 };
        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        void AddRow(string label, Control c)
        {
            int r = layout.RowCount++;
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, 0, r);
            layout.Controls.Add(c, 1, r);
        }

        var browseSteam = new Button { Text = "Browse...", Width = 90 };
        browseSteam.Click += (_, _) => BrowseSteamCmd();
        _downloadBtn.Click += async (_, _) => await DownloadSteamCmdAsync();
        var steamRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        steamRow.Controls.Add(_steamCmdPath);
        steamRow.Controls.Add(browseSteam);
        steamRow.Controls.Add(_downloadBtn);
        AddRow("steamcmd.exe:", steamRow);

        var browseDir = new Button { Text = "Browse...", Width = 90 };
        browseDir.Click += (_, _) => BrowseInstallDir();
        var dirRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        dirRow.Controls.Add(_installDir);
        dirRow.Controls.Add(browseDir);
        AddRow("Install directory (blank = auto):", dirRow);
        AddRow("", _resolvedDir);

        AddRow("App ID:", _appId);
        AddRow("Beta branch (optional):", _betaBranch);
        layout.Controls.Add(_validate, 1, layout.RowCount++);
        layout.Controls.Add(_updateOnReboot, 1, layout.RowCount++);
        AddRow("Message sent before a manual update:", _updateMessage);

        group.Controls.Add(layout);
        root.Controls.Add(group);

        root.Controls.Add(new Label { Text = "SteamCMD command preview:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) });
        root.Controls.Add(_preview);
        root.Controls.Add(new Label
        {
            Text = "Use \"Update Server\" on the Dashboard to update now - the server is stopped while SteamCMD runs.",
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 6)
        });

        var saveBtn = new Button { Text = "Save Update Settings", Width = 180, Height = 30 };
        saveBtn.Click += (_, _) => SaveToSettings();
        root.Controls.Add(saveBtn);

        Controls.Add(root);

        foreach (var c in new Control[] { _steamCmdPath, _installDir, _appId, _betaBranch })
            c.TextChanged += (_, _) => UpdatePreview();
        _validate.CheckedChanged += (_, _) => UpdatePreview();
        // The auto-derived directory depends on the Startup tab's exe path, which may change while this tab is hidden.
        VisibleChanged += (_, _) => { if (Visible) UpdatePreview(); };
    }

    private UpdateSettings ReadForm() => new()
    {
        SteamCmdPath = _steamCmdPath.Text.Trim(),
        InstallDirOverride = _installDir.Text.Trim(),
        AppId = string.IsNullOrWhiteSpace(_appId.Text) ? UpdateSettings.DefaultAppId : _appId.Text.Trim(),
        BetaBranch = _betaBranch.Text.Trim(),
        Validate = _validate.Checked,
        UpdateOnScheduledReboot = _updateOnReboot.Checked,
        UpdateMessage = _updateMessage.Text,
    };

    private void UpdatePreview()
    {
        var u = ReadForm();
        var dir = SteamCmdUpdater.ResolveInstallDir(new AppSettings { Startup = _settings.Startup, Update = u });
        _resolvedDir.Text = dir is null
            ? "Set the server executable on the Startup Switches tab, or pick a directory above."
            : $"Will install into: {dir}";
        var exe = string.IsNullOrWhiteSpace(u.SteamCmdPath) ? "steamcmd.exe" : u.SteamCmdPath;
        _preview.Text = $"\"{exe}\" {SteamCmdUpdater.BuildArguments(u, dir ?? "<install dir>")}";
    }

    private void BrowseSteamCmd()
    {
        using var dlg = new OpenFileDialog { Filter = "steamcmd.exe|steamcmd.exe|Executable (*.exe)|*.exe", Title = "Locate steamcmd.exe" };
        if (dlg.ShowDialog() == DialogResult.OK) _steamCmdPath.Text = dlg.FileName;
    }

    private void BrowseInstallDir()
    {
        using var dlg = new FolderBrowserDialog { Description = "Beasts of Bermuda dedicated server install folder" };
        if (dlg.ShowDialog() == DialogResult.OK) _installDir.Text = dlg.SelectedPath;
    }

    private async Task DownloadSteamCmdAsync()
    {
        _downloadBtn.Enabled = false;
        _downloadBtn.Text = "Downloading...";
        try
        {
            _steamCmdPath.Text = await SteamCmdUpdater.DownloadSteamCmdAsync(SteamCmdUpdater.DefaultSteamCmdDir);
            SaveToSettings(showConfirmation: false);
            MessageBox.Show($"SteamCMD downloaded to:\n{_steamCmdPath.Text}\n\nIt will finish installing itself the first time an update runs.",
                "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to download SteamCMD: " + ex.Message, "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _downloadBtn.Enabled = true;
            _downloadBtn.Text = "Download SteamCMD";
        }
    }

    private void LoadFromSettings()
    {
        var u = _settings.Update;
        _steamCmdPath.Text = u.SteamCmdPath;
        _installDir.Text = u.InstallDirOverride;
        _appId.Text = u.AppId;
        _betaBranch.Text = u.BetaBranch;
        _validate.Checked = u.Validate;
        _updateOnReboot.Checked = u.UpdateOnScheduledReboot;
        _updateMessage.Text = u.UpdateMessage;
    }

    private void SaveToSettings(bool showConfirmation = true)
    {
        _settings.Update = ReadForm();
        _onSaved();
        if (showConfirmation)
            MessageBox.Show("Update settings saved.", "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
