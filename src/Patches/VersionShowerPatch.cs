using HarmonyLib;

namespace Onyx.Patches;

[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
internal static class OnyxVersionShowerPatch
{
    public static void Postfix(VersionShower __instance)
    {
        try
        {
            if (__instance == null || __instance.text == null) return;
            __instance.text.text = "<color=#4CA2FF><b>Onyx Menu</b> by Kawasaki</color>";
        }
        catch { }
    }
}
