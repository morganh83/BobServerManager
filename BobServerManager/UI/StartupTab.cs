using BobServerManager.Models;

namespace BobServerManager.UI;

public class StartupTab : UserControl
{
    private readonly AppSettings _settings;
    private readonly Action _onSaved;

    private readonly TextBox _exePath = new() { Width = 460 };
    private readonly ComboBox _map = new() { Width = 220 };
    private readonly ComboBox _gameMode = new() { Width = 220 };
    private readonly TextBox _sessionName = new() { Width = 220 };
    private readonly TextBox _password = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly CheckBox _showPassword = new() { Text = "Show" };
    private readonly NumericUpDown _numPlayers = new() { Minimum = 1, Maximum = 500, Width = 100 };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Width = 100 };
    private readonly NumericUpDown _queryPort = new() { Minimum = 1, Maximum = 65535, Width = 100 };
    private readonly TextBox _multiHome = new() { Width = 220 };
    private readonly CheckBox _useLog = new() { Text = "-log (recommended: writes a server log and enables the console window)" };
    private readonly TextBox _extraArgs = new() { Width = 460 };
    private readonly TextBox _preview = new() { Multiline = true, ReadOnly = true, Height = 60, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 9) };

    public StartupTab(AppSettings settings, Action onSaved)
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
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;
        void AddRow(string label, Control c1, Control? c2 = null)
        {
            layout.RowCount = row + 1;
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) }, 0, row);
            layout.Controls.Add(c1, 1, row);
            if (c2 != null) layout.Controls.Add(c2, 2, row);
            row++;
        }

        var browseBtn = new Button { Text = "Browse...", Width = 90 };
        browseBtn.Click += (_, _) => BrowseExe();
        var exeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        exeRow.Controls.Add(_exePath);
        exeRow.Controls.Add(browseBtn);
        AddRow("Server executable:", exeRow);

        _map.DropDownStyle = ComboBoxStyle.DropDown;
        _map.Items.AddRange(StartupSettings.KnownMaps);
        AddRow("Map (-MapName):", _map);

        _gameMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _gameMode.Items.AddRange(StartupSettings.KnownGameModes);
        AddRow("Game mode (-GameMode):", _gameMode);

        AddRow("Server name (-SessionName):", _sessionName);

        var pwRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        pwRow.Controls.Add(_password);
        pwRow.Controls.Add(_showPassword);
        _showPassword.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !_showPassword.Checked;
        AddRow("Password (-ServerPassword):", pwRow);

        AddRow("Max players (-NumPlayers):", _numPlayers);
        AddRow("Game port (?Port):", _port);
        AddRow("Query port (?QueryPort):", _queryPort);
        AddRow("MultiHome IP (optional):", _multiHome);
        AddRow("", _useLog);
        AddRow("Extra arguments:", _extraArgs);

        var saveBtn = new Button { Text = "Save Startup Settings", Width = 180, Height = 30 };
        saveBtn.Click += (_, _) => SaveToSettings();
        AddRow("", saveBtn);

        foreach (Control c in layout.Controls)
            if (c is TextBox or ComboBox or NumericUpDown)
                c.Margin = new Padding(0, 4, 0, 0);

        void HookPreview(Control c)
        {
            c.TextChanged += (_, _) => UpdatePreview();
        }
        HookPreview(_exePath); HookPreview(_map); HookPreview(_gameMode); HookPreview(_sessionName);
        HookPreview(_password); HookPreview(_multiHome); HookPreview(_extraArgs);
        _numPlayers.ValueChanged += (_, _) => UpdatePreview();
        _port.ValueChanged += (_, _) => UpdatePreview();
        _queryPort.ValueChanged += (_, _) => UpdatePreview();
        _useLog.CheckedChanged += (_, _) => UpdatePreview();

        var previewLabel = new Label { Text = "Command line preview:", Dock = DockStyle.Top, Padding = new Padding(12, 8, 0, 0), AutoSize = true };

        Controls.Add(_preview);
        Controls.Add(previewLabel);
        Controls.Add(layout);
    }

    private void BrowseExe()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "BeastsOfBermudaServer.exe|BeastsOfBermudaServer.exe|Executable (*.exe)|*.exe",
            Title = "Locate BeastsOfBermudaServer.exe"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _exePath.Text = dlg.FileName;
    }

    private void LoadFromSettings()
    {
        var s = _settings.Startup;
        _exePath.Text = s.ExePath;
        _map.Text = s.MapName;
        _gameMode.Text = s.GameMode;
        _sessionName.Text = s.SessionName;
        _password.Text = s.ServerPassword;
        _numPlayers.Value = Math.Clamp(s.NumPlayers, (int)_numPlayers.Minimum, (int)_numPlayers.Maximum);
        _port.Value = Math.Clamp(s.Port, (int)_port.Minimum, (int)_port.Maximum);
        _queryPort.Value = Math.Clamp(s.QueryPort, (int)_queryPort.Minimum, (int)_queryPort.Maximum);
        _multiHome.Text = s.MultiHome;
        _useLog.Checked = s.UseLog;
        _extraArgs.Text = s.ExtraArgs;
    }

    private void SaveToSettings()
    {
        var s = _settings.Startup;
        s.ExePath = _exePath.Text.Trim();
        s.MapName = string.IsNullOrWhiteSpace(_map.Text) ? StartupSettings.KnownMaps[0] : _map.Text.Trim();
        s.GameMode = string.IsNullOrWhiteSpace(_gameMode.Text) ? StartupSettings.KnownGameModes[0] : _gameMode.Text.Trim();
        s.SessionName = _sessionName.Text.Trim();
        s.ServerPassword = _password.Text;
        s.NumPlayers = (int)_numPlayers.Value;
        s.Port = (int)_port.Value;
        s.QueryPort = (int)_queryPort.Value;
        s.MultiHome = _multiHome.Text.Trim();
        s.UseLog = _useLog.Checked;
        s.ExtraArgs = _extraArgs.Text.Trim();

        _onSaved();
        MessageBox.Show("Startup settings saved.", "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdatePreview()
    {
        var temp = new StartupSettings
        {
            MapName = string.IsNullOrWhiteSpace(_map.Text) ? "Ancestral_Plains" : _map.Text,
            GameMode = string.IsNullOrWhiteSpace(_gameMode.Text) ? "Life_Cycle" : _gameMode.Text,
            SessionName = _sessionName.Text,
            ServerPassword = _password.Text,
            NumPlayers = (int)_numPlayers.Value,
            Port = (int)_port.Value,
            QueryPort = (int)_queryPort.Value,
            MultiHome = _multiHome.Text,
            UseLog = _useLog.Checked,
            ExtraArgs = _extraArgs.Text
        };
        var exe = string.IsNullOrWhiteSpace(_exePath.Text) ? "BeastsOfBermudaServer.exe" : _exePath.Text;
        _preview.Text = $"\"{exe}\" {temp.BuildArguments()}";
    }
}
