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
            __instance.text.text = "<color=#FF8F24><b>OnyxMenu</b> by Kawasaki</color>";
        }
        catch { }
    }
}
