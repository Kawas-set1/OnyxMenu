using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Onyx.Patches;

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
internal static class RevealVotesPatch
{
    internal static readonly HashSet<byte> shown = new HashSet<byte>();

    public static void Postfix(MeetingHud __instance)
    {
        if (!OnyxConfig.RevealVotes.Value || __instance == null || (int)__instance.state >= 4) return;

        try
        {
            foreach (PlayerVoteArea voter in __instance.playerStates)
            {
                if (voter == null || shown.Contains(voter.TargetPlayerId)) continue;

                var pick = voter.VotedFor;
                if (pick == PlayerVoteArea.HasNotVoted || pick == PlayerVoteArea.MissedVote || pick == PlayerVoteArea.DeadVote) continue;

                NetworkedPlayerInfo info = GameData.Instance.GetPlayerById(voter.TargetPlayerId);
                if (info == null || info.Disconnected) continue;
                shown.Add(voter.TargetPlayerId);

                if (pick == PlayerVoteArea.SkippedVote)
                {
                    if (__instance.SkippedVoting != null) __instance.BloopAVoteIcon(info, 0, __instance.SkippedVoting.transform);
                    continue;
                }

                foreach (PlayerVoteArea target in __instance.playerStates)
                    if (target != null && target.TargetPlayerId == pick) { __instance.BloopAVoteIcon(info, 0, target.transform); break; }
            }

            foreach (PlayerVoteArea area in __instance.playerStates)
            {
                if (area == null) continue;
                VoteSpreader spread = area.transform.GetComponent<VoteSpreader>();
                if (spread == null) continue;
                foreach (SpriteRenderer sr in spread.Votes) sr.gameObject.SetActive(true);
            }
            if (__instance.SkippedVoting != null) __instance.SkippedVoting.SetActive(true);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
internal static class RevealVotesResetPatch
{
    public static void Prefix(MeetingHud __instance)
    {
        RevealVotesPatch.shown.Clear();
        if (__instance == null) return;

        try
        {
            foreach (PlayerVoteArea area in __instance.playerStates)
            {
                if (area == null) continue;
                VoteSpreader spread = area.transform.GetComponent<VoteSpreader>();
                if (spread == null || spread.Votes.Count == 0) continue;
                foreach (SpriteRenderer sr in spread.Votes) UnityEngine.Object.DestroyImmediate(sr.gameObject);
                spread.Votes.Clear();
            }
        }
        catch { }
    }
}
