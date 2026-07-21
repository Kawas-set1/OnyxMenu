using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;

namespace Onyx.Patches;

internal static class SkipAnim
{
    internal static IEnumerator Nothing() { yield break; }

    internal static void ThawDead()
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me != null) me.moveable = true;
        }
        catch { }
    }

    internal static void DropOverlay(KillOverlay overlay)
    {
        try
        {
            if (overlay == null) return;
            overlay.StopAllCoroutines();
            if (overlay.gameObject != null && overlay.gameObject.activeSelf)
            {
                overlay.gameObject.SetActive(false);
                overlay.gameObject.SetActive(true);
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ShhhBehaviour), nameof(ShhhBehaviour.PlayAnimation))]
internal static class ShhhSkipPatch
{
    public static bool Prefix(ShhhBehaviour __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (OnyxConfig.SkipShhh == null || !OnyxConfig.SkipShhh.Value || __instance == null) return true;
        try { __instance.gameObject.SetActive(false); } catch { }
        __result = SkipAnim.Nothing().WrapToIl2Cpp();
        return false;
    }
}

[HarmonyPatch]
internal static class KillAnimSkipPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo m in typeof(KillOverlay).GetMethods())
            if (m.Name == nameof(KillOverlay.ShowKillAnimation) && m.ReturnType == typeof(void))
                yield return m;
    }

    public static bool Prefix(KillOverlay __instance)
    {
        if (OnyxConfig.SkipKillAnim == null || !OnyxConfig.SkipKillAnim.Value) return true;
        SkipAnim.DropOverlay(__instance);
        SkipAnim.ThawDead();
        return false;
    }
}

[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowAll))]
internal static class KillAnimShowAllSkipPatch
{
    public static bool Prefix(KillOverlay __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (OnyxConfig.SkipKillAnim == null || !OnyxConfig.SkipKillAnim.Value) return true;
        SkipAnim.DropOverlay(__instance);
        SkipAnim.ThawDead();
        __result = SkipAnim.Nothing().WrapToIl2Cpp();
        return false;
    }
}
