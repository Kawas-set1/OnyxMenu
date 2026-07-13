using UnityEngine;

namespace Onyx;

public sealed class OnyxChatSender : MonoBehaviour
{
    internal static string Message = "";
    internal static bool Spamming;
    private static float _next;

    internal static void SendNow()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || string.IsNullOrWhiteSpace(Message)) return;
        try { me.RpcSendChat(Message); } catch { }
    }

    public void Update()
    {
        if (!Spamming) return;
        if (PlayerControl.LocalPlayer == null || string.IsNullOrWhiteSpace(Message)) { Spamming = false; return; }
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Mathf.Max(1.5f, OnyxConfig.ChatSpamDelay != null ? OnyxConfig.ChatSpamDelay.Value : 2.5f);
        SendNow();
    }
}
