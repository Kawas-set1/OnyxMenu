using System.Collections.Generic;
using HarmonyLib;

namespace Onyx;

[HarmonyPatch(typeof(PlayerPhysics), "FixedUpdate")]
internal static class OnyxSeeThrough
{
    private static readonly Dictionary<byte, float> faded = new Dictionary<byte, float>();

    internal static void Reset() => faded.Clear();

    private static void Postfix(PlayerPhysics __instance)
    {
        bool vents = OnyxConfig.SeeVents.Value;
        bool ghosts = OnyxConfig.SeeGhosts.Value;
        if (!vents && !ghosts) return;
        if (ShipStatus.Instance == null) return;

        try
        {
            PlayerControl p = __instance.myPlayer;
            PlayerControl me = PlayerControl.LocalPlayer;
            if (p == null || me == null || p.Data == null || me.Data == null) return;
            if (p.cosmetics == null || p == me || me.Data.IsDead) return;

            if (vents && p.inVent)
            {
                if (!faded.ContainsKey(p.PlayerId)) faded[p.PlayerId] = p.invisibilityAlpha;
                if (!p.Visible)
                {
                    p.Visible = true;
                    p.invisibilityAlpha = 0.5f;
                    p.cosmetics.SetPhantomRoleAlpha(0.5f);
                    ShowName(p, true);
                }
            }
            else if (faded.TryGetValue(p.PlayerId, out float a))
            {
                faded.Remove(p.PlayerId);
                p.invisibilityAlpha = a;
                p.cosmetics.SetPhantomRoleAlpha(1f);
                ShowName(p, true);
            }

            if (ghosts && p.Data.IsDead) p.Visible = true;
        }
        catch { }
    }

    private static void ShowName(PlayerControl p, bool on)
    {
        try
        {
            var t = p.cosmetics.nameText;
            if (t != null) t.gameObject.SetActive(on);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ShipStatus), "OnEnable")]
internal static class OnyxSeeThroughReset
{
    private static void Postfix() => OnyxSeeThrough.Reset();
}

[HarmonyPatch(typeof(PlayerControl), "CalculatedAlpha", MethodType.Getter)]
internal static class OnyxSeePhantoms
{
    private static void Postfix(PlayerControl __instance, ref float __result)
    {
        if (OnyxConfig.SeePhantoms == null || !OnyxConfig.SeePhantoms.Value) return;
        if (__instance == null || __instance == PlayerControl.LocalPlayer) return;
        if (__result >= 0.5f) return;
        __result = 0.5f;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TurnOnProtection))]
internal static class OnyxSeeProtections
{
    private static void Prefix(ref bool visible)
    {
        if (OnyxConfig.SeeProtections != null && OnyxConfig.SeeProtections.Value) visible = true;
    }
}
