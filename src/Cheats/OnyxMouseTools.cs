using UnityEngine;

namespace Onyx;

public sealed class OnyxMouseTools : MonoBehaviour
{
    private static PlayerControl _sel;
    private float _lastPick = -99f;
    private static readonly Color Outline = new Color(0.30f, 0.62f, 1f, 1f);

    internal static PlayerControl Selected => _sel;

    public void Update()
    {
        bool tp = OnyxConfig.MouseTeleport.Value;
        bool sel = OnyxConfig.MouseSelect.Value;
        if ((!tp && !sel) || AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null)
        {
            Clear();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        if (tp && Input.GetMouseButton(1))
        {
            Vector2 pos = cam.ScreenToWorldPoint(Input.mousePosition);
            try { PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(pos); } catch { }
        }

        if (!sel) { Clear(); return; }

        if (_sel != null && (_sel.Data == null || _sel.Data.Disconnected)) Clear();
        if (Input.GetKeyDown(KeyCode.Escape)) { Clear(); return; }

        if (Input.GetMouseButtonDown(0) && Time.unscaledTime - _lastPick > 0.2f)
        {
            _lastPick = Time.unscaledTime;
            Pick(cam);
        }

        Resize();
    }

    private void Pick(Camera cam)
    {
        Vector2 m = cam.ScreenToWorldPoint(Input.mousePosition);
        PlayerControl best = null;
        float bestD = 1.6f;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (p == null || p.Data == null || p.Data.Disconnected) continue;
            float d = Vector2.Distance(p.transform.position, m);
            if (d < bestD) { bestD = d; best = p; }
        }

        if (best == null) return;
        if (best == _sel) { Clear(); return; }
        Clear();
        _sel = best;
        Outlined(best, true);
    }

    private static void Resize()
    {
        if (_sel == null) return;
        float w = Input.mouseScrollDelta.y;
        if (Mathf.Abs(w) < 0.01f) return;
        Transform t = _sel.transform;
        float s = Mathf.Clamp(t.localScale.x + (w > 0f ? 0.15f : -0.15f), 0.25f, 2f);
        t.localScale = new Vector3(s, s, 1f);
    }

    internal static void Clear()
    {
        if (_sel == null) return;
        Outlined(_sel, false);
        _sel = null;
    }

    private static void Outlined(PlayerControl p, bool on)
    {
        try
        {
            CosmeticsLayer c = p != null ? p.cosmetics : null;
            if (c == null) return;
            if (on)
            {
                c.SetOutline(true, new Il2CppSystem.Nullable<Color>(Outline));
            }
            else
            {
                c.SetOutline(true, new Il2CppSystem.Nullable<Color>(Color.clear));
                c.SetOutline(false, (Il2CppSystem.Nullable<Color>)null);
            }
        }
        catch { }
    }
}
