using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Onyx.Patches;

[HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
internal static class OnyxAccessJoinPatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData client)
        => OnyxAccess.Enforce((InnerNetClient)__instance, client);
}

[HarmonyPatch(typeof(VoteBanSystem), "AddVote")]
internal static class OnyxVoteKickPatch
{
    public static bool Prefix([HarmonyArgument(0)] int srcClient, [HarmonyArgument(1)] int clientId)
    {
        if (!OnyxConfig.VoteKickProtect.Value || AmongUsClient.Instance == null) return true;
        var net = (InnerNetClient)AmongUsClient.Instance;
        if (!net.AmHost) return true;

        string action = OnyxConfig.VoteKickAction.Value;
        if (!string.Equals(action, "Null", System.StringComparison.OrdinalIgnoreCase))
            OnyxAccess.Act(net, srcClient, action, OnyxAccess.ClientName(net, srcClient), $"войт-кик → {OnyxAccess.ClientName(net, clientId)}");

        return false;
    }
}

[HarmonyPatch(typeof(InnerNetClient), "KickPlayer")]
internal static class OnyxKickSelfGuardPatch
{
    public static bool Prefix(InnerNetClient __instance, int clientId)
    {
        if (__instance == null || !__instance.AmHost) return true;
        return clientId != __instance.ClientId && clientId != __instance.HostId;
    }
}

public sealed class OnyxAccessGuard : MonoBehaviour
{
    private float _next;

    public void Update()
    {
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + 0.6f;

        if (AmongUsClient.Instance == null || LobbyBehaviour.Instance == null) return;
        var net = (InnerNetClient)AmongUsClient.Instance;
        if (!net.AmHost || net.allClients == null) return;
        bool fg = OnyxConfig.KickFortegreen.Value;
        bool colorRes = OnyxConfig.ColorReservationsEnabled != null && OnyxConfig.ColorReservationsEnabled.Value;
        if (!fg && !colorRes && !OnyxConfig.AccessBanEnabled.Value && !OnyxConfig.AccessWhitelistOnly.Value
            && !OnyxConfig.AccessNickBanEnabled.Value && !OnyxConfig.MinLevelEnabled.Value && !OnyxConfig.MaxLevelEnabled.Value) return;

        try
        {
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c == null) continue;
                if (colorRes) OnyxColorReservations.TryApplyOnJoin(c.Character);
                if (fg && c.Id != net.ClientId && c.Id != net.HostId && IsFortegreen(c))
                {
                    OnyxAccess.Kick(net, c.Id, false);
                    OnyxToast.Push("Fortegreen", OnyxAccess.SafeName(c), 2.5f, OnyxNotifyKind.Warning);
                    continue;
                }
                OnyxAccess.Enforce(net, c);
            }
        }
        catch { }
    }

    private static bool IsFortegreen(ClientData c)
    {
        try { return c.Character != null && c.Character.CurrentOutfit != null && c.Character.CurrentOutfit.ColorId == 18; }
        catch { return false; }
    }
}
