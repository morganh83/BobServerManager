namespace BobServerManager.UI;

/// <summary>Direct, full-fidelity text view of the whole Game.ini file - the escape hatch for
/// anything the structured tabs don't model (world-event exclusion lists, mixed-herd factions,
/// disabled AI types, etc.). Load/Save on this tab talk straight to the file, bypassing the
/// structured IniDocument, so switch tabs and reload before mixing manual and structured edits.</summary>
public class RawIniPanel : UserControl
{
    private readonly TextBox _text = new();
    private readonly Label _pathLabel = new() { AutoSize = true };

    public RawIniPanel()
    {
        Dock = DockStyle.Fill;
        BuildUi();
    }

    private void BuildUi()
    {
        _text.Multiline = true;
        _text.ScrollBars = ScrollBars.Both;
        _text.Dock = DockStyle.Fill;
        _text.Font = new Font(FontFamily.GenericMonospace, 9);
        _text.AcceptsTab = true;
        _text.WordWrap = false;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(8, 6, 8, 0) };
        top.Controls.Add(_pathLabel);

        Controls.Add(_text);
        Controls.Add(top);
    }

    public void LoadFile(string path)
    {
        _pathLabel.Text = "Editing: " + path;
        _text.Text = File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public void SaveFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, _text.Text);
    }
}
