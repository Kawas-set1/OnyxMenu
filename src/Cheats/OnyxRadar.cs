using System.Collections.Generic;
using UnityEngine;

namespace Onyx;

public sealed class OnyxRadar : MonoBehaviour
{
    private const float W = 236f, H = 212f, Pad = 14f, Head = 22f;

    private static GUIStyle _title;
    private static Texture2D _dot;
    private static readonly Dictionary<byte, List<(Vector2 w, float t)>> _trail = new Dictionary<byte, List<(Vector2, float)>>();

    private static Vector2 _min, _max;
    private static int _boundsMap = -999;

    private static float _sc = 1f, _al = 0.93f;
    private static bool _drag;
    private static Vector2 _dragOff;
    private static float _rx, _ry;

    internal void DrawGui()
    {
        if (OnyxConfig.Radar == null || !OnyxConfig.Radar.Value) return;
        if (ShipStatus.Instance == null || PlayerControl.LocalPlayer == null) return;
        if (MeetingHud.Instance != null || ExileController.Instance != null) return;
        if (!Bounds()) return;

        _sc = Mathf.Clamp((OnyxConfig.RadarSize != null ? OnyxConfig.RadarSize.Value : 100) / 100f, 0.6f, 1.8f);
        _al = Mathf.Clamp((OnyxConfig.RadarOpacity != null ? OnyxConfig.RadarOpacity.Value : 93) / 100f, 0.3f, 1f);
        float w = W * _sc, h = H * _sc;

        if (!_drag) { _rx = OnyxConfig.RadarX.Value; _ry = OnyxConfig.RadarY.Value; }
        _rx = Mathf.Clamp(_rx, 0f, Mathf.Max(0f, Screen.width - w));
        _ry = Mathf.Clamp(_ry, 0f, Mathf.Max(0f, Screen.height - h));

        var box = new Rect(_rx, _ry, w, h);
        float pad = Pad * _sc, head = Head * _sc;
        var inner = new Rect(box.x + pad, box.y + head + 4f * _sc, box.width - 2f * pad, box.height - head - 4f * _sc - pad);

        Event e = Event.current;
        if (e != null)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && box.Contains(e.mousePosition))
            {
                _drag = true;
                _dragOff = new Vector2(e.mousePosition.x - box.x, e.mousePosition.y - box.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _drag)
            {
                _rx = Mathf.Clamp(e.mousePosition.x - _dragOff.x, 0f, Mathf.Max(0f, Screen.width - w));
                _ry = Mathf.Clamp(e.mousePosition.y - _dragOff.y, 0f, Mathf.Max(0f, Screen.height - h));
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _drag)
            {
                _drag = false;
                OnyxConfig.RadarX.Value = _rx;
                OnyxConfig.RadarY.Value = _ry;
                e.Use();
            }
            else if (OnyxConfig.RadarTeleport.Value && e.type == EventType.MouseDown && e.button == 1)
            {
                Teleport(inner);
            }
        }
        if (e == null || e.type != EventType.Repaint) return;

        EnsureStyle();
        OnyxPalette p = OnyxStyle.Current;

        OnyxStyle.FillRounded(box, A(p.Window, 0.93f), 12);
        OnyxStyle.FillRounded(new Rect(box.x, box.y, box.width, head + 6f * _sc), A(p.Accent, 0.10f), 12);
        OnyxStyle.StrokeRounded(box, A(p.Accent, 0.55f), 12, 1);
        OnyxStyle.Fill(new Rect(box.x + 12f * _sc, box.y + head + 3f * _sc, box.width - 24f * _sc, 1f), A(p.Accent, 0.35f));
        _title.fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * _sc));
        _title.normal.textColor = A(p.Accent, 1f);
        GUI.Label(new Rect(box.x, box.y + 3f * _sc, box.width, head), "◎  " + MapName(), _title);

        OnyxStyle.FillRounded(inner, A(Color.black, 0.30f), 8);
        OnyxStyle.StrokeRounded(inner, A(Color.white, 0.05f), 8, 1);

        OnyxNav.Graph g = OnyxNav.Current();
        if (g != null) { try { Skeleton(g, inner); } catch { } }
        Players(inner);
        if (OnyxConfig.RadarBodies.Value) Bodies(inner);
    }

    private static void EnsureStyle()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
    }

    private static string MapName()
    {
        switch (OnyxNav.CurrentMapId())
        {
            case 0: case 3: return "The Skeld";
            case 1: return "Mira HQ";
            case 2: return "Polus";
            case 4: return "Airship";
            case 5: return "Fungle";
            default: return OnyxText.T("Карта", "Map");
        }
    }

    private static bool Bounds()
    {
        int map = OnyxNav.CurrentMapId();
        if (map == _boundsMap && _min.x <= _max.x) return true;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        OnyxNav.Graph g = OnyxNav.Current();
        if (g != null && g.Pos.Length > 0)
        {
            foreach (Vector2 v in g.Pos) Grow(ref min, ref max, v);
        }
        else return false;

        if (min.x > max.x) return false;
        Vector2 m = (max - min) * 0.06f + Vector2.one * 0.5f;
        _min = min - m; _max = max + m; _boundsMap = map;
        _trail.Clear();
        return true;
    }

    private static void Grow(ref Vector2 min, ref Vector2 max, Vector2 v)
    {
        if (v.x < min.x) min.x = v.x;
        if (v.y < min.y) min.y = v.y;
        if (v.x > max.x) max.x = v.x;
        if (v.y > max.y) max.y = v.y;
    }

    private static Vector2 Map(Vector2 w, Rect r)
    {
        float tx = (w.x - _min.x) / Mathf.Max(0.01f, _max.x - _min.x);
        float ty = (w.y - _min.y) / Mathf.Max(0.01f, _max.y - _min.y);
        return new Vector2(r.x + tx * r.width, r.y + (1f - ty) * r.height);
    }

    private static Vector2[] _skel;
    private static Color32[] _skelPx;
    private static Texture2D _skelTex;
    private static GUIStyle _skelStyle;
    private static int _skelMap = -999, _skelW, _skelH;
    private static float _skelAt;

    private static void Skeleton(OnyxNav.Graph g, Rect r)
    {
        int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, 1024);
        int h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, 1024);
        int map = OnyxNav.CurrentMapId();

        bool dirty = _skelTex == null || map != _skelMap;
        bool resized = w != _skelW || h != _skelH;
        if (dirty || (resized && Time.unscaledTime - _skelAt > 0.1f))
        {
            BuildSkelTex(g, w, h);
            _skelMap = map; _skelW = w; _skelH = h; _skelAt = Time.unscaledTime;
        }
        if (_skelTex == null) return;

        if (_skelStyle == null) _skelStyle = new GUIStyle();
        _skelStyle.normal.background = _skelTex;
        Color prev = GUI.color;
        GUI.color = A(OnyxStyle.Current.Accent, 0.16f);
        GUI.Box(r, GUIContent.none, _skelStyle);
        GUI.color = prev;
    }

    private static void BuildSkelTex(OnyxNav.Graph g, int w, int h)
    {
        if (_skelTex == null || _skelTex.width != w || _skelTex.height != h)
        {
            if (_skelTex != null) Object.Destroy(_skelTex);
            _skelTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _skelPx = new Color32[w * h];
        }
        else System.Array.Clear(_skelPx, 0, _skelPx.Length);

        int n = g.Pos.Length;
        if (_skel == null || _skel.Length < n) _skel = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float tx = (g.Pos[i].x - _min.x) / Mathf.Max(0.01f, _max.x - _min.x);
            float ty = (g.Pos[i].y - _min.y) / Mathf.Max(0.01f, _max.y - _min.y);
            _skel[i] = new Vector2(tx * w, ty * h);
        }

        int thick = Mathf.Max(1, Mathf.RoundToInt(1.4f * _sc));
        for (int i = 0; i < n; i++)
            foreach (int nb in g.Adj[i])
            {
                if (nb <= i || nb >= n) continue;
                Stamp(_skelPx, w, h, _skel[i], _skel[nb], thick);
            }

        _skelTex.SetPixels32(_skelPx);
        _skelTex.Apply();
    }

    private static void Stamp(Color32[] px, int w, int h, Vector2 a, Vector2 b, int thick)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 0.5f) return;
        int steps = Mathf.CeilToInt(len);
        float sx = dx / steps, sy = dy / steps;
        int half = thick / 2;
        Color32 col = new Color32(255, 255, 255, 255);
        for (int s = 0; s <= steps; s++)
        {
            int cx = Mathf.RoundToInt(a.x + sx * s);
            int cy = Mathf.RoundToInt(a.y + sy * s);
            for (int oy = -half; oy <= half; oy++)
            {
                int y = cy + oy;
                if (y < 0 || y >= h) continue;
                int row = y * w;
                for (int ox = -half; ox <= half; ox++)
                {
                    int x = cx + ox;
                    if (x < 0 || x >= w) continue;
                    px[row + x] = col;
                }
            }
        }
    }

    private static void Players(Rect r)
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null) continue;
                bool dead = pc.Data.IsDead;
                Vector2 w = pc.GetTruePosition();
                Vector2 sp = Map(w, r);
                Color col = PlayerColor(pc);

                if (!dead) { Track(pc.PlayerId, w); Trail(pc.PlayerId, r, col); }
                else col.a = 0.4f;

                if (pc == me) DrawDot(sp, (15f + pulse * 5f) * _sc, A(OnyxStyle.Current.Accent, 0.22f));
                float d = (pc == me ? 11f : 8.5f) * _sc;
                DrawDot(sp, d + 3f * _sc, A(Color.black, 0.6f));
                DrawDot(sp, d, A(col, col.a));
            }
        }
        catch { }
    }

    private static void Track(byte id, Vector2 w)
    {
        if (!_trail.TryGetValue(id, out var list)) { list = new List<(Vector2, float)>(); _trail[id] = list; }
        float now = Time.unscaledTime;
        if (list.Count == 0 || now - list[list.Count - 1].t > 0.06f) list.Add((w, now));
        while (list.Count > 0 && now - list[0].t > 0.5f) list.RemoveAt(0);
    }

    private static void Trail(byte id, Rect r, Color col)
    {
        if (!_trail.TryGetValue(id, out var list) || list.Count < 2) return;
        for (int i = 1; i < list.Count; i++)
            Line(Map(list[i - 1].w, r), Map(list[i].w, r), A(col, (float)i / list.Count * 0.45f * col.a), 2f * _sc);
    }

    private static Texture2D DotTex()
    {
        if (_dot != null) return _dot;
        int s = 32;
        _dot = new Texture2D(s, s, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        float rad = s / 2f - 1f, c = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((rad - dist) / 1.5f) * 255f));
            }
        _dot.SetPixels32(px);
        _dot.Apply();
        return _dot;
    }

    private static GUIStyle _dotStyle;

    private static void DrawDot(Vector2 c, float size, Color col)
    {
        if (_dotStyle == null) _dotStyle = new GUIStyle();
        _dotStyle.normal.background = DotTex();
        Color prev = GUI.color;
        GUI.color = new Color(col.r, col.g, col.b, col.a * prev.a);
        GUI.Box(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), GUIContent.none, _dotStyle);
        GUI.color = prev;
    }

    private static DeadBody[] _bodies;
    private static float _bodiesAt = -99f;

    private static void Bodies(Rect r)
    {
        try
        {
            if (_bodies == null || Time.unscaledTime - _bodiesAt > 0.5f)
            {
                _bodiesAt = Time.unscaledTime;
                _bodies = Object.FindObjectsOfType<DeadBody>();
            }
            if (_bodies == null) return;
            for (int i = 0; i < _bodies.Length; i++)
            {
                DeadBody b = _bodies[i];
                if (b == null) continue;
                Vector2 sp = Map(b.TruePosition, r);
                DrawDot(sp, 11f * _sc, A(new Color(0.6f, 0.1f, 0.1f, 1f), 0.85f));
                DrawDot(sp, 9f * _sc, A(new Color(0.9f, 0.25f, 0.25f, 1f), 0.95f));
                float k = 3f * _sc;
                Line(new Vector2(sp.x - k, sp.y - k), new Vector2(sp.x + k, sp.y + k), A(Color.white, 1f), 1.6f * _sc);
                Line(new Vector2(sp.x - k, sp.y + k), new Vector2(sp.x + k, sp.y - k), A(Color.white, 1f), 1.6f * _sc);
            }
        }
        catch { }
    }

    private static void Teleport(Rect r)
    {
        Event e = Event.current;
        if (!r.Contains(e.mousePosition)) return;
        float tx = (e.mousePosition.x - r.x) / r.width;
        float ty = 1f - (e.mousePosition.y - r.y) / r.height;
        Vector2 world = new Vector2(_min.x + tx * (_max.x - _min.x), _min.y + ty * (_max.y - _min.y));
        try { PlayerControl.LocalPlayer.NetTransform.SnapTo(world); } catch { }
        e.Use();
    }

    private static Color PlayerColor(PlayerControl pc)
    {
        try
        {
            int id = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
            if (Palette.PlayerColors != null && id >= 0 && id < Palette.PlayerColors.Length)
            {
                Color32 c = Palette.PlayerColors[id];
                return new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
            }
        }
        catch { }
        return Color.white;
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a * _al);

    private static void Line(Vector2 a, Vector2 b, Color col, float w)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, a);
        OnyxStyle.Fill(new Rect(a.x, a.y - w * 0.5f, len, w), col);
        GUI.matrix = m;
    }
}
