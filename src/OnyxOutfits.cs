using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Onyx;

internal static class OnyxOutfits
{
    private const char Sep = '\t';

    internal static int MaxColor()
    {
        try { return Palette.PlayerColors != null ? Mathf.Max(0, Palette.PlayerColors.Length - 1) : 11; }
        catch { return 11; }
    }

    internal static bool Usable(PlayerControl p) => p != null && p.Data != null && !p.Data.Disconnected && p.PlayerId < 100;

    internal static string Capture(PlayerControl src)
    {
        if (!Usable(src)) return string.Empty;
        try
        {
            var o = src.Data.DefaultOutfit;
            return string.Join(Sep.ToString(), new[]
            {
                Mathf.Clamp(o.ColorId, 0, MaxColor()).ToString(),
                Clean(o.HatId), Clean(o.SkinId), Clean(o.VisorId), Clean(o.NamePlateId), Clean(o.PetId),
            });
        }
        catch { return string.Empty; }
    }

    internal static bool Apply(PlayerControl target, string data)
    {
        if (target == null || string.IsNullOrWhiteSpace(data)) return false;
        string[] p = data.Split(Sep);
        if (p.Length < 6 || !int.TryParse(p[0], out int col)) return false;
        try
        {
            target.RpcSetColor((byte)Mathf.Clamp(col, 0, MaxColor()));
            target.RpcSetSkin(p[2] ?? string.Empty);
            target.RpcSetHat(p[1] ?? string.Empty);
            target.RpcSetVisor(p[3] ?? string.Empty);
            target.RpcSetNamePlate(p[4] ?? string.Empty);
            target.RpcSetPet(p[5] ?? string.Empty);
            return true;
        }
        catch { return false; }
    }

    internal static string Summary(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return string.Empty;
        string[] p = data.Split(Sep);
        if (p.Length < 6) return string.Empty;
        int n = 0;
        for (int i = 1; i < 6; i++) if (!string.IsNullOrEmpty(p[i])) n++;
        return OnyxText.T($"цвет +{n}", $"color +{n}");
    }

    private static string Clean(string v) => (v ?? string.Empty).Replace(Sep.ToString(), string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

    internal static string RandomHat() => RandomCosmetic(0);
    internal static string RandomSkin() => RandomCosmetic(1);
    internal static string RandomVisor() => RandomCosmetic(2);

    private static string RandomCosmetic(int kind)
    {
        try
        {
            HatManager hm = DestroyableSingleton<HatManager>.Instance;
            if (hm == null) return string.Empty;
            switch (kind)
            {
                case 0:
                {
                    var all = (Il2CppArrayBase<HatData>)(object)hm.allHats;
                    int n = all.Count;
                    return n <= 0 ? string.Empty : ((CosmeticData)all[Random.Range(0, n)]).ProdId;
                }
                case 1:
                {
                    var all = (Il2CppArrayBase<SkinData>)(object)hm.allSkins;
                    int n = all.Count;
                    return n <= 0 ? string.Empty : ((CosmeticData)all[Random.Range(0, n)]).ProdId;
                }
                default:
                {
                    var all = (Il2CppArrayBase<VisorData>)(object)hm.allVisors;
                    int n = all.Count;
                    return n <= 0 ? string.Empty : ((CosmeticData)all[Random.Range(0, n)]).ProdId;
                }
            }
        }
        catch { return string.Empty; }
    }
}
