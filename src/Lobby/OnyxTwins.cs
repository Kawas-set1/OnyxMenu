using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Onyx;

public sealed class OnyxTwins : MonoBehaviour
{
    private const int Max = 100;
    private const int Owner = -2;
    private const byte SnapRpc = 21;
    private const ushort SeqStep = 5;
    private const float SettleDelay = 2.2f;

    private sealed class Twin { public PlayerControl Pc; public Vector2 At; public int Left; public float NextAt; public ushort Seq; }
    private struct Job { public byte Src; public Vector2 At; public bool Slow; }

    private static readonly List<Twin> _live = new List<Twin>();
    private static readonly Queue<Job> _pend = new Queue<Job>();
    private float _spawnGate;
    private float _tick;

    internal static OnyxTwins Instance { get; private set; }
    public void Awake() => Instance = this;
    public void OnDestroy() { if (Instance == this) Instance = null; }

    internal static int Count => _live.Count;
    internal static void Forget() { _live.Clear(); _pend.Clear(); }

    public void Update()
    {
        float now = Time.unscaledTime;

        try { Clicks(); } catch { }

        if (_pend.Count > 0 && now >= _spawnGate && Ready())
        {
            Job j = _pend.Dequeue();
            _spawnGate = now + (j.Slow ? 0.28f : 0.14f);
            if (_live.Count < Max)
            {
                PlayerControl src = ById(j.Src);
                if (src != null) { try { Make(src, j.At); } catch { } }
            }
        }

        if (_live.Count > 0 && now >= _tick)
        {
            _tick = now + 0.3f;
            try { Settle(now); } catch { }
        }
    }

    internal static string CloneOf(PlayerControl src)
    {
        if (Instance == null || !Ready()) return OnyxText.T("Только хост.", "Host only.");
        if (src == null || src.transform == null) return OnyxText.T("Нет игрока.", "No player.");
        if (_live.Count + _pend.Count >= Max) return OnyxText.T("Лимит клонов.", "Clone limit.");
        Vector3 p = src.transform.position + Vector3.left * 0.6f;
        _pend.Enqueue(new Job { Src = src.PlayerId, At = new Vector2(p.x, p.y) });
        return OnyxText.T("Клон в очереди", "Clone queued");
    }

    internal static string Formation(int idx, int count)
    {
        if (Instance == null || !Ready()) return OnyxText.T("Только хост.", "Host only.");
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.transform == null) return OnyxText.T("Нет игрока.", "No player.");
        int n = Mathf.Clamp(count, 1, Max - _live.Count - _pend.Count);
        if (n <= 0) return OnyxText.T("Лимит клонов.", "Clone limit.");
        Vector3 c = me.transform.position;
        for (int i = 0; i < n; i++)
        {
            Vector3 fp = OnyxLobbyClones.FormationPos(idx, i, n, c);
            _pend.Enqueue(new Job { Src = me.PlayerId, At = new Vector2(fp.x, fp.y) });
        }
        return OnyxText.T("Клонов в очереди: ", "Clones queued: ") + n;
    }

    internal static string Text(string text)
    {
        if (Instance == null || !Ready()) return OnyxText.T("Только хост.", "Host only.");
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.transform == null) return OnyxText.T("Нет игрока.", "No player.");
        if (string.IsNullOrWhiteSpace(text)) return OnyxText.T("Пустой текст.", "Empty text.");

        text = text.ToUpperInvariant();
        float px = 0.38f;
        float need = OnyxCloneFont.GetTextWidth(text, px);
        if (need > 15f) px = px * 15f / need;
        List<Vector3> offs = OnyxCloneFont.GetPositions(text, px);
        if (offs.Count == 0) return OnyxText.T("Пусто.", "Empty.");

        Vector3 c = me.transform.position;
        int room = Max - _live.Count - _pend.Count;
        int made = 0;
        for (int i = 0; i < offs.Count && made < room; i++)
        {
            _pend.Enqueue(new Job { Src = me.PlayerId, At = new Vector2(c.x + offs[i].x, c.y + offs[i].y), Slow = true });
            made++;
        }
        return OnyxText.T("Клонов в очереди: ", "Clones queued: ") + made;
    }

    internal static void ClearAll()
    {
        _pend.Clear();
        Twin[] arr = _live.ToArray();
        _live.Clear();
        foreach (Twin t in arr) Kill(t?.Pc);
    }

    internal static bool Ready()
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;
            if (PlayerControl.LocalPlayer == null || GameData.Instance == null) return false;
            InnerNetClient.GameStates gs = ((InnerNetClient)AmongUsClient.Instance).GameState;
            return gs == InnerNetClient.GameStates.Joined || gs == InnerNetClient.GameStates.Started;
        }
        catch { return false; }
    }

    private void Clicks()
    {
        if (OnyxConfig.NetCloneMode == null || !OnyxConfig.NetCloneMode.Value) return;
        if (!Ready() || OnyxMenu.Opened || Camera.main == null) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;

        Vector3 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 at = new Vector2(w.x, w.y);

        if (Input.GetMouseButtonDown(0))
        {
            if (_live.Count + _pend.Count < Max)
                _pend.Enqueue(new Job { Src = me.PlayerId, At = at });
        }
        else if (Input.GetMouseButtonDown(1))
        {
            Nearest(at);
        }
    }

    private static void Nearest(Vector2 at)
    {
        int best = -1;
        float bd = 1.6f;
        for (int i = 0; i < _live.Count; i++)
        {
            Twin t = _live[i];
            if (t?.Pc == null) continue;
            float d = Vector2.Distance(t.At, at);
            if (d < bd) { bd = d; best = i; }
        }
        if (best < 0) return;
        PlayerControl pc = _live[best].Pc;
        _live.RemoveAt(best);
        Kill(pc);
    }

    private static PlayerControl ById(byte pid)
    {
        try { foreach (PlayerControl p in PlayerControl.AllPlayerControls) if (p != null && p.PlayerId == pid) return p; }
        catch { }
        return null;
    }

    private void Make(PlayerControl src, Vector2 at)
    {
        if (src == null || src.Data == null) return;
        AmongUsClient net = AmongUsClient.Instance;
        PlayerControl prefab = net != null ? net.PlayerPrefab : null;
        if (prefab == null) return;

        Vector3 pos = new Vector3(at.x, at.y, src.transform.position.z);
        PlayerControl tw = Object.Instantiate(prefab);
        tw.PlayerId = src.PlayerId;
        tw.isNew = false;
        tw.notRealPlayer = true;
        ((InnerNetClient)net).Spawn((InnerNetObject)(object)tw, Owner, (SpawnFlags)0);
        ((Component)tw).transform.position = pos;

        Twin t = new Twin { Pc = tw, At = at, Left = 6, NextAt = Time.unscaledTime + SettleDelay };
        if (tw.NetTransform != null)
        {
            ((Behaviour)tw.NetTransform).enabled = true;
            try { t.Seq = tw.NetTransform.lastSequenceId; } catch { }
            Place(t);
        }
        _live.Add(t);
    }

    private static void Settle(float now)
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            Twin t = _live[i];
            if (t?.Pc == null) { _live.RemoveAt(i); continue; }
            if (t.Left <= 0 || now < t.NextAt) continue;
            t.Left--;
            t.NextAt = now + 0.4f;
            Place(t);
        }
    }

    private static void Place(Twin t)
    {
        if (t?.Pc == null) return;
        try
        {
            CustomNetworkTransform nt = t.Pc.NetTransform;
            try { nt.Halt(); } catch { }
            t.Seq = (ushort)(t.Seq + SeqStep);
            nt.SnapTo(t.At, t.Seq);
            MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(((InnerNetObject)nt).NetId, SnapRpc, SendOption.Reliable, -1);
            NetHelpers.WriteVector2(t.At, w);
            w.Write(t.Seq);
            AmongUsClient.Instance.FinishRpcImmediately(w);
        }
        catch { }
    }

    private static void Kill(PlayerControl pc)
    {
        if (pc == null) return;
        try { pc.PlayerId = 253; } catch { }
        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            MessageWriter w = MessageWriter.Get((SendOption)1);
            w.StartMessage(5);
            w.Write(((InnerNetClient)net).GameId);
            w.StartMessage(5);
            w.WritePacked(((InnerNetObject)(object)pc).NetId);
            w.EndMessage();
            w.EndMessage();
            ((InnerNetClient)net).SendOrDisconnect(w);
            w.Recycle();
            ((InnerNetClient)net).RemoveNetObject((InnerNetObject)(object)pc);
        }
        catch { }
        try { Object.Destroy(pc.gameObject); } catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.GetTruePosition))]
internal static class OnyxTwinsPosGuard
{
    public static bool Prefix(PlayerControl __instance, ref Vector2 __result)
    {
        try
        {
            if (__instance != null && __instance.transform != null) return true;
        }
        catch { }
        __result = Vector2.zero;
        return false;
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), "Start")]
internal static class OnyxTwinsResetPatch
{
    public static void Postfix() => OnyxTwins.Forget();
}

