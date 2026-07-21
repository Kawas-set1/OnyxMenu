using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Onyx.Patches;

public sealed class OnyxModStampDriver : MonoBehaviour
{
    private ModManager _mgr;
    private float _nextLookup;
    private bool _shown;

    public void LateUpdate()
    {
        if (LobbyBehaviour.Instance != null) { _shown = false; return; }

        if (_mgr == null && Time.unscaledTime >= _nextLookup)
        {
            _mgr = Object.FindObjectOfType<ModManager>();
            _nextLookup = Time.unscaledTime + 0.75f;
            _shown = false;
        }

        if (_mgr == null) return;
        if (OnyxConfig.HideModStamp.Value) { OnyxModStamp.Hide(_mgr); _shown = false; }
        else if (!_shown) { _shown = true; OnyxModStamp.Show(_mgr); }
    }
}

internal static class OnyxModStamp
{
    private static int _lastShowFrame = -1;

    internal static void Show(ModManager mgr)
    {
        if (mgr == null) return;
        if (OnyxConfig.HideModStamp.Value) { Hide(mgr); return; }
        if (_lastShowFrame == Time.frameCount) return;
        _lastShowFrame = Time.frameCount;

        try
        {
            SpriteRenderer stamp = mgr.ModStamp;
            if (stamp != null && ((Renderer)stamp).enabled) return;
        }
        catch { }

        mgr.ShowModStamp();
    }

    internal static void Hide(ModManager mgr)
    {
        if (mgr == null) return;
        try
        {
            SpriteRenderer stamp = mgr.ModStamp;
            if (stamp != null && ((Renderer)stamp).enabled) ((Renderer)stamp).enabled = false;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ModManager), "LateUpdate")]
internal static class OnyxModStampPatch
{
    public static void Postfix(ModManager __instance)
    {
        try
        {
            if (__instance == null) return;
            if (OnyxConfig.HideModStamp.Value) OnyxModStamp.Hide(__instance);
            else OnyxModStamp.Show(__instance);
        }
        catch { }
    }
}
