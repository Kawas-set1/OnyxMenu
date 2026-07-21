using HarmonyLib;
using UnityEngine;

namespace Onyx;

internal static class OnyxLagComp
{
    private static int _hold;

    private static bool On => OnyxConfig.LagComp != null && OnyxConfig.LagComp.Value;

    [HarmonyPatch(typeof(CustomNetworkTransform), "Serialize")]
    private static class Suppress
    {
        private static bool Prefix(CustomNetworkTransform __instance, bool __1, ref bool __result)
        {
            if (!On || __1) return true;
            if (__instance == null || __instance.myPlayer != PlayerControl.LocalPlayer) return true;
            if (MeetingHud.Instance != null) return true;

            if (OnyxConfig.LagCompFreeze.Value)
            {
                __result = false;
                return false;
            }

            if (OnyxConfig.LagCompJitter.Value)
            {
                if (_hold > 0)
                {
                    _hold--;
                    __result = false;
                    return false;
                }
                int lo = Mathf.Clamp(OnyxConfig.LagCompJitterMin.Value, 1, 30);
                int hi = Mathf.Clamp(OnyxConfig.LagCompJitterMax.Value, lo, 30);
                _hold = Random.Range(lo, hi + 1);
            }
            return true;
        }
    }
}
