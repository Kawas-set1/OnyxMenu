using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Onyx;

internal static class OnyxNameHistory
{
    private sealed class Rec
    {
        internal string Fc;
        internal string Nick;
        internal List<string> Nicks;
        internal string First;
        internal string Last;
    }

    private static readonly Dictionary<string, Rec> Cache = new Dictionary<string, Rec>(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    private static string HistoryTxt => Path.Combine(BepInEx.Paths.GameRootPath, "Onyx", "PlayerHistory.txt");

    internal static string RecordJoin(PlayerControl p)
    {
        if (p == null || p.Data == null) return null;
        string nick = OnyxNameColor.Strip(SafeName(p)).Trim();
        if (nick.Length == 0 || nick == "???" || nick == "Игрок") return null;
        string fc = SafeFc(p);
        if (fc.Length == 0) return null;

        EnsureLoaded();
        string key = fc.ToLowerInvariant();
        string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        if (Cache.TryGetValue(key, out Rec r))
        {
            if (string.Equals(r.Nick, nick, StringComparison.Ordinal)) return null;
            string prev = r.Nick;
            if (!r.Nicks.Contains(nick)) r.Nicks.Insert(0, nick);
            r.Nick = nick;
            r.Last = now;
            Save();
            return prev;
        }

        Cache[key] = new Rec { Fc = fc, Nick = nick, Nicks = new List<string> { nick }, First = now, Last = now };
        Save();
        return null;
    }

    internal static int KnownNickCount(PlayerControl p)
    {
        string fc = SafeFc(p);
        if (fc.Length == 0) return 0;
        EnsureLoaded();
        return Cache.TryGetValue(fc.ToLowerInvariant(), out Rec r) ? r.Nicks.Count : 0;
    }

    internal static string CurrentNick(PlayerControl p) => OnyxNameColor.Strip(SafeName(p)).Trim();

    private static string SafeName(PlayerControl p)
    {
        try { return p.Data.PlayerName ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string SafeFc(PlayerControl p)
    {
        try { return p != null && p.Data != null ? (p.Data.FriendCode ?? string.Empty).Trim() : string.Empty; }
        catch { return string.Empty; }
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Load();
    }

    private static void Load()
    {
        Cache.Clear();
        try
        {
            if (!File.Exists(HistoryTxt)) return;
            foreach (string raw in File.ReadAllLines(HistoryTxt, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('|');
                if (p.Length < 5) continue;
                string fc = p[0].Trim();
                if (fc.Length == 0) continue;

                var nicks = new List<string>();
                foreach (string part in p[2].Split(';'))
                {
                    string n = part.Trim();
                    if (n.Length > 0) nicks.Add(n);
                }
                if (nicks.Count == 0 && p[1].Trim().Length > 0) nicks.Add(p[1].Trim());

                Cache[fc.ToLowerInvariant()] = new Rec
                {
                    Fc = fc,
                    Nick = p[1].Trim(),
                    Nicks = nicks,
                    First = p[3].Trim(),
                    Last = p[4].Trim(),
                };
            }
        }
        catch { }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryTxt));
            var sb = new StringBuilder();
            sb.Append("# Onyx — FriendCode | ник | история через ; | первый вход | последний\n");
            foreach (Rec r in Cache.Values)
                sb.Append(r.Fc).Append('|').Append(r.Nick).Append('|').Append(string.Join(";", r.Nicks)).Append('|').Append(r.First).Append('|').Append(r.Last).Append('\n');
            File.WriteAllText(HistoryTxt, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }
}

public sealed class OnyxHistoryTracker : MonoBehaviour
{
    private float _next;

    public void Update()
    {
        if (OnyxConfig.NameHistory == null || !OnyxConfig.NameHistory.Value) return;
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + 0.6f;
        if (PlayerControl.AllPlayerControls == null) return;

        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p == null) continue;
                string prev = OnyxNameHistory.RecordJoin(p);
                if (prev != null) OnyxToast.Push("Смена ника", $"{prev} → {OnyxNameHistory.CurrentNick(p)}", 4f, OnyxNotifyKind.Info);
            }
        }
        catch { }
    }
}
