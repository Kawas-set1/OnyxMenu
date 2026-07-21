using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Onyx;

internal static class OnyxMeetingTools
{
    private static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    internal static string CloseVoting()
    {
        try
        {
            if (!Host) return OnyxText.T("Только хост.", "Host only.");
            MeetingHud m = MeetingHud.Instance;
            if (m == null) return OnyxText.T("Нет собрания.", "No meeting.");
            int state = (int)m.CurrentState;
            if (state != 2 && state != 3) return OnyxText.T("Голосование не активно.", "Voting isn't active.");
            m.ForceSkipAll();
            return OnyxText.T("Голосование закрыто.", "Voting closed.");
        }
        catch { return OnyxText.T("Не удалось закрыть.", "Failed to close."); }
    }

    internal static string CloseMeeting()
    {
        try
        {
            if (!Host) return OnyxText.T("Только хост.", "Host only.");
            MeetingHud m = MeetingHud.Instance;
            if (m == null) return OnyxText.T("Нет собрания.", "No meeting.");
            m.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0), (NetworkedPlayerInfo)null, true);
            m.Close();
            m.RpcClose();
            return OnyxText.T("Собрание закрыто (без выгона).", "Meeting closed (no ejection).");
        }
        catch { return OnyxText.T("Не удалось закрыть.", "Failed to close."); }
    }
}
