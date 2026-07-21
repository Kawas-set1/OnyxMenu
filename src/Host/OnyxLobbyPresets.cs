using System.Collections.Generic;
using System.Text;

namespace Onyx;

internal static class OnyxLobbyPresets
{
    private static readonly List<string> _names = new List<string>();
    private static readonly Dictionary<string, string> _data = new Dictionary<string, string>();
    private static string _raw;

    private static void Sync()
    {
        string cur = OnyxConfig.LobbyPresets != null ? OnyxConfig.LobbyPresets.Value : "";
        if (cur == _raw) return;
        _raw = cur;
        _names.Clear();
        _data.Clear();
        if (string.IsNullOrEmpty(cur)) return;
        foreach (string line in cur.Split('\n'))
        {
            if (string.IsNullOrEmpty(line)) continue;
            int bar = line.IndexOf('|');
            if (bar <= 0) continue;
            string name = line.Substring(0, bar);
            if (_data.ContainsKey(name)) continue;
            _names.Add(name);
            _data[name] = line.Substring(bar + 1);
        }
    }

    private static void Flush()
    {
        var sb = new StringBuilder();
        foreach (string n in _names)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(n).Append('|').Append(_data[n]);
        }
        _raw = sb.ToString();
        if (OnyxConfig.LobbyPresets != null) OnyxConfig.LobbyPresets.Value = _raw;
    }

    internal static List<string> Names()
    {
        Sync();
        return _names;
    }

    internal static string Save(string name)
    {
        Sync();
        name = Clean(name);
        if (name.Length == 0) return "Пустое имя";
        if (!OnyxLobbySettings.Ready()) return "Только хост в лобби";
        string payload = OnyxLobbySettings.Capture();
        if (!_data.ContainsKey(name)) _names.Add(name);
        _data[name] = payload;
        while (_names.Count > 12) { string drop = _names[0]; _names.RemoveAt(0); _data.Remove(drop); }
        Flush();
        return "Пресет «" + name + "» сохранён";
    }

    internal static string Apply(string name)
    {
        Sync();
        if (!_data.TryGetValue(name, out string payload)) return "Нет пресета";
        if (!OnyxLobbySettings.Ready()) return "Только хост в лобби";
        return OnyxLobbySettings.ApplyState(payload) ? "Применён «" + name + "»" : "Битый пресет";
    }

    internal static void Delete(string name)
    {
        Sync();
        if (!_data.ContainsKey(name)) return;
        _names.Remove(name);
        _data.Remove(name);
        Flush();
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("|", "").Replace("\n", "").Replace("\r", "").Trim();
        if (s.Length > 20) s = s.Substring(0, 20);
        return s;
    }
}
