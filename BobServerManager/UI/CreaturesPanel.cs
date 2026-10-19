using System.Text.RegularExpressions;
using BobServerManager.Core;

namespace BobServerManager.UI;

/// <summary>
/// Editor for the per-species population/growth caps (Game.ini "CreatureLimits" / "GrowthLimits"
/// arrays). The game's full EDinoType enum isn't published anywhere this tool could verify, so the
/// species column is free text rather than a locked dropdown - existing entries load back exactly
/// as they were, and new rows just need the same in-game species name you'd see elsewhere in your
/// own Game.ini or the game's admin UI.
/// </summary>
public class CreaturesPanel : UserControl, IConfigSubTab
{
    private const string Main = "/Script/BeastsOfBermuda.ServerGameInstance";
    private readonly DataGridView _grid = new();

    public CreaturesPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BuildUi();
    }

    private void BuildUi()
    {
        _grid.Width = 760;
        _grid.Height = 380;
        _grid.AllowUserToAddRows = true;
        _grid.AllowUserToDeleteRows = true;
        _grid.RowHeadersVisible = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Species (EDinoType)", Width = 160 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Percent", HeaderText = "% Allowed", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "GroupLimit", HeaderText = "Group Limit", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrowthLimit", HeaderText = "Growth Limit", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SoftCapStart", HeaderText = "Soft Cap Start", Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SoftCapEnd", HeaderText = "Soft Cap End", Width = 100 });

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(10) };
        var g = new GroupBox { Text = "Creature Limits", AutoSize = true, Padding = new Padding(10), Width = 800 };
        var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        stack.Controls.Add(new Label
        {
            Text = "One row per species. Leave a field blank to omit it (the server will use its own default for that value).",
            AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 2, 0, 8)
        });
        stack.Controls.Add(_grid);
        g.Controls.Add(stack);
        root.Controls.Add(g);
        Controls.Add(root);
    }

    public void RefreshFromDocument(IniDocument doc)
    {
        _grid.Rows.Clear();
        var byType = new Dictionary<string, string[]>();

        foreach (var raw in doc.GetAllWithKey(Main, "CreatureLimits"))
        {
            var m = Regex.Match(raw,
                @"CreatureType\s*=\s*EDinoType::(\w+)\s*,\s*PercentAllowed\s*=\s*([\d.]+)f?\s*,\s*GroupLimit\s*=\s*(\d+)\s*,\s*SoftCapStart\s*=\s*([\d.]+)f?\s*,\s*SoftCapEnd\s*=\s*([\d.]+)f?");
            if (!m.Success) continue;
            var type = m.Groups[1].Value;
            var row = byType.TryGetValue(type, out var existing) ? existing : new string[4];
            row[0] = m.Groups[2].Value; row[1] = m.Groups[3].Value; row[2] = existing?[2] ?? ""; row[3] = m.Groups[4].Value + "|" + m.Groups[5].Value;
            byType[type] = row;
        }
        foreach (var raw in doc.GetAllWithKey(Main, "GrowthLimits"))
        {
            var m = Regex.Match(raw, @"Type\s*=\s*EDinoType::(\w+)\s*,\s*Limit\s*=\s*([\d.]+)f?");
            if (!m.Success) continue;
            var type = m.Groups[1].Value;
            var row = byType.TryGetValue(type, out var existing) ? existing : new string[4];
            row[2] = m.Groups[2].Value;
            byType[type] = row;
        }

        foreach (var (type, row) in byType)
        {
            var caps = (row[3] ?? "|").Split('|');
            _grid.Rows.Add(type, row[0], row[1], row[2], caps.Length > 0 ? caps[0] : "", caps.Length > 1 ? caps[1] : "");
        }
    }

    public void ApplyToDocument(IniDocument doc)
    {
        doc.RemoveAllWithKey(Main, "CreatureLimits");
        doc.RemoveAllWithKey(Main, "GrowthLimits");

        var limitLines = new List<string>();
        var growthLines = new List<string>();

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            var type = row.Cells["Type"].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(type)) continue;

            var percent = ParseOr(row.Cells["Percent"].Value, "100");
            var groupLimit = ParseOr(row.Cells["GroupLimit"].Value, "0");
            var growth = row.Cells["GrowthLimit"].Value?.ToString()?.Trim();
            var capStart = ParseOr(row.Cells["SoftCapStart"].Value, "0");
            var capEnd = ParseOr(row.Cells["SoftCapEnd"].Value, "0");

            limitLines.Add($"CreatureLimits=(CreatureType=EDinoType::{type},PercentAllowed={percent}f,GroupLimit={groupLimit},SoftCapStart={capStart}f,SoftCapEnd={capEnd}f)");
            if (!string.IsNullOrWhiteSpace(growth))
                growthLines.Add($"GrowthLimits=(Type=EDinoType::{type},Limit={growth}f)");
        }

        if (limitLines.Count > 0) doc.AppendLines(Main, new[] { "!CreatureLimits=ClearArray" }.Concat(limitLines));
        if (growthLines.Count > 0) doc.AppendLines(Main, new[] { "!GrowthLimits=ClearArray" }.Concat(growthLines));
    }

    private static string ParseOr(object? value, string fallback)
    {
        var s = value?.ToString()?.Trim();
        return string.IsNullOrEmpty(s) ? fallback : s;
    }
}
