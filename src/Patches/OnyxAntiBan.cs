using Hazel;
using HarmonyLib;
using InnerNet;

namespace Onyx.Patches;

[HarmonyPatch(typeof(ShipStatus), "HandleRpc")]
internal static class OnyxAntiBan
{
    private static bool Prefix(byte callId, MessageReader reader)
    {
        if (callId != 35 || reader == null) return true;

        InnerNetClient net;
        try { net = (InnerNetClient)AmongUsClient.Instance; }
        catch { return true; }
        if (net == null) return true;

        bool host = false;
        try { host = net.AmHost; } catch { }

        if (!host)
        {
            if (Hostile(reader, out _))
            {
                OnyxToast.Push("Анти-бан", "Погашен краш-пакет.", 2.5f, OnyxNotifyKind.Warning);
                return false;
            }
            return true;
        }

        if (OnyxConfig.AntiBanHost == null || !OnyxConfig.AntiBanHost.Value) return true;

        if (Hostile(reader, out PlayerControl src) && src != null && src != PlayerControl.LocalPlayer)
        {
            int cid = ((InnerNetObject)src).OwnerId;
            if (cid >= 0 && cid != net.ClientId && cid != net.HostId)
            {
                OnyxAccess.Act(net, cid, OnyxConfig.AntiBanHostAction.Value, OnyxAccess.ClientName(net, cid), "краш-бан пакет");
                return false;
            }
        }
        return true;
    }

    private static bool Hostile(MessageReader reader, out PlayerControl src)
    {
        src = null;
        try
        {
            MessageReader r = MessageReader.Get(reader);
            if ((int)(SystemTypes)r.ReadByte() != 37) return false;
            src = r.ReadNetObject<PlayerControl>();
            r.ReadUInt16();
            int op = r.ReadByte();
            return op == 5 || op <= 0;
        }
        catch { return false; }
    }
}
