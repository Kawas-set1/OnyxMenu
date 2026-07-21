using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Onyx;

internal static class OnyxSpawnFlood
{
    private const int Cap = 120;

    private static int frame = -1;
    private static int count;
    private static float lastNote;

    internal static bool On => OnyxConfig.BlockFakeMeetings != null && OnyxConfig.BlockFakeMeetings.Value;

    internal static bool Allow()
    {
        int f = Time.frameCount;
        if (f != frame) { frame = f; count = 0; }
        count++;
        return count <= Cap;
    }

    internal static Il2CppSystem.Collections.IEnumerator Drop()
    {
        if (Time.unscaledTime - lastNote >= 1f)
        {
            lastNote = Time.unscaledTime;
            OnyxSecurityNotify.Fire("Обрезан спавн-флуд", "Trimmed a spawn flood");
        }
        return Empty().WrapToIl2Cpp();
    }

    private static IEnumerator Empty() { yield break; }
}

[HarmonyPatch(typeof(InnerNetClient), "CoHandleSpawn")]
internal static class OnyxSpawnFloodPatch
{
    public static bool Prefix(ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (!OnyxSpawnFlood.On || OnyxSpawnFlood.Allow()) return true;
        __result = OnyxSpawnFlood.Drop();
        return false;
    }
}
