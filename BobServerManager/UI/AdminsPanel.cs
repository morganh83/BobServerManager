using System.Text.RegularExpressions;
using BobServerManager.Core;

namespace BobServerManager.UI;

/// <summary>
/// Editor for the three admin-related array blocks in Game.ini: ServerAdmins / PlayerChatTags /
/// PlayerChatColors (merged into one "Staff" grid, exactly like the community config tool presents
/// them, since they're all keyed by the same SteamID64), AdminRanks, and AdminCommandRules. The
/// exact "(Key=Value,...)" formats are taken from the community generator's output.
/// </summary>
public class AdminsPanel : UserControl, IConfigSubTab
{
    private const string Main = "/Script/BeastsOfBermuda.ServerGameInstance";

    // Known admin command names, gathered from the wiki + the community config tool's permission list.
    // The combo is editable, so anything missing here can still be typed in directly.
    private static readonly string[] KnownCommands =
    {
        "Announce", "BanPlayer", "UnBanPlayer", "KickPlayer", "ServerMutePlayer", "RespawnPlayer",
        "ControlServer", "ControlWeather", "CheckServerData", "ModifyServerData", "FlushServerData",
        "ForceSaveData", "ClearBones", "ClearCarcasses", "EditFoliage", "GroupInvitesOnly", "Groups",
        "GrowEntity", "ModifyEggsAndNesting", "ModifyTalents", "PossessEntity", "RestartMap",
        "SpawnEntity", "Teleport", "UseObserver", "ServerConsoleCommand", "AddScentToArea",
        "RemoveAllScentNodeData"
    };

    private readonly DataGridView _staffGrid = new();
    private readonly DataGridView _ranksGrid = new();
    private readonly DataGridView _commandsGrid = new();

    public AdminsPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BuildUi();
    }

    private void BuildUi()
    {
        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(10) };

        ConfigureGrid(_staffGrid, 720, 220);
        _staffGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SteamID", HeaderText = "SteamID64", Width = 150 });
        _staffGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rank", HeaderText = "Admin Rank (blank = not an admin)", Width = 180 });
        _staffGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tag", HeaderText = "Chat Tag (optional)", Width = 150 });
        _staffGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Color", HeaderText = "Chat Color #RRGGBB (optional)", Width = 180 });
        root.Controls.Add(GroupOf("Server Admins / Chat Tags / Chat Colors (by SteamID64)", _staffGrid,
            HelpLabel("One row per player. Leave Rank blank for a player who only has a chat tag/color, not admin access.")));

        ConfigureGrid(_ranksGrid, 400, 150);
        _ranksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RankName", HeaderText = "Rank Name", Width = 200 });
        _ranksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RankLevel", HeaderText = "Level (higher = more power)", Width = 180 });
        root.Controls.Add(GroupOf("Admin Ranks", _ranksGrid));

        ConfigureGrid(_commandsGrid, 500, 220);
        var cmdCol = new DataGridViewComboBoxColumn { Name = "Command", HeaderText = "Command", Width = 220, Items = { }, FlatStyle = FlatStyle.Flat };
        cmdCol.Items.AddRange(KnownCommands);
        _commandsGrid.Columns.Add(cmdCol);
        _commandsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MinRank", HeaderText = "Minimal Rank Required", Width = 220 });
        root.Controls.Add(GroupOf("Admin Command Permissions", _commandsGrid,
            HelpLabel("MinimalRank must match one of the Rank Names above (or be blank to allow local server/host use only).")));

        Controls.Add(root);
    }

    private static void ConfigureGrid(DataGridView g, int width, int height)
    {
        g.Width = width;
        g.Height = height;
        g.AllowUserToAddRows = true;
        g.AllowUserToDeleteRows = true;
        g.RowHeadersVisible = false;
        g.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
    }

    public void RefreshFromDocument(IniDocument doc)
    {
        _staffGrid.Rows.Clear();
        var staff = new Dictionary<string, (string rank, string tag, string color)>();

        foreach (var raw in doc.GetAllWithKey(Main, "ServerAdmins"))
        {
            var m = Regex.Match(raw, @"UserSteamId64\s*=\s*(\d+).*?AdminRank\s*=\s*""([^""]*)""");
            if (!m.Success) continue;
            var id = m.Groups[1].Value;
            var entry = staff.TryGetValue(id, out var e) ? e : (rank: "", tag: "", color: "");
            staff[id] = (m.Groups[2].Value, entry.tag, entry.color);
        }
        foreach (var raw in doc.GetAllWithKey(Main, "PlayerChatTags"))
        {
            var m = Regex.Match(raw, @"SteamID\s*=\s*(\d+).*?ChatTag\s*=\s*""([^""]*)""");
            if (!m.Success) continue;
            var id = m.Groups[1].Value;
            var entry = staff.TryGetValue(id, out var e) ? e : (rank: "", tag: "", color: "");
            staff[id] = (entry.rank, m.Groups[2].Value, entry.color);
        }
        foreach (var raw in doc.GetAllWithKey(Main, "PlayerChatColors"))
        {
            var m = Regex.Match(raw, @"SteamID\s*=\s*(\d+).*?R\s*=\s*([\d.]+).*?G\s*=\s*([\d.]+).*?B\s*=\s*([\d.]+)");
            if (!m.Success) continue;
            var id = m.Groups[1].Value;
            var r = (int)Math.Round(double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
            var g = (int)Math.Round(double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
            var b = (int)Math.Round(double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture) * 255);
            var hex = $"#{r:X2}{g:X2}{b:X2}";
            var entry = staff.TryGetValue(id, out var e) ? e : (rank: "", tag: "", color: "");
            staff[id] = (entry.rank, entry.tag, hex);
        }
        foreach (var (id, v) in staff)
            _staffGrid.Rows.Add(id, v.rank, v.tag, v.color);

        _ranksGrid.Rows.Clear();
        foreach (var raw in doc.GetAllWithKey(Main, "AdminRanks"))
        {
            var m = Regex.Match(raw, @"AdminRank\s*=\s*""([^""]*)""\s*,\s*RankLevel\s*=\s*(-?\d+)");
            if (m.Success) _ranksGrid.Rows.Add(m.Groups[1].Value, m.Groups[2].Value);
        }

        _commandsGrid.Rows.Clear();
        foreach (var raw in doc.GetAllWithKey(Main, "AdminCommandRules"))
        {
            var m = Regex.Match(raw, @"CommandType\s*=\s*EAdminCommands::(\w+)\s*,\s*MinimalRank\s*=\s*""([^""]*)""");
            if (m.Success) _commandsGrid.Rows.Add(m.Groups[1].Value, m.Groups[2].Value);
        }
    }

    public void ApplyToDocument(IniDocument doc)
    {
        doc.RemoveAllWithKey(Main, "ServerAdmins");
        doc.RemoveAllWithKey(Main, "PlayerChatTags");
        doc.RemoveAllWithKey(Main, "PlayerChatColors");

        var adminLines = new List<string>();
        var tagLines = new List<string>();
        var colorLines = new List<string>();

        foreach (DataGridViewRow row in _staffGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var id = row.Cells["SteamID"].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(id)) continue;

            var rank = row.Cells["Rank"].Value?.ToString()?.Trim() ?? "";
            var tag = row.Cells["Tag"].Value?.ToString()?.Trim() ?? "";
            var color = row.Cells["Color"].Value?.ToString()?.Trim() ?? "";

            if (!string.IsNullOrEmpty(rank))
                adminLines.Add($"ServerAdmins=(UserSteamId64={id},AdminRank=\"{rank}\")");
            if (!string.IsNullOrEmpty(tag))
                tagLines.Add($"PlayerChatTags=(SteamID={id},ChatTag=\"{tag}\")");
            if (!string.IsNullOrEmpty(color) && color.Length == 7 && color[0] == '#'
                && !color.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    int r = Convert.ToInt32(color.Substring(1, 2), 16);
                    int g = Convert.ToInt32(color.Substring(3, 2), 16);
                    int b = Convert.ToInt32(color.Substring(5, 2), 16);
                    string F(int v) => (v / 255.0).ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture);
                    colorLines.Add($"PlayerChatColors=(SteamID={id},ChatColor=(R={F(r)},G={F(g)},B={F(b)},A=1.0))");
                }
                catch { /* malformed hex - skip rather than write garbage */ }
            }
        }

        if (adminLines.Count > 0) doc.AppendLines(Main, adminLines);
        if (tagLines.Count > 0) { doc.AppendLines(Main, new[] { "!PlayerChatTags=ClearArray" }.Concat(tagLines)); }
        if (colorLines.Count > 0) { doc.AppendLines(Main, new[] { "!PlayerChatColors=ClearArray" }.Concat(colorLines)); }

        doc.RemoveAllWithKey(Main, "AdminRanks");
        var rankLines = new List<string>();
        foreach (DataGridViewRow row in _ranksGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var name = row.Cells["RankName"].Value?.ToString()?.Trim();
            var level = row.Cells["RankLevel"].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(level)) continue;
            rankLines.Add($"AdminRanks=(AdminRank=\"{name}\",RankLevel={level})");
        }
        if (rankLines.Count > 0) doc.AppendLines(Main, rankLines);

        doc.RemoveAllWithKey(Main, "AdminCommandRules");
        var cmdLines = new List<string>();
        foreach (DataGridViewRow row in _commandsGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var cmd = row.Cells["Command"].Value?.ToString()?.Trim();
            var rank = row.Cells["MinRank"].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) continue;
            cmdLines.Add($"AdminCommandRules=(CommandType=EAdminCommands::{cmd},MinimalRank=\"{rank}\")");
        }
        if (cmdLines.Count > 0) doc.AppendLines(Main, cmdLines);
    }

    private static Label HelpLabel(string text) => new() { Text = text, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 2, 0, 8) };

    private static Control GroupOf(string title, params Control[] children)
    {
        var g = new GroupBox { Text = title, AutoSize = true, Padding = new Padding(10), Width = 760 };
        var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        stack.Controls.AddRange(children);
        g.Controls.Add(stack);
        return g;
    }
}
