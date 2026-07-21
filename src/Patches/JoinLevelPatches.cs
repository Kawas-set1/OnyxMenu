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

    internal static void Remember(int clientId, uint raw)
    {
        if (clientId < 0 || raw == uint.MaxValue || raw > 9999u) return;
        if (raw == 0u && Cache.ContainsKey(clientId)) return;
        Cache[clientId] = raw;
    }

    internal static void RememberRpc(PlayerControl pc, uint raw)
    {
        if (pc == null) return;
        try { Remember(pc.OwnerId, raw); } catch { }
    }

    private static bool TryClient(PlayerControl pc, out uint raw)
    {
        raw = 0u;
        try
        {
            if (pc == null || AmongUsClient.Instance == null) return false;
            ClientData c = AmongUsClient.Instance.GetClientFromCharacter(pc);
            if (c == null) return false;
            uint lv = c.PlayerLevel;
            if (lv == uint.MaxValue || lv > 9999u) return false;
            raw = lv;
            return true;
        }
        catch { return false; }
    }

    internal static bool TryRaw(int clientId, PlayerControl pc, out uint raw)
    {
        if (TryGet(clientId, out raw)) return true;
        if (TryClient(pc, out raw)) { Remember(clientId, raw); return true; }

        try
        {
            if (pc != null && pc.Data != null)
            {
                uint lv = pc.Data.PlayerLevel;
                if (lv > 0u && lv <= 9999u) { raw = lv; return true; }
            }
        }
        catch { }

        raw = 0u;
        return false;
    }

    internal static string Display(int clientId, PlayerControl pc)
        => TryRaw(clientId, pc, out uint raw) ? (raw + 1u).ToString() : "?";

    internal static string Display(PlayerControl pc)
    {
        int id = -1;
        try { if (pc != null) id = pc.OwnerId; } catch { }
        return Display(id, pc);
    }

    internal static bool TryLevel(int clientId, PlayerControl pc, out int level)
    {
        if (TryRaw(clientId, pc, out uint raw)) { level = (int)(raw + 1u); return true; }
        level = 0;
        return false;
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

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class OnyxLevelRpcPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != 38 || __instance == null || reader == null) return;
        MessageReader copy = null;
        try
        {
            copy = MessageReader.Get(reader);
            uint raw = copy.ReadPackedUInt32();
            OnyxJoinLevels.RememberRpc(__instance, raw);
        }
        catch { }
        finally { try { copy?.Recycle(); } catch { } }
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Deserialize))]
internal static class OnyxLevelInfoPatch
{
    public static void Postfix(NetworkedPlayerInfo __instance)
    {
        if (__instance == null) return;
        try
        {
            if (__instance.IsIncomplete) return;
            uint raw = __instance.PlayerLevel;
            if (raw == 0u) return;
            OnyxJoinLevels.Remember(__instance.ClientId, raw);
        }
        catch { }
    }
}
