using HarmonyLib;

namespace Onyx;

[HarmonyPatch(typeof(PlayerPhysics), "FixedUpdate")]
internal static class OnyxSeeThrough
{
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
            if (p.cosmetics == null) return;
            if (me.Data.IsDead) return;

            if (p.inVent)
            {
                if (vents && !p.Visible)
                {
                    p.Visible = true;
                    p.invisibilityAlpha = 0.5f;
                    p.cosmetics.SetPhantomRoleAlpha(0.5f);
                    ShowName(p, true);
                }
                else if (!vents && p.invisibilityAlpha == 0.5f)
                {
                    p.Visible = false;
                    p.invisibilityAlpha = 0f;
                    p.cosmetics.SetPhantomRoleAlpha(0f);
                    ShowName(p, false);
                }
            }
            else if (ghosts && p.Data.IsDead && p != me)
            {
                p.Visible = true;
            }
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
