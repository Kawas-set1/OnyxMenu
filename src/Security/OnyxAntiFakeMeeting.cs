using HarmonyLib;
using UnityEngine;

namespace Onyx;

internal static class OnyxAntiFakeMeeting
{
    private static float lastNote;

    internal static bool On => OnyxConfig.BlockFakeMeetings != null && OnyxConfig.BlockFakeMeetings.Value;

    internal static bool Illegal()
    {
        try { return ShipStatus.Instance == null || LobbyBehaviour.Instance != null; }
        catch { return false; }
    }

    internal static void Kill(MeetingHud hud)
    {
        try { if (hud != null) Object.Destroy(hud.gameObject); } catch { }

        if (Time.unscaledTime - lastNote < 1f) return;
        lastNote = Time.unscaledTime;
        OnyxSecurityNotify.Fire("Заблокирован фейк-митинг в лобби", "Blocked a fake lobby meeting");
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class OnyxFakeMeetingStartPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        if (!OnyxAntiFakeMeeting.On || !OnyxAntiFakeMeeting.Illegal()) return true;
        OnyxAntiFakeMeeting.Kill(__instance);
        return false;
    }
}
