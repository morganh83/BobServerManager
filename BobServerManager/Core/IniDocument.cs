using System.Text;

namespace BobServerManager.Core;

/// <summary>
/// A structure-preserving INI reader/writer for Unreal Engine style config files (Game.ini).
/// Unlike a strict key/value dictionary, UE ini files routinely repeat the same key many times
/// inside one section to represent arrays (e.g. "ServerAdmins=(...)" once per admin), and use
/// "!Key=ClearArray" markers plus "+Key=Value" append markers. A naive last-value-wins parser
/// would silently destroy that. This class keeps every line exactly as written, grouped by
/// section, and only mutates the specific lines a caller asks for - anything it doesn't know
/// about (comments, arrays, blank lines) survives a load/edit/save round trip untouched.
/// </summary>
public class IniDocument
{
    public class Section
    {
        public string Name { get; }
        public List<string> Lines { get; } = new();
        public Section(string name) => Name = name;
    }

    // Lines before the first [Section] header, if any (rare in Game.ini, but preserved anyway).
    public List<string> Preamble { get; } = new();
    public List<Section> Sections { get; } = new();

    public static IniDocument Load(string path)
    {
        var doc = new IniDocument();
        if (!File.Exists(path))
            return doc;

        Section? current = null;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var trimmed = rawLine.TrimStart();
            if (trimmed.StartsWith('[') && trimmed.Contains(']'))
            {
                var name = trimmed[1..trimmed.IndexOf(']')];
                current = doc.Sections.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (current is null)
                {
                    current = new Section(name);
                    doc.Sections.Add(current);
                }
                continue;
            }

            if (current is null)
                doc.Preamble.Add(rawLine);
            else
                current.Lines.Add(rawLine);
        }

        return doc;
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        foreach (var line in Preamble)
            sb.Append(line).Append("\r\n");

        foreach (var section in Sections)
        {
            sb.Append('[').Append(section.Name).Append(']').Append("\r\n");
            foreach (var line in section.Lines)
                sb.Append(line).Append("\r\n");
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    public Section GetOrCreateSection(string name)
    {
        var s = Sections.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (s is null)
        {
            s = new Section(name);
            Sections.Add(s);
        }
        return s;
    }

    /// <summary>Reads the value of the first "Key=..." line in a section (unquoted). Null if absent.</summary>
    public string? GetValue(string section, string key)
    {
        var s = Sections.FirstOrDefault(x => x.Name.Equals(section, StringComparison.OrdinalIgnoreCase));
        if (s is null) return null;

        foreach (var line in s.Lines)
        {
            var t = line.Trim();
            if (t.Length <= key.Length + 1) continue;
            if (t.StartsWith(key, StringComparison.OrdinalIgnoreCase) && t[key.Length] == '=')
            {
                var value = t[(key.Length + 1)..].Trim();
                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                    value = value[1..^1];
                return value;
            }
        }
        return null;
    }

    public bool GetBool(string section, string key, bool fallback)
    {
        var v = GetValue(section, key);
        if (v is null) return fallback;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public int GetInt(string section, string key, int fallback)
    {
        var v = GetValue(section, key);
        if (v is null) return fallback;
        var digits = new string(v.TakeWhile(c => char.IsDigit(c) || c == '-').ToArray());
        return int.TryParse(digits, out var n) ? n : fallback;
    }

    public float GetFloat(string section, string key, float fallback)
    {
        var v = GetValue(section, key);
        if (v is null) return fallback;
        v = v.TrimEnd('f', 'F');
        return float.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    /// <summary>Sets ("Key=Value") - replaces the first existing line for that key, or appends one.
    /// Pass <paramref name="quoted"/> for string-typed UE properties, which are wrapped in quotes.</summary>
    public void SetValue(string section, string key, string value, bool quoted = false)
    {
        var s = GetOrCreateSection(section);
        var formatted = quoted ? $"{key}=\"{value}\"" : $"{key}={value}";

        for (int i = 0; i < s.Lines.Count; i++)
        {
            var t = s.Lines[i].Trim();
            if (t.Length > key.Length && t.StartsWith(key, StringComparison.OrdinalIgnoreCase) && t[key.Length] == '=')
            {
                s.Lines[i] = formatted;
                return;
            }
        }
        s.Lines.Add(formatted);
    }

    public void SetBool(string section, string key, bool value) => SetValue(section, key, value ? "true" : "false");
    public void SetInt(string section, string key, int value) => SetValue(section, key, value.ToString());
    public void SetFloat(string section, string key, float value) =>
        SetValue(section, key, value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f");

    /// <summary>Removes every line in a section whose key (text before '=') exactly matches.
    /// Used to rewrite array-style blocks like ServerAdmins=(...) before appending a fresh set.</summary>
    public void RemoveAllWithKey(string section, string key)
    {
        var s = Sections.FirstOrDefault(x => x.Name.Equals(section, StringComparison.OrdinalIgnoreCase));
        if (s is null) return;
        s.Lines.RemoveAll(line =>
        {
            var t = line.Trim();
            var eq = t.IndexOf('=');
            if (eq < 0) return false;
            return t[..eq].Equals(key, StringComparison.OrdinalIgnoreCase);
        });
    }

    public void AppendLines(string section, IEnumerable<string> lines)
    {
        var s = GetOrCreateSection(section);
        s.Lines.AddRange(lines);
    }

    /// <summary>All raw lines for a key in a section, in file order (for reading array blocks back into a grid).</summary>
    public List<string> GetAllWithKey(string section, string key)
    {
        var result = new List<string>();
        var s = Sections.FirstOrDefault(x => x.Name.Equals(section, StringComparison.OrdinalIgnoreCase));
        if (s is null) return result;
        foreach (var line in s.Lines)
        {
            var t = line.Trim();
            var eq = t.IndexOf('=');
            if (eq < 0) continue;
            if (t[..eq].Equals(key, StringComparison.OrdinalIgnoreCase))
                result.Add(t[(eq + 1)..]);
        }
        return result;
    }
}
