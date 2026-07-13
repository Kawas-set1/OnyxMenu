using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Onyx;

public sealed class OnyxJoinDetector : MonoBehaviour
{
    private const float ScanInterval = 0.5f;
    private const float PendingTimeout = 2.5f;

    private static readonly string[] SusTokens =
    {
        "menu", "mod", "cheat", "hack", "inject", "trainer", "aimbot", "godmode",
        "esp", "exploit", "njord", "meow", "sus", "aura", "xenon", "nexus",
    };

    private readonly HashSet<int> _known = new HashSet<int>();
    private readonly Dictionary<int, float> _pending = new Dictionary<int, float>();
    private readonly List<int> _live = new List<int>();
    private readonly List<int> _stale = new List<int>();
    private bool _primed;
    private float _next;

    public void Update()
    {
        if (OnyxConfig.JoinDetect == null || !OnyxConfig.JoinDetect.Value)
        {
            Forget();
            return;
        }

        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + ScanInterval;
        Scan();
    }

    private void Forget()
    {
        if (_known.Count == 0 && _pending.Count == 0 && !_primed) return;
        _known.Clear();
        _pending.Clear();
        _primed = false;
    }

    private void Scan()
    {
        InnerNetClient net = Net();
        if (net == null || net.allClients == null)
        {
            Forget();
            return;
        }

        float now = Time.realtimeSinceStartup;
        bool priming = !_primed;
        _live.Clear();

        var cursor = net.allClients.GetEnumerator();
        while (cursor.MoveNext())
        {
            ClientData client = cursor.Current;
            if (client == null || client.Id < 0) continue;
            _live.Add(client.Id);

            if (priming) { _known.Add(client.Id); continue; }
            if (_known.Contains(client.Id)) continue;
            if (client.Id == net.ClientId) { _known.Add(client.Id); continue; }

            if (!_pending.ContainsKey(client.Id)) _pending[client.Id] = now;
            bool hasChar = client.Character != null && client.Character.Data != null;
            if (hasChar || now - _pending[client.Id] >= PendingTimeout)
            {
                Announce(client);
                _known.Add(client.Id);
                _pending.Remove(client.Id);
            }
        }

        if (priming) _primed = true;
        PruneDeparted();
    }

    private void PruneDeparted()
    {
        _stale.Clear();
        foreach (int id in _known)
            if (!_live.Contains(id)) _stale.Add(id);
        for (int i = 0; i < _stale.Count; i++) _known.Remove(_stale[i]);

        _stale.Clear();
        foreach (var pair in _pending)
            if (!_live.Contains(pair.Key)) _stale.Add(pair.Key);
        for (int i = 0; i < _stale.Count; i++) _pending.Remove(_stale[i]);
    }

    private static void Announce(ClientData client)
    {
        string name = ResolveName(client);
        string level = ResolveLevel(client);

        string platform = "?";
        string raw = string.Empty;
        try
        {
            if (client.PlatformData != null)
            {
                platform = PlatformLabel(client.PlatformData.Platform);
                raw = CleanRaw(client.PlatformData.PlatformName);
            }
        }
        catch { }

        bool sus = IsSuspicious(raw);
        string tail = $"Ур.{level} · {platform}";
        if (raw.Length > 0) tail += $" · {raw}";

        string title = (sus ? "⚠ " : "＋ ") + name;
        OnyxToast.Push(title, tail, 4.5f, sus ? OnyxNotifyKind.Danger : OnyxNotifyKind.Info);
    }

    private static string ResolveName(ClientData client)
    {
        try
        {
            if (client.Character != null && client.Character.Data != null && !string.IsNullOrWhiteSpace(client.Character.Data.PlayerName))
                return Trim(client.Character.Data.PlayerName, 22);
        }
        catch { }

        try
        {
            if (client.PlatformData != null && !string.IsNullOrWhiteSpace(client.PlatformData.PlatformName))
                return Trim(client.PlatformData.PlatformName, 22);
        }
        catch { }

        return "Игрок";
    }

    private static string ResolveLevel(ClientData client)
    {
        if (Patches.OnyxJoinLevels.TryGet(client.Id, out uint raw)) return (raw + 1u).ToString();
        try
        {
            if (client.Character != null && client.Character.Data != null && client.Character.Data.PlayerLevel != uint.MaxValue && client.Character.Data.PlayerLevel <= 9999u)
                return (client.Character.Data.PlayerLevel + 1u).ToString();
        }
        catch { }

        return "?";
    }

    private static string CleanRaw(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string raw = value.Replace("\r", " ").Replace("\n", " ").Trim();

        int pipe = raw.IndexOf('|');
        if (pipe > 0) raw = raw.Substring(0, pipe).TrimEnd();

        if (raw.Equals("TESTNAME", System.StringComparison.OrdinalIgnoreCase)) return string.Empty;

        return Trim(raw, 24);
    }

    private static bool IsSuspicious(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        string lower = raw.ToLowerInvariant();
        for (int i = 0; i < SusTokens.Length; i++)
            if (lower.Contains(SusTokens[i])) return true;
        return false;
    }

    private static string Trim(string value, int max)
    {
        string clean = value.Trim();
        return clean.Length <= max ? clean : clean.Substring(0, max - 1).TrimEnd() + "…";
    }

    private static InnerNetClient Net()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }

    private static string PlatformLabel(Platforms platform)
    {
        return (int)platform switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS Store",
            5 => "Itch.io",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
            112 => "Starlight",
            _ => "Unknown",
        };
    }
}
