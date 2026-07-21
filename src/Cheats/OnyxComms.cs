using HarmonyLib;

namespace Onyx;

internal static class OnyxComms
{
    internal static bool On => OnyxConfig.CommsBypass != null && OnyxConfig.CommsBypass.Value;
}

[HarmonyPatch(typeof(PlayerControl), "AreCommsAffected")]
internal static class OnyxCommsTaskPatch
{
    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (!__result || !OnyxComms.On) return;
        if (__instance != PlayerControl.LocalPlayer) return;
        __result = false;
    }
}

[HarmonyPatch(typeof(RoleBehaviour), "CommsSabotaged", MethodType.Getter)]
internal static class OnyxCommsRolePatch
{
    public static void Postfix(RoleBehaviour __instance, ref bool __result)
    {
        if (!__result || !OnyxComms.On) return;
        if (__instance == null || __instance.Player != PlayerControl.LocalPlayer) return;
        __result = false;
    }
}
