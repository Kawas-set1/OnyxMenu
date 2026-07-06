using HarmonyLib;

namespace Onyx.Patches;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal static class XmasSendPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] string chatText)
    {
        try
        {
            if (__instance != PlayerControl.LocalPlayer || !OnyxXmas.IsCommand(chatText)) return true;
            bool on = OnyxXmas.Toggle(__instance.PlayerId);
            OnyxToast.Push(OnyxText.T("Ёлка", "Xmas"),
                on ? OnyxText.T("Цвета переливаются!", "Colors cycling!") : OnyxText.T("Остановлено.", "Stopped."),
                2f, OnyxNotifyKind.Info);
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class XmasChatCommandPatch
{
    public static bool Prefix([HarmonyArgument(0)] PlayerControl sourcePlayer, [HarmonyArgument(1)] string chatText)
    {
        try
        {
            if (OnyxConfig.ChatCmdXmas == null || !OnyxConfig.ChatCmdXmas.Value) return true;
            if (sourcePlayer == null || !OnyxXmas.IsCommand(chatText)) return true;
            OnyxXmas.Toggle(sourcePlayer.PlayerId);
            return false;
        }
        catch { return true; }
    }
}
