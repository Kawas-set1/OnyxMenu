using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Onyx;

public sealed class OnyxEventNotify : MonoBehaviour
{
    private static readonly int[] SabSys = { 3, 8, 7, 14, 15 };
    private static readonly bool[] SabPrev = new bool[5];

    internal static bool On => OnyxConfig.EventNotify != null && OnyxConfig.EventNotify.Value;

    internal static void Fire(string ru, string en, OnyxNotifyKind kind)
    {
        string msg = OnyxText.T(ru, en);
        OnyxEventLog.Add(msg, kind);
        if (!On) return;
        OnyxToast.Push(OnyxText.T("Событие", "Event"), msg, 3f, kind);
        if (OnyxConfig.EventNotifyChat != null && OnyxConfig.EventNotifyChat.Value)
        {
            try
            {
                if (HudManager.Instance != null && HudManager.Instance.Chat != null && PlayerControl.LocalPlayer != null)
                    HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
            }
            catch { }
        }
    }

    internal static string ByClient(int clientId)
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.OwnerId == clientId && pc.Data != null)
                    return pc.Data.PlayerName;
        }
        catch { }
        return "?";
    }

    internal static string PName(PlayerControl pc)
    {
        try { return pc != null && pc.Data != null ? pc.Data.PlayerName : "?"; }
        catch { return "?"; }
    }

    private static bool Mine(int clientId)
    {
        try { return PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.OwnerId == clientId; }
        catch { return false; }
    }

    public void Update()
    {
        if (OnyxConfig.NotifySabotage == null || !OnyxConfig.NotifySabotage.Value
            || ShipStatus.Instance == null || MeetingHud.Instance != null)
        {
            for (int i = 0; i < SabPrev.Length; i++) SabPrev[i] = false;
            return;
        }

        for (int i = 0; i < SabSys.Length; i++)
        {
            bool now = IsSab(SabSys[i]);
            if (now && !SabPrev[i])
                Fire("⚠ Саботаж: " + SabRu(SabSys[i]), "⚠ Sabotage: " + SabEn(SabSys[i]), OnyxNotifyKind.Danger);
            SabPrev[i] = now;
        }
    }

    private static bool IsSab(int sysType)
    {
        try
        {
            var sys = ShipStatus.Instance.Systems[(SystemTypes)sysType];
            if (sys == null) return false;
            var act = ((Il2CppObjectBase)sys).TryCast<IActivatable>();
            return act != null && act.IsActive;
        }
        catch { return false; }
    }

    private static string SabRu(int s) => s == 3 || s == 15 ? "Реактор" : s == 8 ? "Кислород" : s == 7 ? "Свет" : s == 14 ? "Связь" : "Саботаж";
    private static string SabEn(int s) => s == 3 || s == 15 ? "Reactor" : s == 8 ? "Oxygen" : s == 7 ? "Lights" : s == 14 ? "Comms" : "Sabotage";

    [HarmonyPatch(typeof(VoteBanSystem), "AddVote")]
    private static class VkPatch
    {
        public static void Postfix(int __0, int __1)
        {
            if (OnyxConfig.NotifyVotekick == null || !OnyxConfig.NotifyVotekick.Value) return;
            if (Mine(__0)) return;
            string a = ByClient(__0), b = ByClient(__1);
            Fire($"Войткик: {a} → {b}", $"Votekick: {a} → {b}", OnyxNotifyKind.Warning);
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer), new Type[] { typeof(PlayerControl), typeof(MurderResultFlags) })]
    private static class KillPatch
    {
        public static void Postfix(PlayerControl __instance, PlayerControl target)
        {
            if (OnyxConfig.NotifyKill == null || !OnyxConfig.NotifyKill.Value || __instance == null || target == null) return;
            if (Mine(__instance.OwnerId)) return;
            if (target.Data == null || !target.Data.IsDead) return;
            string k = PName(__instance), v = PName(target);
            Fire($"Убийство: {k} → {v}", $"Kill: {k} → {v}", OnyxNotifyKind.Danger);
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    private static class MeetPatch
    {
        public static void Postfix()
        {
            if (OnyxConfig.NotifyMeeting == null || !OnyxConfig.NotifyMeeting.Value) return;
            Fire("Началось собрание", "Meeting started", OnyxNotifyKind.Info);
        }
    }

    [HarmonyPatch(typeof(ExileController), "Begin")]
    private static class EjectPatch
    {
        public static void Postfix(ExileController __instance)
        {
            if (OnyxConfig.NotifyEject == null || !OnyxConfig.NotifyEject.Value || __instance == null) return;
            try
            {
                NetworkedPlayerInfo ex = __instance.initData.networkedPlayer;
                string who = ex != null ? ex.PlayerName : OnyxText.T("никто", "no one");
                Fire($"Изгнан: {who}", $"Ejected: {who}", OnyxNotifyKind.Warning);
            }
            catch { }
        }
    }
}
