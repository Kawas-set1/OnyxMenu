using HarmonyLib;

namespace Onyx.Patches;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class OnyxChatMutePatch
{
    public static bool Prefix([HarmonyArgument(0)] PlayerControl sourcePlayer)
    {
        try
        {
            if (sourcePlayer == null || sourcePlayer.Data == null) return true;
            if (sourcePlayer == PlayerControl.LocalPlayer) return true;
            return !OnyxMuteList.IsMuted(sourcePlayer.Data.FriendCode);
        }
        catch { return true; }
    }
}
