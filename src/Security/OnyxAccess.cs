using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InnerNet;
using UnityEngine;

namespace Onyx;

internal sealed class AccessEntry
{
    internal string Name;
    internal string Code;
    internal string Puid;
}

internal static class OnyxAccess
{
    private static readonly List<AccessEntry> Bans = new List<AccessEntry>();
    private static readonly List<AccessEntry> Whites = new List<AccessEntry>();
    private static readonly List<string> NickBans = new List<string>();
    private static readonly Dictionary<int, float> ActedAt = new Dictionary<int, float>();
    private const float ActCooldown = 6f;
    private static bool _loaded;

    private static string OnyxDir => Path.Combine(BepInEx.Paths.GameRootPath, "Onyx");
    private static string BanTxt => Path.Combine(OnyxDir, "BanList.txt");
    private static string WhiteTxt => Path.Combine(OnyxDir, "WhiteList.txt");
    private static string NickTxt => Path.Combine(OnyxDir, "NickBanList.txt");

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Parse(OnyxConfig.BanList?.Value, Bans);
        Parse(OnyxConfig.WhiteList?.Value, Whites);
        ParseNicks(OnyxConfig.NickBanList?.Value, NickBans);
    }

    private static void ParseNicks(string csv, List<string> into)
    {
        into.Clear();
        if (string.IsNullOrWhiteSpace(csv)) return;
        foreach (string item in csv.Split(';'))
        {
            string n = item.Trim();
            if (n.Length > 0 && !HasNick(into, n)) into.Add(n);
        }
    }

    private static void Parse(string csv, List<AccessEntry> into)
    {
        into.Clear();
        if (string.IsNullOrWhiteSpace(csv)) return;
        foreach (string item in csv.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            string[] p = item.Split('|');
            string name = p.Length >= 1 ? p[0].Trim() : string.Empty;
            string code = p.Length >= 2 ? p[1].Trim() : string.Empty;
            string puid = p.Length >= 3 ? p[2].Trim() : string.Empty;
            if ((code.Length > 0 || puid.Length > 0) && !Has(into, code, puid))
                into.Add(new AccessEntry { Name = name, Code = code, Puid = puid });
        }
    }

    private static void Persist(BepInEx.Configuration.ConfigEntry<string> entry, List<AccessEntry> list, string txtPath)
    {
        if (entry != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(Esc(list[i].Name)).Append('|').Append(Esc(list[i].Code)).Append('|').Append(Esc(list[i].Puid));
            }
            entry.Value = sb.ToString();
        }

        WriteTxt(txtPath, list);
    }

    private static void WriteTxt(string path, List<AccessEntry> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb = new StringBuilder();
            sb.AppendLine("# Onyx — формат: Имя | FriendCode | PUID");
            for (int i = 0; i < list.Count; i++)
                sb.Append(list[i].Name).Append(" | ").Append(list[i].Code).Append(" | ").Append(list[i].Puid).Append('\n');
            File.WriteAllText(path, sb.ToString());
        }
        catch { }
    }

    private static string Esc(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(';', ' ').Replace('|', ' ').Trim();

    private static bool Has(List<AccessEntry> list, string code, string puid)
    {
        for (int i = 0; i < list.Count; i++)
        {
            AccessEntry e = list[i];
            if (!string.IsNullOrEmpty(code) && string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(puid) && string.Equals(e.Puid, puid, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool HasNick(List<string> list, string name)
    {
        string key = NickKey(name);
        if (key.Length == 0) return false;
        for (int i = 0; i < list.Count; i++)
            if (NickKey(list[i]) == key) return true;
        return false;
    }

    private static string NickKey(string name)
    {
        string s = OnyxNameColor.Strip(name);
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    internal static int BanCount { get { EnsureLoaded(); return Bans.Count; } }
    internal static int WhiteCount { get { EnsureLoaded(); return Whites.Count; } }
    internal static int NickBanCount { get { EnsureLoaded(); return NickBans.Count; } }
    internal static IReadOnlyList<AccessEntry> BanEntries { get { EnsureLoaded(); return Bans; } }
    internal static IReadOnlyList<AccessEntry> WhiteEntries { get { EnsureLoaded(); return Whites; } }
    internal static IReadOnlyList<string> NickBanEntries { get { EnsureLoaded(); return NickBans; } }

    internal static bool IsBanned(string fc, string puid) { EnsureLoaded(); return Has(Bans, fc, puid); }
    internal static bool IsWhite(string fc, string puid) { EnsureLoaded(); return Has(Whites, fc, puid); }
    internal static bool IsNickBanned(string name) { EnsureLoaded(); return HasNick(NickBans, name); }

    internal static void AddBan(string name, string fc, string puid)
    {
        EnsureLoaded();
        if ((string.IsNullOrWhiteSpace(fc) && string.IsNullOrWhiteSpace(puid)) || Has(Bans, fc, puid)) return;
        Bans.Add(new AccessEntry { Name = name ?? string.Empty, Code = (fc ?? string.Empty).Trim(), Puid = (puid ?? string.Empty).Trim() });
        Persist(OnyxConfig.BanList, Bans, BanTxt);
    }

    internal static void AddWhite(string name, string fc, string puid)
    {
        EnsureLoaded();
        if ((string.IsNullOrWhiteSpace(fc) && string.IsNullOrWhiteSpace(puid)) || Has(Whites, fc, puid)) return;
        Whites.Add(new AccessEntry { Name = name ?? string.Empty, Code = (fc ?? string.Empty).Trim(), Puid = (puid ?? string.Empty).Trim() });
        Persist(OnyxConfig.WhiteList, Whites, WhiteTxt);
    }

    internal static void RemoveBan(string key) { EnsureLoaded(); if (RemoveByKey(Bans, key)) Persist(OnyxConfig.BanList, Bans, BanTxt); }
    internal static void RemoveWhite(string key) { EnsureLoaded(); if (RemoveByKey(Whites, key)) Persist(OnyxConfig.WhiteList, Whites, WhiteTxt); }

    private static bool RemoveByKey(List<AccessEntry> list, string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        for (int i = list.Count - 1; i >= 0; i--)
            if (string.Equals(list[i].Code, key, StringComparison.OrdinalIgnoreCase) || string.Equals(list[i].Puid, key, StringComparison.OrdinalIgnoreCase))
            { list.RemoveAt(i); return true; }
        return false;
    }

    internal static void ClearBans() { EnsureLoaded(); Bans.Clear(); Persist(OnyxConfig.BanList, Bans, BanTxt); }
    internal static void ClearWhites() { EnsureLoaded(); Whites.Clear(); Persist(OnyxConfig.WhiteList, Whites, WhiteTxt); }

    internal static void AddNickBan(string name)
    {
        EnsureLoaded();
        string clean = OnyxNameColor.Strip(name).Trim();
        if (clean.Length == 0 || HasNick(NickBans, clean)) return;
        NickBans.Add(clean);
        PersistNicks();
    }

    internal static void RemoveNickBan(string name)
    {
        EnsureLoaded();
        string key = NickKey(name);
        for (int i = NickBans.Count - 1; i >= 0; i--)
            if (NickKey(NickBans[i]) == key) { NickBans.RemoveAt(i); PersistNicks(); return; }
    }

    internal static void ClearNickBans() { EnsureLoaded(); NickBans.Clear(); PersistNicks(); }

    private static void PersistNicks()
    {
        if (OnyxConfig.NickBanList != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < NickBans.Count; i++) { if (i > 0) sb.Append(';'); sb.Append(Esc(NickBans[i])); }
            OnyxConfig.NickBanList.Value = sb.ToString();
        }

        try
        {
            Directory.CreateDirectory(OnyxDir);
            var sb2 = new StringBuilder();
            sb2.AppendLine("# Onyx — по одному нику в строке");
            for (int i = 0; i < NickBans.Count; i++) sb2.Append(NickBans[i]).Append('\n');
            File.WriteAllText(NickTxt, sb2.ToString());
        }
        catch { }
    }

    internal static void ImportTxt()
    {
        EnsureLoaded();
        int b = ImportFile(BanTxt, Bans);
        int w = ImportFile(WhiteTxt, Whites);
        Persist(OnyxConfig.BanList, Bans, BanTxt);
        Persist(OnyxConfig.WhiteList, Whites, WhiteTxt);
        OnyxToast.Push("Импорт TXT", $"Бан: {b} · Вайт: {w}", 2.5f, OnyxNotifyKind.Success);
    }

    private static int ImportFile(string path, List<AccessEntry> list)
    {
        try
        {
            if (!File.Exists(path)) return list.Count;
            list.Clear();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('|');
                string name, fc, puid;
                if (p.Length == 1) { name = string.Empty; fc = p[0].Trim(); puid = string.Empty; }
                else { name = p[0].Trim(); fc = p.Length >= 2 ? p[1].Trim() : string.Empty; puid = p.Length >= 3 ? p[2].Trim() : string.Empty; }
                if ((fc.Length > 0 || puid.Length > 0) && !Has(list, fc, puid))
                    list.Add(new AccessEntry { Name = name, Code = fc, Puid = puid });
            }
        }
        catch { }
        return list.Count;
    }

    internal static void Kick(InnerNetClient net, int clientId, bool ban)
    {
        try
        {
            if (net == null || !net.AmHost) return;
            if (clientId < 0 || clientId == net.ClientId || clientId == net.HostId) return;
            net.KickPlayer(clientId, ban);
        }
        catch { }
    }

    internal static void Act(InnerNetClient net, int clientId, string action, string who, string reason)
    {
        switch ((action ?? "Null").Trim().ToLowerInvariant())
        {
            case "warn":
                OnyxToast.Push("Защита", $"{who}: {reason}", 3f, OnyxNotifyKind.Warning);
                break;
            case "kick":
                Kick(net, clientId, false);
                OnyxToast.Push("Кик", $"{who}: {reason}", 3f, OnyxNotifyKind.Warning);
                break;
            case "ban":
                ClientData c = FindClient(net, clientId);
                if (c != null) AddBan(SafeName(c), SafeFc(c), SafePuid(c));
                Kick(net, clientId, true);
                OnyxToast.Push("Бан", $"{who}: {reason}", 3f, OnyxNotifyKind.Danger);
                break;
        }
    }

    internal static void BanClient(InnerNetClient net, ClientData c)
    {
        if (c == null) return;
        AddBan(SafeName(c), SafeFc(c), SafePuid(c));
        Kick(net, c.Id, true);
        OnyxToast.Push("Бан-лист", SafeName(c), 2.5f, OnyxNotifyKind.Danger);
    }

    internal static void WhiteClient(ClientData c)
    {
        if (c == null) return;
        AddWhite(SafeName(c), SafeFc(c), SafePuid(c));
        OnyxToast.Push("Вайтлист", SafeName(c), 2.5f, OnyxNotifyKind.Success);
    }

    internal static void NickBanClient(InnerNetClient net, ClientData c)
    {
        if (c == null) return;
        AddNickBan(SafeName(c));
        Kick(net, c.Id, true);
        OnyxToast.Push("Ник-бан", SafeName(c), 2.5f, OnyxNotifyKind.Danger);
    }

    internal static void Enforce(InnerNetClient net, ClientData client)
    {
        try
        {
            if (net == null || !net.AmHost || client == null) return;
            if (client.Id < 0 || client.Id == net.ClientId || client.Id == net.HostId) return;

            string fc = SafeFc(client);
            string puid = SafePuid(client);
            if (IsWhite(fc, puid)) return;
            if (OnCooldown(client.Id)) return;

            if (OnyxConfig.AccessBanEnabled.Value && IsBanned(fc, puid))
            {
                Touch(client.Id);
                Kick(net, client.Id, true);
                OnyxToast.Push("Бан-лист", SafeName(client), 3f, OnyxNotifyKind.Danger);
                return;
            }

            if (OnyxConfig.AccessNickBanEnabled.Value && IsNickBanned(SafeName(client)))
            {
                Touch(client.Id);
                Kick(net, client.Id, true);
                OnyxToast.Push("Ник-бан", SafeName(client), 3f, OnyxNotifyKind.Danger);
                return;
            }

            if (OnyxConfig.AccessWhitelistOnly.Value && WhiteCount > 0 && !IsWhite(fc, puid))
            {
                Touch(client.Id);
                Kick(net, client.Id, false);
                OnyxToast.Push("Не в вайтлисте", SafeName(client), 3f, OnyxNotifyKind.Warning);
                return;
            }

            if (TryLevel(client, out int lvl))
            {
                if (OnyxConfig.MinLevelEnabled.Value && lvl < OnyxConfig.MinLevel.Value)
                {
                    Touch(client.Id);
                    Act(net, client.Id, OnyxConfig.MinLevelAction.Value, SafeName(client), $"ур.{lvl} < {OnyxConfig.MinLevel.Value}");
                    return;
                }

                if (OnyxConfig.MaxLevelEnabled.Value && lvl > OnyxConfig.MaxLevel.Value)
                {
                    Touch(client.Id);
                    Act(net, client.Id, OnyxConfig.MaxLevelAction.Value, SafeName(client), $"ур.{lvl} > {OnyxConfig.MaxLevel.Value}");
                }
            }
        }
        catch { }
    }

    private static bool OnCooldown(int id) => ActedAt.TryGetValue(id, out float t) && Time.realtimeSinceStartup - t < ActCooldown;
    private static void Touch(int id) => ActedAt[id] = Time.realtimeSinceStartup;

    internal static ClientData FindClient(InnerNetClient net, int clientId)
    {
        try
        {
            if (net == null || net.allClients == null) return null;
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id == clientId) return c;
            }
        }
        catch { }
        return null;
    }

    internal static string ClientName(InnerNetClient net, int clientId)
    {
        ClientData c = FindClient(net, clientId);
        return c != null ? SafeName(c) : "#" + clientId;
    }

    internal static string SafeName(ClientData c)
    {
        try { if (c != null && !string.IsNullOrWhiteSpace(c.PlayerName)) return c.PlayerName.Trim(); }
        catch { }
        return c != null ? "#" + c.Id : "?";
    }

    private static string SafeFc(ClientData c)
    {
        try { return c.FriendCode ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string SafePuid(ClientData c)
    {
        try { return c.ProductUserId ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static bool TryLevel(ClientData c, out int level)
    {
        level = 0;
        if (c != null && Patches.OnyxJoinLevels.TryGet(c.Id, out uint raw)) { level = (int)(raw + 1u); return true; }
        try
        {
            PlayerControl pc = c.Character;
            if (pc != null && pc.Data != null && pc.Data.PlayerLevel != uint.MaxValue && pc.Data.PlayerLevel <= 9999u)
            {
                level = (int)(pc.Data.PlayerLevel + 1u);
                return true;
            }
        }
        catch { }
        return false;
    }
}
