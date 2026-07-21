using AmongUs.Data;
using HarmonyLib;
using UnityEngine;

namespace Onyx.Patches;

[HarmonyPatch(typeof(PlatformSpecificData), "Serialize")]
internal static class PlatformSpoofOutboundPatch
{

    private static readonly Platforms[] PlatformValues =
    {
        (Platforms)1,
        (Platforms)2,
        (Platforms)3,
        (Platforms)4,
        (Platforms)5,
        (Platforms)6,
        (Platforms)7,
        (Platforms)8,
        (Platforms)9,
        (Platforms)10,
        (Platforms)112,
    };

    public static void Prefix(PlatformSpecificData __instance)
    {
        if (__instance == null) return;
        if (!(OnyxConfig.SpoofPlatformEnabled?.Value ?? false)) return;
        try
        {
            int idx = Mathf.Clamp(OnyxConfig.SpoofPlatformIndex?.Value ?? 1, 0, PlatformValues.Length - 1);
            Platforms platform = PlatformValues[idx];
            __instance.Platform = platform;

            switch ((int)platform)
            {
                case 4:
                    __instance.XboxPlatformId = 2584878536129841uL;
                    break;
                case 8:
                    __instance.PlatformName = "StargazerS";
                    __instance.XboxPlatformId = 0uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
                case 9:
                    __instance.PlatformName = "CosmicVoid7";
                    __instance.XboxPlatformId = 2584878536129841uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
                case 10:
                    __instance.PlatformName = "";
                    __instance.XboxPlatformId = 0uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
                default:
                    __instance.PlatformName = "TESTNAME";
                    __instance.XboxPlatformId = 0uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
            }
        }
        catch { }
    }
}

public sealed class OnyxSpoofDriver : MonoBehaviour
{
    private float _nextApply;

    public void Update()
    {
        if (!(OnyxConfig.SpoofLevelEnabled?.Value ?? false)) return;
        float now = Time.realtimeSinceStartup;
        if (now < _nextApply) return;
        _nextApply = now + 3f;
        ApplyLevel();
    }

    private static void ApplyLevel()
    {
        int display = Mathf.Clamp(OnyxConfig.SpoofLevelValue?.Value ?? 100, 1, 9999);
        uint raw = (uint)(display - 1);
        try
        {
            if (DataManager.Player.stats.level != raw)
            {
                DataManager.Player.stats.level = raw;
                ((AbstractSaveData)DataManager.Player).Save();
            }
        }
        catch
        {
            try
            {
                if (DataManager.Player.Stats.Level != raw)
                {
                    DataManager.Player.Stats.Level = raw;
                    ((AbstractSaveData)DataManager.Player).Save();
                }
            }
            catch { }
        }
    }

    internal static void ApplyNow() => ApplyLevel();
}

[HarmonyPatch(typeof(SystemInfo), nameof(SystemInfo.deviceUniqueIdentifier), MethodType.Getter)]
internal static class DeviceIdSpoofPatch
{
    private static string fake;

    public static void Postfix(ref string __result)
    {
        if (!(OnyxConfig.SpoofDeviceId?.Value ?? false)) return;
        fake ??= System.Guid.NewGuid().ToString("N");
        __result = fake;
    }
}
