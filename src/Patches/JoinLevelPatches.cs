using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;

namespace Onyx.Patches;

internal static class OnyxJoinLevels
{
    private static readonly Dictionary<int, uint> Cache = new Dictionary<int, uint>();

    internal static bool TryGet(int clientId, out uint raw)
    {
        raw = 0u;
        return clientId >= 0 && Cache.TryGetValue(clientId, out raw) && raw <= 9999u;
    }

    internal static void Inspect(InnerNetClient net, MessageReader reader)
    {
        if (net == null || reader == null || reader.Tag != 1 || !net.AmHost) return;

        MessageReader copy = null;
        MessageReader plat = null;
        try
        {
            copy = MessageReader.Get(reader);
            int gameId = copy.ReadInt32();
            if (gameId != net.GameId) return;
            int clientId = copy.ReadInt32();
            if (clientId == net.ClientId) return;

            copy.ReadInt32();
            copy.ReadString();
            plat = copy.ReadMessage();
            int platformId = plat.Tag;
            plat.ReadString();
            if (platformId == 4 || platformId == 9) plat.ReadUInt64();
            else if (platformId == 10) plat.ReadUInt64();

            uint level = copy.ReadPackedUInt32();
            if (level != uint.MaxValue && level <= 9999u) Cache[clientId] = level;
        }
        catch { }
        finally
        {
            try { plat?.Recycle(); } catch { }
            try { copy?.Recycle(); } catch { }
        }
    }
}

[HarmonyPatch(typeof(InnerNetClient), "HandleMessage")]
internal static class OnyxJoinLevelPatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix(InnerNetClient __instance, [HarmonyArgument(0)] MessageReader reader)
    {
        try { OnyxJoinLevels.Inspect(__instance, reader); }
        catch { }
    }
}
