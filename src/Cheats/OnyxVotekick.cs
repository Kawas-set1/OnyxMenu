using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Onyx;

// Войткик через VoteBanSystem.CmdAddVote.
public sealed class OnyxVotekick : MonoBehaviour
{
    private enum Phase { Off, Room, Voted, Left, Rejoin, Final }

    private const float Settle = 0.4f;
    private const float LeaveMin = 1.1f;
    private const float LeaveMax = 1.5f;
    private const float StableHold = 0.5f;
    private const float RejoinDelay = 1.5f;
    private const float RejoinTimeout = 22f;
    private const float ManualTimeout = 180f;
    private const float FinalDelay = 1.5f;
    private const float RapidStep = 0.12f;
    private const float PulseStep = 0.3f;
    private const int SweepPasses = 3;
    private const int Cycles = 2;

    private static Phase _phase = Phase.Off;
    private static int _code;
    private static int _cyclesDone;
    private static float _at;
    private static float _pulseAt;
    private static float _votedStart;
    private static int _votedCount;
    private static float _votedStableAt;
    private static bool _swept;

    private static readonly List<byte> _queue = new List<byte>();
    private static float _rapidAt;
    private static int _passesLeft;

    private const float AutoInterval = 3f;
    private static readonly HashSet<byte> _targets = new HashSet<byte>();
    private static bool _autoOn;
    private static float _autoAt;

    public static bool Armed => _phase != Phase.Off;
    public static int TargetCount => _targets.Count;
    public static bool AutoTargeting => _autoOn;
    public static bool IsTarget(byte id) => _targets.Contains(id);

    public static void ToggleTarget(byte id)
    {
        if (!_targets.Remove(id)) _targets.Add(id);
        if (_targets.Count == 0) _autoOn = false;
    }

    public static void ClearTargets()
    {
        _targets.Clear();
        _autoOn = false;
        Toast(OnyxText.T("цели", "targets"), OnyxText.T("Список целей очищен.", "Target list cleared."), OnyxNotifyKind.Info);
    }

    public static bool HostIsTarget()
    {
        PlayerControl h = HostPlayer();
        return h != null && _targets.Contains(h.PlayerId);
    }

    public static void ToggleHostTarget()
    {
        PlayerControl h = HostPlayer();
        if (h == null) { Toast(OnyxText.T("хост", "host"), OnyxText.T("Хост не найден.", "Host not found."), OnyxNotifyKind.Warning); return; }
        if (h == PlayerControl.LocalPlayer) { Toast(OnyxText.T("хост", "host"), OnyxText.T("Хост — это ты.", "You are the host."), OnyxNotifyKind.Warning); return; }
        ToggleTarget(h.PlayerId);
        string nm = h.Data != null && !string.IsNullOrEmpty(h.Data.PlayerName) ? h.Data.PlayerName : "?";
        Toast(OnyxText.T("хост", "host"), _targets.Contains(h.PlayerId)
            ? OnyxText.T("Отмечен целью: ", "Marked as target: ") + nm
            : OnyxText.T("Снят с целей: ", "Unmarked: ") + nm, OnyxNotifyKind.Info);
    }

    private static PlayerControl HostPlayer()
    {
        try
        {
            InnerNetClient net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null) return null;
            int hostId = net.HostId;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.Data != null && !pc.Data.Disconnected && pc.OwnerId == hostId) return pc;
        }
        catch { }
        return null;
    }

    public static void ToggleTargetAuto()
    {
        if (_autoOn) { _autoOn = false; Toast(OnyxText.T("авто-цели", "auto-targets"), OnyxText.T("Выключено.", "Off."), OnyxNotifyKind.Info); return; }
        if (_targets.Count == 0) { Toast(OnyxText.T("авто-цели", "auto-targets"), OnyxText.T("Сначала отметь цели.", "Mark targets first."), OnyxNotifyKind.Warning); return; }
        _autoOn = true;
        _autoAt = Time.unscaledTime;
        Toast(OnyxText.T("авто-цели", "auto-targets"), OnyxText.T("Голосую по отмеченным: ", "Voting marked: ") + _targets.Count, OnyxNotifyKind.Success);
    }

    private static void TickAuto()
    {
        if (!_autoOn) return;
        if (_targets.Count == 0) { _autoOn = false; return; }
        if (Time.unscaledTime < _autoAt) return;
        _autoAt = Time.unscaledTime + AutoInterval;
        VoteTargets();
    }

    private static int VoteTargets()
    {
        if (VoteBanSystem.Instance == null || _targets.Count == 0) return 0;
        int n = 0;
        try
        {
            foreach (byte id in _targets)
            {
                PlayerControl pc = ById(id);
                if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected) continue;
                VoteBanSystem.Instance.CmdAddVote(pc.Data.ClientId);
                n++;
            }
        }
        catch { }
        return n;
    }

    private static bool IsSel(PlayerControl pc)
    {
        return _targets.Count == 0 || _targets.Contains(pc.PlayerId);
    }

    public static void ToggleAuto()
    {
        if (_phase != Phase.Off) { Stop(OnyxText.T("выключено", "off")); return; }
        _cyclesDone = 0;
        _phase = Phase.Room;
        _at = Time.unscaledTime + Settle;
        Toast(OnyxText.T("взведено", "armed"), OnyxText.T("Голосую и, если включён перезаход, сам перезайду.", "Voting; auto-rejoin if enabled."), OnyxNotifyKind.Info);
    }

    private static void Stop(string why)
    {
        _phase = Phase.Off;
        _swept = false;
        _passesLeft = 0;
        _queue.Clear();
        Toast(OnyxText.T("авто", "auto"), why, OnyxNotifyKind.Info);
    }

    public void Update()
    {
        TickAuto();
        TickRapid();
        if (_phase == Phase.Off) return;
        try
        {
            switch (_phase)
            {
                case Phase.Room: TickRoom(); break;
                case Phase.Voted: TickVoted(); break;
                case Phase.Left: TickLeft(); break;
                case Phase.Rejoin: TickRejoin(); break;
                case Phase.Final: TickFinal(); break;
            }
        }
        catch { }
    }

    private static void TickRoom()
    {
        if (!InRoom()) return;
        if (Time.unscaledTime < _at) return;

        bool auto = OnyxConfig.VkRejoin.Value;
        SaveCode(!auto);

        if (_cyclesDone >= Cycles)
        {
            _swept = false;
            _phase = Phase.Final;
            _at = Time.unscaledTime + FinalDelay;
            Toast(OnyxText.T("финал", "final"), OnyxText.T("Сейчас пройдусь по каждому…", "Sweeping each player shortly…"), OnyxNotifyKind.Success);
            return;
        }

        int sent = VoteAll(false);
        string tail = auto ? OnyxText.T(". Выхожу…", ". Leaving…") : OnyxText.T(". Выхожу, код скопирован.", ". Leaving, code copied.");
        Toast(OnyxText.T("раунд ", "round ") + (_cyclesDone + 1), OnyxText.T("Голоса ушли: ", "Votes sent: ") + sent + tail, OnyxNotifyKind.Success);
        float now = Time.unscaledTime;
        _phase = Phase.Voted;
        _votedStart = now;
        _pulseAt = now + PulseStep;
        _votedCount = -1;
        _votedStableAt = now + StableHold;
    }

    private static void TickVoted()
    {
        float now = Time.unscaledTime;
        if (now >= _pulseAt) { _pulseAt = now + PulseStep; VoteAll(true); }

        int cnt = CountTargets();
        if (cnt != _votedCount) { _votedCount = cnt; _votedStableAt = now + StableHold; }

        float since = now - _votedStart;
        bool ready = since >= LeaveMin && now >= _votedStableAt;
        if (!ready && since < LeaveMax) return;

        Leave();
        _phase = Phase.Left;
        _at = now + RejoinDelay;
    }

    private static int CountTargets()
    {
        int n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && !pc.AmOwner && pc.Data != null && !pc.Data.Disconnected && IsSel(pc)) n++;
        }
        catch { }
        return n;
    }

    private static void TickLeft()
    {
        if (InRoom()) return;
        if (Time.unscaledTime < _at) return;

        if (OnyxConfig.VkRejoin.Value)
        {
            Rejoin(_code);
            _at = Time.unscaledTime + RejoinTimeout;
        }
        else
        {
            _at = Time.unscaledTime + ManualTimeout;
            Toast(OnyxText.T("ждём", "waiting"), OnyxText.T("Вставь код и зайди снова — продолжу.", "Paste the code and rejoin — I'll continue."), OnyxNotifyKind.Info);
        }
        _phase = Phase.Rejoin;
    }

    private static void TickRejoin()
    {
        if (InRoom())
        {
            _cyclesDone++;
            _phase = Phase.Room;
            _at = Time.unscaledTime + Settle;
            Toast(OnyxText.T("перезаход", "rejoin"), OnyxText.T("Зашёл, раунд ", "Joined, round ") + (_cyclesDone + 1), OnyxNotifyKind.Info);
            return;
        }
        if (Time.unscaledTime >= _at)
        {
            SaveCode(true);
            Stop(OnyxConfig.VkRejoin.Value
                ? OnyxText.T("Не смог сам зайти — код скопирован, зайди вручную.", "Auto-join failed — code copied, rejoin manually.")
                : OnyxText.T("Долго нет захода — отменил.", "No rejoin — cancelled."));
        }
    }

    private static void TickFinal()
    {
        if (_swept) return;
        if (Time.unscaledTime < _at) return;
        StartRapid();
        _swept = true;
    }

    private static void TickRapid()
    {
        if (_queue.Count == 0)
        {
            if (_passesLeft > 0) { _passesLeft--; FillQueue(); return; }
            if (_phase == Phase.Final && _swept) Stop(OnyxText.T("Готово.", "Done."));
            return;
        }
        if (Time.unscaledTime < _rapidAt) return;
        _rapidAt = Time.unscaledTime + RapidStep;

        byte id = _queue[0];
        _queue.RemoveAt(0);
        PlayerControl pc = ById(id);
        if (pc != null) TryVote(pc.Data != null ? pc.Data.ClientId : -1);
    }

    private static void StartRapid()
    {
        _passesLeft = SweepPasses - 1;
        FillQueue();
        _rapidAt = Time.unscaledTime;
    }

    private static void FillQueue()
    {
        _queue.Clear();
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && !pc.AmOwner && pc.Data != null && !pc.Data.Disconnected && IsSel(pc))
                    _queue.Add(pc.PlayerId);
        }
        catch { }
    }

    public static void RapidAll()
    {
        StartRapid();
        int targets = _queue.Count;
        if (targets > 0) Toast(OnyxText.T("перебор", "sweep"), OnyxText.T("Бью по всем ×", "Voting all ×") + SweepPasses + ": " + targets, OnyxNotifyKind.Success);
        else Toast(OnyxText.T("перебор", "sweep"), OnyxText.T("Нет целей.", "No targets."), OnyxNotifyKind.Warning);
    }

    public static int VoteAll(bool once)
    {
        if (VoteBanSystem.Instance == null || PlayerControl.AllPlayerControls == null) return 0;
        int n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected || !IsSel(pc)) continue;
                int reps = once ? 1 : 3;
                for (int i = 0; i < reps; i++) { VoteBanSystem.Instance.CmdAddVote(pc.Data.ClientId); n++; }
            }
        }
        catch { }
        return n;
    }

    public static void VoteAllStay()
    {
        int n = VoteAll(false);
        if (n > 0) Toast(OnyxText.T("вручную", "manual"), OnyxText.T("Отправлено: ", "Sent: ") + n + OnyxText.T(". Остаюсь.", ". Staying."), OnyxNotifyKind.Success);
        else Toast(OnyxText.T("вручную", "manual"), OnyxText.T("Нет целей или система не готова.", "No targets or system not ready."), OnyxNotifyKind.Warning);
    }

    public static void VoteOne(PlayerControl pc)
    {
        if (pc == null || pc.Data == null) return;
        if (TryVote(pc.Data.ClientId))
        {
            string nm = pc.Data.PlayerName;
            if (string.IsNullOrEmpty(nm)) nm = "?";
            Toast(nm, OnyxText.T("Голос ушёл. Нужно 3 разных клиента.", "Vote sent. Needs 3 unique clients."), OnyxNotifyKind.Info);
        }
    }

    private static bool TryVote(int clientId)
    {
        if (clientId < 0 || VoteBanSystem.Instance == null) return false;
        try { VoteBanSystem.Instance.CmdAddVote(clientId); return true; }
        catch { return false; }
    }

    private static void SaveCode(bool copyAlways = false)
    {
        try
        {
            int code = ((InnerNetClient)AmongUsClient.Instance).GameId;
            if (code != 0) _code = code;
            if ((copyAlways || OnyxConfig.VkCopyCode.Value) && _code != 0)
                GUIUtility.systemCopyBuffer = GameCode.IntToGameName(_code);
        }
        catch { }
    }

    private static void Rejoin(int code)
    {
        try
        {
            AmongUsClient au = AmongUsClient.Instance;
            if (au == null || code == 0) return;
            au.GameId = code;
            var e = au.CoJoinOnlineGameFromCode(code);
            if (e != null) au.StartCoroutine(e);
        }
        catch { }
    }

    private static void Leave()
    {
        try { if (AmongUsClient.Instance != null) AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame); }
        catch { }
    }

    private static bool InRoom() => LobbyBehaviour.Instance != null || ShipStatus.Instance != null;

    private static PlayerControl ById(byte id)
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.PlayerId == id) return pc;
        }
        catch { }
        return null;
    }

    private static void Toast(string t, string d, OnyxNotifyKind k) => OnyxToast.Push(OnyxText.T("Войткик — ", "Votekick — ") + t, d, 3f, k);
}
