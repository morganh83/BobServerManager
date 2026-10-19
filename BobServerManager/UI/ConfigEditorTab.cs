using BobServerManager.Core;
using BobServerManager.Models;

namespace BobServerManager.UI;

/// <summary>
/// Container for the Game.ini editor: a path picker shared by all sub-tabs, plus Load/Save that
/// round-trip through <see cref="IniDocument"/> for the structured tabs. The Raw tab reads/writes
/// the file directly and independently - reload after switching between raw and structured edits.
/// </summary>
public class ConfigEditorTab : UserControl
{
    private readonly AppSettings _settings;
    private readonly Action _onSaved;

    private readonly TextBox _pathBox = new() { Width = 520 };
    private readonly TabControl _subTabs = new() { Dock = DockStyle.Fill };

    private readonly GeneralSettingsPanel _general = new();
    private readonly AdminsPanel _admins = new();
    private readonly CreaturesPanel _creatures = new();
    private readonly RawIniPanel _raw = new();

    private IniDocument _doc = new();

    public ConfigEditorTab(AppSettings settings, Action onSaved)
    {
        _settings = settings;
        _onSaved = onSaved;
        Dock = DockStyle.Fill;
        BuildUi();
        _pathBox.Text = ResolveIniPath();
        LoadAll();
    }

    private void BuildUi()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
        top.Controls.Add(new Label { Text = "Game.ini path:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        top.Controls.Add(_pathBox);

        var browseBtn = new Button { Text = "Browse...", Width = 90 };
        browseBtn.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Game.ini|Game.ini|INI files (*.ini)|*.ini", FileName = "Game.ini" };
            if (dlg.ShowDialog() == DialogResult.OK) _pathBox.Text = dlg.FileName;
        };
        var autoBtn = new Button { Text = "Auto-detect", Width = 90 };
        autoBtn.Click += (_, _) => _pathBox.Text = ResolveIniPath();
        var loadBtn = new Button { Text = "Reload", Width = 90 };
        loadBtn.Click += (_, _) => LoadAll();
        var saveBtn = new Button { Text = "Save All", Width = 90 };
        saveBtn.Click += (_, _) => SaveAll();

        top.Controls.Add(browseBtn);
        top.Controls.Add(autoBtn);
        top.Controls.Add(loadBtn);
        top.Controls.Add(saveBtn);

        _subTabs.TabPages.Add(new TabPage("General") { Controls = { _general } });
        _subTabs.TabPages.Add(new TabPage("Admins & Permissions") { Controls = { _admins } });
        _subTabs.TabPages.Add(new TabPage("Creature Limits") { Controls = { _creatures } });
        _subTabs.TabPages.Add(new TabPage("Raw INI") { Controls = { _raw } });

        Controls.Add(_subTabs);
        Controls.Add(top);
    }

    private string ResolveIniPath()
    {
        if (!string.IsNullOrWhiteSpace(_settings.GameIniPathOverride))
            return _settings.GameIniPathOverride;

        var exe = _settings.Startup.ExePath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            var exeDir = Path.GetDirectoryName(exe);
            if (!string.IsNullOrEmpty(exeDir))
                return Path.Combine(exeDir, "BeastsOfBermuda", "Saved", "Config", "WindowsServer", "Game.ini");
        }
        return "";
    }

    private void LoadAll()
    {
        var path = _pathBox.Text.Trim();
        _settings.GameIniPathOverride = path;

        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("Set the server executable on the Startup tab, or browse to Game.ini manually.",
                "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _doc = IniDocument.Load(path);
        _general.RefreshFromDocument(_doc);
        _admins.RefreshFromDocument(_doc);
        _creatures.RefreshFromDocument(_doc);
        _raw.LoadFile(path);

        if (!File.Exists(path))
            MessageBox.Show("That Game.ini doesn't exist yet - it will be created when you Save All.",
                "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveAll()
    {
        var path = _pathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("Set a Game.ini path first.", "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_subTabs.SelectedTab?.Text == "Raw INI")
        {
            // The raw tab edits the file directly and doesn't go through the shared IniDocument.
            _raw.SaveFile(path);
        }
        else
        {
            _general.ApplyToDocument(_doc);
            _admins.ApplyToDocument(_doc);
            _creatures.ApplyToDocument(_doc);
            _doc.Save(path);
            _raw.LoadFile(path); // keep the raw view in sync with what structured tabs just wrote
        }

        _settings.GameIniPathOverride = path;
        _onSaved();
        MessageBox.Show("Game.ini saved.", "Bob Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
