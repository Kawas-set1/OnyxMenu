using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;

namespace Onyx;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal static class OnyxCommands
{
    public static bool Prefix(PlayerControl __instance, string chatText)
    {
        try
        {
            if (__instance != PlayerControl.LocalPlayer || string.IsNullOrEmpty(chatText)) return true;

            string t = chatText.Trim();
            if (t.Length < 2 || t[0] != '/') return true;
            int sp = t.IndexOf(' ');
            string cmd = (sp < 0 ? t : t.Substring(0, sp)).ToLowerInvariant();
            string rest = sp < 0 ? "" : t.Substring(sp + 1).Trim();
            bool host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

            if (OnyxConfig.ChatCmds == null || !OnyxConfig.ChatCmds.Value) return true;

            switch (cmd)
            {
                case "/kick": Kick(rest, false, host); return false;
                case "/ban": Kick(rest, true, host); return false;
                case "/mute": Mute(rest, host); return false;
                case "/color":
                    if (rest.IndexOf(' ') < 0) return true;
                    ColorOther(rest, host); return false;
                case "/role": Role(rest, host); return false;
                case "/start": Simple(host, () => { if (GameStartManager.Instance != null) GameStartManager.Instance.BeginGame(); }, "Старт", "Start"); return false;
                case "/end": Simple(host, () => { GameManager.Instance.RpcEndGame((GameOverReason)1, false); }, "Матч завершён", "Match ended"); return false;
                case "/meeting": Simple(host, () => { PlayerControl.LocalPlayer.CmdReportDeadBody(null); }, "Собрание", "Meeting"); return false;
                case "/close": if (RequireHost(host)) Toast(OnyxText.T("Собрание", "Meeting"), OnyxMeetingTools.CloseMeeting()); return false;
                case "/fix": if (RequireHost(host)) FixSabs(); return false;
                default: return true;
            }
        }
        catch { return true; }
    }

    private static System.DateTime _lastHelp;

    internal static void BroadcastHelp()
    {
        if ((System.DateTime.UtcNow - _lastHelp).TotalSeconds < 2.5) return;
        _lastHelp = System.DateTime.UtcNow;
        Broadcast(OnyxText.T("» Onyx: /help · /c <цвет> · /color <цвет>", "» Onyx: /help · /c <color> · /color <color>"));
    }

    internal static bool TryHostSelf(string cmd)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;
        switch (cmd)
        {
            case "/fix": FixSabs(); return true;
            default: return false;
        }
    }

    private static void FixSabs()
    {
        OnyxSabotage.Fix();
        Toast(OnyxText.T("Саботажи", "Sabotages"), OnyxText.T("Все саботажи починены.", "All sabotages fixed."));
    }

    internal static void ShowHostHelp()
    {
        AddLocal(OnyxText.T("Хост: /kick · /ban · /mute <ник> · /color <ник> <цвет> · /role <ник> <роль> · /start · /end · /meeting · /close · /fix",
                            "Host: /kick · /ban · /mute <name> · /color <name> <color> · /role <name> <role> · /start · /end · /meeting · /close · /fix"));
    }

    private static void Broadcast(string text)
    {
        try { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.RpcSendChat(text); } catch { }
    }

    private static void AddLocal(string text)
    {
        try
        {
            if (HudManager.Instance != null && HudManager.Instance.Chat != null && PlayerControl.LocalPlayer != null)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, text);
        }
        catch { }
    }

    private static void Kick(string name, bool ban, bool host)
    {
        if (!RequireHost(host)) return;
        PlayerControl p = Find(name);
        if (p == null) { NotFound(name); return; }
        try { AmongUsClient.Instance.KickPlayer(p.OwnerId, ban); } catch { }
        Toast(ban ? OnyxText.T("Бан", "Ban") : OnyxText.T("Кик", "Kick"), PName(p));
    }

    private static void Mute(string name, bool host)
    {
        if (!RequireHost(host)) return;
        PlayerControl p = Find(name);
        if (p == null || p.Data == null) { NotFound(name); return; }
        bool now = OnyxMuteList.Toggle(p.Data.FriendCode);
        Toast(OnyxText.T("Мут", "Mute"), PName(p) + (now ? OnyxText.T(" — заглушён", " — muted") : OnyxText.T(" — размут", " — unmuted")));
    }

    private static void ColorOther(string rest, bool host)
    {
        if (!RequireHost(host)) return;
        int sp = rest.LastIndexOf(' ');
        if (sp < 0) return;
        PlayerControl p = Find(rest.Substring(0, sp));
        int id = OnyxColorCmd.ColorId(rest.Substring(sp + 1).Trim().ToLowerInvariant());
        if (p == null) { NotFound(rest.Substring(0, sp)); return; }
        if (id < 0) return;
        try { p.RpcSetColor((byte)id); } catch { }
        Toast(OnyxText.T("Цвет", "Color"), PName(p));
    }

    private static void Role(string rest, bool host)
    {
        if (!RequireHost(host)) return;
        int sp = rest.LastIndexOf(' ');
        if (sp < 0) return;
        PlayerControl p = Find(rest.Substring(0, sp));
        if (p == null || p.Data == null) { NotFound(rest.Substring(0, sp)); return; }
        RoleTypes role = RoleFromArg(rest.Substring(sp + 1).Trim().ToLowerInvariant());
        if ((int)role == 255) return;
        OnyxForceRoles.Set(p.PlayerId, role);
        try { p.RpcSetRole(role, false); } catch { try { p.RpcSetRole(role); } catch { } }
        Toast(OnyxText.T("Роль", "Role"), PName(p));
    }

    private static void Simple(bool host, System.Action act, string ru, string en)
    {
        if (!RequireHost(host)) return;
        try { act(); Toast(OnyxText.T(ru, en), ""); } catch { }
    }

    private static RoleTypes RoleFromArg(string a)
    {
        if (string.IsNullOrEmpty(a)) return (RoleTypes)255;
        if (a == "пред" || a == "имп" || a == "imp") return RoleTypes.Impostor;
        if (a == "мир" || a == "крю") return RoleTypes.Crewmate;
        foreach (var r in OnyxForceRoles.Roles)
        {
            if ((int)r.Role == 255) continue;
            string ru = r.Ru.ToLowerInvariant(), en = r.En.ToLowerInvariant();
            if (ru == a || en == a || ru.StartsWith(a) || en.StartsWith(a)) return r.Role;
        }
        return (RoleTypes)255;
    }

    private static PlayerControl Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim().ToLowerInvariant();
        PlayerControl partial = null;
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || pc.Data == null) continue;
            string n = Strip(pc.Data.PlayerName).ToLowerInvariant();
            if (n == name) return pc;
            if (partial == null && (n.StartsWith(name) || n.Contains(name))) partial = pc;
        }
        return partial;
    }

    private static string PName(PlayerControl pc) => pc != null && pc.Data != null ? Strip(pc.Data.PlayerName) : "?";

    private static string Strip(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        bool tag = false;
        foreach (char ch in s)
        {
            if (ch == '<') tag = true;
            else if (ch == '>') tag = false;
            else if (!tag) sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    private static bool RequireHost(bool host)
    {
        if (host) return true;
        Toast(OnyxText.T("Команда", "Command"), OnyxText.T("Только хост.", "Host only."));
        return false;
    }

    private static void NotFound(string name) => Toast(OnyxText.T("Команда", "Command"), OnyxText.T("Игрок не найден: ", "Player not found: ") + name.Trim());

    private static void Toast(string title, string body) => OnyxToast.Push(title, body, 2.5f, OnyxNotifyKind.Info);
}
