using System.Text.RegularExpressions;
using Hazel;

namespace Onyx;

internal static class OnyxWhisper
{
    private static readonly Regex Tags = new Regex("<.*?>");

    internal static bool TryHandle(ChatController chat)
    {
        if (chat == null || chat.freeChatField == null || chat.freeChatField.textArea == null) return false;
        string text = chat.freeChatField.textArea.text;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string low = text.ToLowerInvariant();
        if (!low.StartsWith("/w ") && !low.StartsWith("/pm ") && !low.StartsWith("/msg ")) return false;

        string[] parts = text.Split(new[] { ' ' }, 3);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
        {
            Local("<color=#FF6B6B>[Onyx]</color> " + OnyxText.T("Формат: /w [ник или ID] сообщение", "Usage: /w [name or ID] message"));
            Clear(chat);
            return true;
        }

        PlayerControl target = Find(parts[1].Trim().ToLowerInvariant());
        string msg = Strip(parts[2]);
        if (target == null || target.Data == null || target == PlayerControl.LocalPlayer)
        {
            Local("<color=#FF6B6B>[Onyx]</color> " + OnyxText.T("Игрок не найден.", "Player not found."));
            Clear(chat);
            return true;
        }

        try
        {
            MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 13, SendOption.Reliable, target.OwnerId);
            w.Write(OnyxText.T("шепчет тебе:\n", "whispers to you:\n") + msg);
            AmongUsClient.Instance.FinishRpcImmediately(w);
            Local($"<color=#8AB4FF>{OnyxText.T("Шепнул", "Whisper to")} {Strip(target.Data.PlayerName)}:</color>\n{msg}");
        }
        catch { }

        Clear(chat);
        return true;
    }

    internal static void Prefill(string name)
    {
        try
        {
            if (HudManager.Instance == null || HudManager.Instance.Chat == null) return;
            ChatController chat = HudManager.Instance.Chat;
            chat.SetVisible(true);
            if (chat.freeChatField != null && chat.freeChatField.textArea != null)
                chat.freeChatField.textArea.SetText("/w " + name + " ", string.Empty);
        }
        catch { }
    }

    private static PlayerControl Find(string q)
    {
        try
        {
            if (PlayerControl.AllPlayerControls == null) return null;
            if (byte.TryParse(q, out byte id))
                foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                    if (p != null && p.PlayerId == id) return p;

            PlayerControl partial = null;
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.Disconnected || p == PlayerControl.LocalPlayer) continue;
                string name = Strip(p.Data.PlayerName).ToLowerInvariant().Trim();
                if (name == q) return p;
                if (partial == null && name.StartsWith(q)) partial = p;
            }
            return partial;
        }
        catch { return null; }
    }

    private static string Strip(string s) => Tags.Replace(s ?? string.Empty, string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);

    private static void Local(string msg)
    {
        try
        {
            if (HudManager.Instance != null && HudManager.Instance.Chat != null && PlayerControl.LocalPlayer != null)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
        }
        catch { }
    }

    private static void Clear(ChatController chat)
    {
        try { chat.freeChatField.textArea.SetText(string.Empty, string.Empty); } catch { }
    }
}
