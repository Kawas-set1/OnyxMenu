using HarmonyLib;

namespace Onyx.Patches;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class ZoomShadowPatch
{
    private static bool _wasZoomed;

    public static void Postfix(HudManager __instance)
    {
        try
        {
            if (__instance == null || __instance.ShadowQuad == null || __instance.ShadowQuad.gameObject == null) return;

            bool zoomed = VisualAssist.IsZoomActive();
            if (zoomed)
            {
                if (__instance.ShadowQuad.gameObject.activeSelf) __instance.ShadowQuad.gameObject.SetActive(false);
            }
            else if (_wasZoomed)
            {
                __instance.ShadowQuad.gameObject.SetActive(true);
            }
            _wasZoomed = zoomed;
        }
        catch { }
    }
}
