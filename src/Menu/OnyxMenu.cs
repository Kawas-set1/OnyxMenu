using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using InnerNet;
using UnityEngine;

namespace Onyx;

public sealed class OnyxMenu : MonoBehaviour
{
    private const int WindowId = 770731;
    private const float FullH = 610f;
    private const float MinW = 620f;
    private const float HeaderH = 64f;
    private const float SidebarW = 200f;
    private const float TabH = 44f;
    private const float RowH = 34f;
    private const float FooterH = 40f;

    private struct TabDef
    {
        public OnyxIcon Icon;
        public string Ru;
        public string En;
        public TabDef(OnyxIcon icon, string ru, string en) { Icon = icon; Ru = ru; En = en; }
        public string Name => OnyxText.T(Ru, En);
    }

    private static readonly TabDef[] Tabs =
    {
        new TabDef(OnyxIcon.Home, "Главная", "Home"),
        new TabDef(OnyxIcon.Star, "Удобства", "QoL"),
        new TabDef(OnyxIcon.Door, "Лобби", "Lobby"),
        new TabDef(OnyxIcon.Eye, "Визуальные", "Visual"),
        new TabDef(OnyxIcon.Crew, "Игроки", "Players"),
        new TabDef(OnyxIcon.Bolt, "Читы", "Cheats"),
        new TabDef(OnyxIcon.Shield, "Защита", "Guard"),
        new TabDef(OnyxIcon.Wifi, "Сеть", "Network"),
        new TabDef(OnyxIcon.Tune, "Правила", "Rules"),
    };

    internal static bool Opened;

    private bool _open;
    private bool _collapsed;
    private int _tab;
    private int _slider;
    private readonly HashSet<int> _roleOpen = new HashSet<int>();
    private string _presetName = "";
    private float _scroll;
    private float _h = FullH;
    private int _resize;
    private Rect _window = new Rect(300f, 110f, 720f, FullH);

    private bool _built;
    private int _themeBuilt = -1;
    private Texture2D _gradient;
    private Texture2D _gloss;
    private GUIStyle _windowBg;
    private GUI.WindowFunction _winFn;
    private GUIStyle _brand;
    private GUIStyle _verPill;
    private GUIStyle _tabStyle;
    private GUIStyle _cardTitle;
    private GUIStyle _rowLabel;
    private GUIStyle _value;
    private GUIStyle _valueC;
    private GUIStyle _muted;
    private GUIStyle _footer;
    private GUIStyle _btnLabel;
    private GUIStyle _centerMuted;
    private GUIStyle _arrow;
    private GUIStyle _smallBtn;
    private GUIStyle _rowName;
    private GUIStyle _invisible;
    private GUIStyle _keyBadge;
    private GUIStyle _star;

    private readonly List<ClientData> _guardClients = new List<ClientData>();
    private readonly List<PlayerControl> _roleClients = new List<PlayerControl>();
    private readonly List<QuickItem> _searchHits = new List<QuickItem>();
    private string _cloneText = "";
    private string _netText = "";
    private byte _netSrc = 255;
    private readonly List<PlayerControl> _netPick = new List<PlayerControl>();
    private string _nickText = "";
    private string _chatSend = "";
    private string _search = "";
    private string _searchDone;
    private string _textFocus;
    private bool _wasTyping;

    private bool _rebinding;
    private ConfigEntry<KeyCode> _rebindTarget;
    internal static bool Rebinding;

    private float _openAt = -1f;
    private float _closeAt = -1f;
    private float _fade = 1f;
    private int _press;
    private Texture2D _crystalTex;
    private string _crystalAccent;
    private GUIStyle _crystalStyle;
    private float _pressAt;

    private int _tabDir = 1;
    private float _tabAnimAt = -1f;
    private Rect _tabHi;
    private bool _tabHiInit;
    private readonly Dictionary<object, float> _pillAnim = new Dictionary<object, float>();
    private readonly Dictionary<object, float> _pillVel = new Dictionary<object, float>();

    private static Vector2 M => Event.current != null ? Event.current.mousePosition : new Vector2(-1f, -1f);

    public void Update()
    {
        if (!_rebinding && OnyxConfig.MenuKey != null && Input.GetKeyDown(OnyxConfig.MenuKey.Value))
        {
            if (_open && _closeAt < 0f) RequestClose();
            else Open();
        }
        if (!_rebinding && _open && _closeAt < 0f && Input.GetKeyDown(KeyCode.Escape))
            RequestClose();
        Opened = _open;

        bool typing = _open && _closeAt < 0f && _textFocus != null;
        if (typing) SetMoveable(false);
        else if (_wasTyping) SetMoveable(true);
        _wasTyping = typing;
    }

    private void Open()
    {
        _open = true;
        _openAt = Time.unscaledTime;
        _closeAt = -1f;
    }

    private void RequestClose()
    {
        if (_open && _closeAt < 0f) _closeAt = Time.unscaledTime;
    }

    private void SetTab(int nt)
    {
        if (nt == _tab) return;
        _tabDir = nt > _tab ? 1 : -1;
        _tab = nt;
        _tabAnimAt = Time.unscaledTime;
        _scroll = 0f;
    }

    private static float SmoothSat(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private static int RectId(Rect r) => (Mathf.RoundToInt(r.x) * 73856093) ^ (Mathf.RoundToInt(r.y) * 19349663) ^ (Mathf.RoundToInt(r.width) * 83492791);

    private float PressK(int id)
    {
        if (_press != id) return 1f;
        float t = (Time.unscaledTime - _pressAt) / 0.13f;
        if (t >= 1f) { _press = 0; return 1f; }
        return Mathf.Lerp(0.97f, 1f, SmoothSat(t));
    }

    private void Pressed(Rect r, int id)
    {
        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            _press = id;
            _pressAt = Time.unscaledTime;
        }
    }

    private static void SetMoveable(bool value)
    {
        try { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = value; }
        catch { }
    }

    internal void DrawGui()
    {
        if (!_open) return;
        Build();
        HandleRebindCapture();

        float s = Mathf.Clamp(Screen.height / 1080f, 0.72f, 1.8f);
        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float vw = Screen.width / s;
        float vh = Screen.height / s;

        float now = Time.unscaledTime;
        float fade = 1f;
        if (_closeAt >= 0f)
        {
            float t = Mathf.Clamp01((now - _closeAt) / 0.2f);
            fade = 1f - SmoothSat(t);
            if (t >= 1f) { _open = false; _closeAt = -1f; _openAt = -1f; _collapsed = false; GUI.matrix = prevMatrix; return; }
        }
        else if (_openAt >= 0f)
        {
            float t = Mathf.Clamp01((now - _openAt) / 0.26f);
            fade = SmoothSat(t);
            if (t >= 1f) _openAt = -1f;
        }
        _fade = fade;

        OnyxStyle.Fill(new Rect(0f, 0f, vw, vh), new Color(0.02f, 0.02f, 0.022f, 0.34f * fade));

        ClampWindow(vw, vh);
        if (fade >= 1f) HandleResize(vw, vh);

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fade);

        DrawShadow(_window);
        if (_winFn == null) _winFn = (GUI.WindowFunction)DrawWindow;
        _window = GUI.Window(WindowId, _window, _winFn, string.Empty, _windowBg);

        GUI.color = prevColor;
        GUI.matrix = prevMatrix;
    }

    private const int RzL = 1, RzR = 2, RzT = 4, RzB = 8;

    private void HandleResize(float vw, float vh)
    {
        Event e = Event.current;
        if (_collapsed || e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 m = e.mousePosition;
            bool inX = m.x >= _window.x - 8f && m.x <= _window.xMax + 8f;
            bool inY = m.y >= _window.y - 8f && m.y <= _window.yMax + 8f;
            int dir = 0;
            if (inY && Mathf.Abs(m.x - _window.x) <= 8f) dir |= RzL;
            if (inY && Mathf.Abs(m.x - _window.xMax) <= 8f) dir |= RzR;
            if (inX && Mathf.Abs(m.y - _window.y) <= 8f) dir |= RzT;
            if (inX && Mathf.Abs(m.y - _window.yMax) <= 8f) dir |= RzB;
            if (dir != 0) { _resize = dir; e.Use(); }
        }
        else if (_resize != 0 && e.type == EventType.MouseDrag)
        {
            Vector2 m = e.mousePosition;
            if ((_resize & RzR) != 0) _window.width = Mathf.Clamp(m.x - _window.x, MinW, Mathf.Max(MinW, vw - _window.x));
            if ((_resize & RzB) != 0) _h = Mathf.Clamp(m.y - _window.y, FullH, Mathf.Max(FullH, vh - _window.y));
            if ((_resize & RzL) != 0)
            {
                float right = _window.x + _window.width;
                float nx = Mathf.Clamp(m.x, 0f, right - MinW);
                _window.x = nx;
                _window.width = right - nx;
            }
            if ((_resize & RzT) != 0)
            {
                float bottom = _window.y + _h;
                float ny = Mathf.Clamp(m.y, 0f, bottom - FullH);
                _window.y = ny;
                _h = bottom - ny;
            }
            e.Use();
        }
        else if (_resize != 0 && e.type == EventType.MouseUp)
        {
            _resize = 0;
            e.Use();
        }
    }

    private void ClampWindow(float vw, float vh)
    {
        _window.width = Mathf.Clamp(_window.width, MinW, Mathf.Max(MinW, vw));
        _h = Mathf.Clamp(_h, FullH, Mathf.Max(FullH, vh));
        _window.height = _collapsed ? HeaderH : _h;
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, vw - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, vh - _window.height));
    }

    private static void DrawShadow(Rect w)
    {
        for (int i = 0; i < 6; i++)
        {
            float e = 4f + i * 6f;
            OnyxStyle.FillRounded(new Rect(w.x - e, w.y - e + 6f, w.width + e * 2f, w.height + e * 2f), A(Color.black, 0.06f), 26);
        }
    }

    private void DrawWindow(int id)
    {
        OnyxPalette p = OnyxStyle.Current;
        float w = _window.width;
        float h = _window.height;
        GUI.color = new Color(1f, 1f, 1f, _fade);

        if (_collapsed) OnyxStyle.FillRounded(new Rect(0f, 0f, w, h), p.Window, 16);
        else OnyxStyle.DrawTex(new Rect(0f, 0f, w, h), _gradient);
        OnyxStyle.FillRounded(new Rect(0f, 0f, w, h), A(p.Accent, 0.04f), 16);
        OnyxStyle.StrokeRounded(new Rect(0f, 0f, w, h), A(p.Accent, 0.22f), 16, 1);
        if (!_collapsed)
            OnyxStyle.DrawTex(new Rect(2f, 2f, w - 4f, HeaderH - 3f), _gloss);
        OnyxStyle.Fill(new Rect(16f, 1f, w - 32f, 1f), A(Color.white, 0.11f));

        DrawHeader(w, p);
        if (_collapsed) { GUI.DragWindow(new Rect(0f, 0f, w, HeaderH)); return; }

        float sbTop = HeaderH + 8f;
        float sbBot = h - FooterH - 2f;
        OnyxStyle.FillRounded(new Rect(8f, sbTop, SidebarW - 14f, sbBot - sbTop), A(Color.black, 0.14f), 12);
        OnyxStyle.Fill(new Rect(10f, sbTop + 1f, SidebarW - 18f, 1f), A(Color.white, 0.05f));
        OnyxStyle.Fill(new Rect(SidebarW - 2f, HeaderH + 12f, 1f, h - HeaderH - FooterH - 24f), A(Color.white, 0.06f));

        var navZone = new Rect(0f, HeaderH, SidebarW, h - HeaderH - FooterH);
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && navZone.Contains(Event.current.mousePosition))
        {
            SetTab(Mathf.Clamp(_tab + (Event.current.delta.y > 0f ? 1 : -1), 0, 9));
            Event.current.Use();
        }

        var settingsRect = new Rect(14f, h - FooterH - TabH - 8f, SidebarW - 26f, TabH);
        Rect activeRect = _tab == 9 ? settingsRect : new Rect(14f, HeaderH + 14f + _tab * (TabH + 4f), SidebarW - 26f, TabH);
        if (!_tabHiInit) { _tabHi = activeRect; _tabHiInit = true; }
        if (Event.current != null && Event.current.type == EventType.Repaint)
            _tabHi = LerpRect(_tabHi, activeRect, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        DrawTabHighlight(_tabHi);

        float ty = HeaderH + 14f;
        for (int i = 0; i < Tabs.Length; i++)
        {
            DrawTab(new Rect(14f, ty, SidebarW - 26f, TabH), Tabs[i].Icon, Tabs[i].Name, i);
            ty += TabH + 4f;
        }
        DrawTab(settingsRect, OnyxIcon.Gear, OnyxText.T("Настройки", "Settings"), 9);

        DrawSearchBar(new Rect(SidebarW, HeaderH + 8f, w - SidebarW - 16f, 26f));
        var area = new Rect(SidebarW, HeaderH + 40f, w - SidebarW - 16f, h - HeaderH - 40f - FooterH);
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && area.Contains(Event.current.mousePosition))
        {
            _scroll += Event.current.delta.y * 20f;
            Event.current.Use();
        }

        float slide = 0f;
        float cAlpha = 1f;
        if (_tabAnimAt >= 0f)
        {
            float ct = Mathf.Clamp01((Time.unscaledTime - _tabAnimAt) / 0.17f);
            float ce = SmoothSat(ct);
            slide = (1f - ce) * 34f * _tabDir;
            cAlpha = Mathf.Clamp01(ce * 1.2f);
            if (ct >= 1f) _tabAnimAt = -1f;
        }

        Color cPrev = GUI.color;
        GUI.color = new Color(cPrev.r, cPrev.g, cPrev.b, cPrev.a * cAlpha);
        GUI.BeginGroup(area);
        DrawStars(area.width, area.height);
        float x = 4f;
        float cx = x + slide;
        float cw = area.width - 20f;
        float startY = 6f - _scroll;
        float cy = startY;
        var localArea = new Rect(slide, 0f, area.width, area.height);
        if (!string.IsNullOrEmpty(_search))
            DrawSearch(cx, ref cy, cw);
        else switch (_tab)
        {
            case 0: DrawHome(cx, ref cy, cw); break;
            case 1: DrawQoL(cx, ref cy, cw); break;
            case 2: DrawLobbyTab(cx, ref cy, cw); break;
            case 3: DrawVisual(cx, ref cy, cw); break;
            case 4: DrawPlayers(cx, ref cy, cw); break;
            case 5: DrawCheats(cx, ref cy, cw); break;
            case 6: DrawGuard(cx, ref cy, cw); break;
            case 7: DrawNet(cx, ref cy, cw); break;
            case 8: DrawHostTab(cx, ref cy, cw); break;
            case 9: DrawSettings(cx, ref cy, cw); break;
            default: DrawEmpty(localArea, OnyxIcon.Star, OnyxText.T("Раздел в разработке", "Section in progress")); break;
        }
        GUI.EndGroup();
        GUI.color = cPrev;

        float contentH = cy - startY;
        float maxScroll = Mathf.Max(0f, contentH - (area.height - 12f));
        _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);
        if (maxScroll > 1f)
        {
            float trackH = area.height - 8f;
            float thumbH = Mathf.Max(28f, trackH * (area.height / Mathf.Max(contentH, 1f)));
            float thumbY = area.y + 4f + (trackH - thumbH) * (_scroll / maxScroll);
            OnyxStyle.FillRounded(new Rect(area.xMax - 5f, area.y + 4f, 3f, trackH), A(Color.white, 0.05f), 1);
            OnyxStyle.FillRounded(new Rect(area.xMax - 5f, thumbY, 3f, thumbH), A(p.Accent, 0.55f), 1);
        }

        float fy = h - FooterH + 4f;
        OnyxStyle.Fill(new Rect(SidebarW, h - FooterH, w - SidebarW - 16f, 1f), A(Color.white, 0.06f));
        string acc = Hex(p.Accent);
        GUI.Label(new Rect(x, fy, cw, FooterH - 8f),
            $"{OnyxText.T("Меню", "Menu")} <color=#{acc}><b>{KeyName(OnyxConfig.MenuKey)}</b></color>     {OnyxText.T("Код лобби", "Lobby code")} <color=#{acc}><b>{KeyName(OnyxConfig.CopyCodeKey)}</b></color>     {OnyxText.T("Закрыть", "Close")} <color=#{acc}><b>Esc</b></color>", _footer);

        Color grip = A(p.Muted, 0.5f);
        for (int gi = 0; gi < 3; gi++)
            for (int gj = 0; gj <= gi; gj++)
                OnyxStyle.Fill(new Rect(w - 8f - gj * 4f, h - 8f - gi * 4f, 2f, 2f), grip);

        GUI.DragWindow(new Rect(0f, 0f, w, HeaderH));
    }

    private void DrawHeader(float w, OnyxPalette p)
    {
        DrawLogo(new Rect(20f, 15f, 34f, 34f), p);

        var brandR = new Rect(67f, 15f, 96f, 34f);
        GUI.Label(brandR, "ONYX", _brand);
        float sh = Mathf.Repeat(Time.unscaledTime * 0.6f, 3.4f) / 3.4f;
        float bw = brandR.width * 0.24f;
        float bx = brandR.x - bw + sh * (brandR.width + bw * 2f);
        Color hc = GUI.color;
        GUI.BeginGroup(new Rect(bx, brandR.y, bw, brandR.height));
        Color oc = _brand.normal.textColor;
        _brand.normal.textColor = Color.white;
        GUI.color = new Color(1f, 1f, 1f, hc.a * 0.55f);
        GUI.Label(new Rect(brandR.x - bx, 0f, brandR.width, brandR.height), "ONYX", _brand);
        _brand.normal.textColor = oc;
        GUI.color = hc;
        GUI.EndGroup();

        var ver = new Rect(w - 168f, 20f, 62f, 24f);
        OnyxStyle.FillRounded(ver, A(p.Accent, 0.14f), 8);
        GUI.Label(ver, "v" + OnyxPlugin.PluginVersion, _verPill);

        var minB = new Rect(w - 96f, 20f, 26f, 24f);
        var closeB = new Rect(w - 62f, 20f, 26f, 24f);
        if (WindowButton(minB, OnyxIcon.Minimize, p)) _collapsed = !_collapsed;
        if (WindowButton(closeB, OnyxIcon.Close, p)) RequestClose();

        OnyxStyle.Fill(new Rect(0f, HeaderH - 1f, w, 1f), A(p.Accent, 0.30f));
    }

    private bool WindowButton(Rect r, OnyxIcon icon, OnyxPalette p)
    {
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        if (hover) OnyxStyle.FillRounded(r, A(Color.white, 0.07f), 7);
        OnyxIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), hover ? p.Text : p.Muted);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private void DrawLogo(Rect box, OnyxPalette p)
    {
        Vector2 c = box.center;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
        OnyxStyle.FillRounded(new Rect(c.x - 22f, c.y - 22f, 44f, 44f), A(p.Accent, 0.09f + 0.13f * pulse), 22);

        if (_crystalTex == null || _crystalAccent != p.Id)
        {
            _crystalTex = BuildCrystal(p.Accent);
            _crystalAccent = p.Id;
        }
        if (_crystalStyle == null) _crystalStyle = new GUIStyle();
        _crystalStyle.normal.background = _crystalTex;
        GUI.Box(box, GUIContent.none, _crystalStyle);

        OnyxStyle.FillRounded(new Rect(box.x + 9f, box.y + 8f, 4f, 4f), A(Color.white, 0.85f), 2);
    }

    private static Texture2D BuildCrystal(Color accent)
    {
        const int n = 64;
        float cen = n * 0.5f;
        float half = n * 0.33f;
        float rr = half * 0.42f;
        const float k = 0.70710678f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - cen, dy = y + 0.5f - cen;
                float u = (dx + dy) * k, v = (dy - dx) * k;
                float qx = Mathf.Abs(u) - half + rr, qy = Mathf.Abs(v) - half + rr;
                float mx = Mathf.Max(qx, 0f), my = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(mx * mx + my * my) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rr;
                float a = Mathf.Clamp01(0.5f - d);
                if (a <= 0f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                Color col = accent;
                if (v <= 0f) col = Color.Lerp(col, Color.white, 0.18f);
                float sd = Mathf.Min(Mathf.Abs(u), Mathf.Abs(v));
                if (sd < 1.3f) col = Color.Lerp(col, Color.black, 0.20f * (1f - sd / 1.3f));
                px[y * n + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(a * 255f));
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private void DrawTabHighlight(Rect r)
    {
        OnyxPalette p = OnyxStyle.Current;
        OnyxStyle.FillRounded(r, A(p.Accent, 0.18f), 11);
        OnyxStyle.StrokeRounded(r, A(p.Accent, 0.38f), 11, 1);
        OnyxStyle.Fill(new Rect(r.x + 12f, r.y + 1f, r.width - 24f, 1f), A(Color.white, 0.08f));
        OnyxStyle.FillRounded(new Rect(r.x + 3f, r.y + 8f, 3f, r.height - 16f), p.Accent, 2);
    }

    private void DrawTab(Rect r, OnyxIcon icon, string label, int index)
    {
        OnyxPalette p = OnyxStyle.Current;
        bool active = index == _tab;
        bool hover = r.Contains(M);

        if (!active && hover)
        {
            OnyxStyle.FillRounded(r, A(Color.white, 0.05f), 11);
            OnyxStyle.StrokeRounded(r, A(Color.white, 0.06f), 11, 1);
        }

        Color ink = active ? p.Accent : (hover ? p.Text : p.Muted);
        if (active)
            OnyxStyle.FillRounded(new Rect(r.x + 9f, r.y + r.height / 2f - 14f, 28f, 28f), A(p.Accent, 0.16f), 14);
        OnyxIcons.Draw(icon, new Rect(r.x + 14f, r.y + r.height / 2f - 9f, 18f, 18f), ink);
        _tabStyle.normal.textColor = ink;
        GUI.Label(new Rect(r.x + 44f, r.y, r.width - 48f, r.height), label, _tabStyle);
        if (GUI.Button(r, GUIContent.none, _invisible)) SetTab(index);
    }

    private Rect Card(float x, ref float y, float w, string title, float bodyH)
    {
        OnyxPalette p = OnyxStyle.Current;
        const float head = 44f;
        var card = new Rect(x, y, w, head + bodyH + 14f);
        bool hover = card.Contains(M);
        if (hover) OnyxStyle.FillRounded(new Rect(card.x - 2f, card.y + 4f, card.width + 4f, card.height), A(p.Accent, 0.10f), 14);
        OnyxStyle.FillRounded(card, p.Panel, 12);
        OnyxStyle.FillRounded(card, A(Color.white, hover ? 0.045f : 0.02f), 12);
        OnyxStyle.FillRounded(new Rect(card.x, card.y, card.width, head), A(Color.white, 0.03f), 12);
        OnyxStyle.StrokeRounded(card, hover ? A(p.Accent, 0.30f) : A(Color.white, 0.08f), 12, 1);
        OnyxStyle.Fill(new Rect(card.x + 12f, card.y + 1f, card.width - 24f, 1f), A(Color.white, 0.10f));
        OnyxStyle.Fill(new Rect(card.x + 12f, card.yMax - 2f, card.width - 24f, 1f), A(Color.black, 0.20f));

        OnyxStyle.FillRounded(new Rect(card.x + 18f, card.y + head / 2f - 7f, 3f, 14f), p.Accent, 2);
        GUI.Label(new Rect(card.x + 32f, card.y, card.width - 70f, head), title.ToUpperInvariant(), _cardTitle);
        OnyxIcons.Draw(OnyxIcon.Chevron, new Rect(card.xMax - 32f, card.y + head / 2f - 6f, 13f, 13f), p.Muted);
        OnyxStyle.Fill(new Rect(card.x + 18f, card.y + head - 1f, card.width - 36f, 1f), A(Color.white, 0.05f));

        y = card.yMax + 14f;
        return new Rect(card.x + 18f, card.y + head + 8f, card.width - 36f, bodyH);
    }

    private void Sub(float x, ref float y, float w, string title)
    {
        OnyxPalette p = OnyxStyle.Current;
        OnyxStyle.Fill(new Rect(x, y + 21f, w, 1f), A(Color.white, 0.06f));
        OnyxStyle.FillRounded(new Rect(x, y + 6f, 2f, 12f), A(p.Accent, 0.75f), 1);
        GUI.Label(new Rect(x + 10f, y - 1f, w - 12f, 22f), title.ToUpperInvariant(), _cardTitle);
        y += 26f;
    }

    private static readonly string[] KickBanVals = { "Kick", "Ban" };
    private static readonly string[] VoteVals = { "Null", "Warn", "Kick", "Ban" };
    private static string[] _kickBanDisp, _voteDisp;
    private static bool _dispRu;

    private static void EnsureDisp()
    {
        bool ru = OnyxText.IsRussian;
        if (_kickBanDisp != null && ru == _dispRu) return;
        _dispRu = ru;
        _kickBanDisp = new[] { OnyxText.T("Кик", "Kick"), OnyxText.T("Бан", "Ban") };
        _voteDisp = new[] { OnyxText.T("Нулл", "Null"), OnyxText.T("Варн", "Warn"), OnyxText.T("Кик", "Kick"), OnyxText.T("Бан", "Ban") };
    }

    private static string[] KickBanDisp { get { EnsureDisp(); return _kickBanDisp; } }
    private static string[] VoteDisp { get { EnsureDisp(); return _voteDisp; } }

    private void DrawGuard(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, OnyxText.T("Списки доступа", "Access lists"), 2f * RowH + 62f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Бан-лист (кик по заходу)", "Ban list (kick on join)"), OnyxConfig.AccessBanEnabled);
        Toggle(b.x, ref by, b.width, OnyxText.T("Только вайтлист", "Whitelist only"), OnyxConfig.AccessWhitelistOnly);
        GUI.Label(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{OnyxText.T("Бан", "Ban")}: <b>{OnyxAccess.BanCount}</b>   {OnyxText.T("Вайт", "White")}: <b>{OnyxAccess.WhiteCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 174f, by + 1f, 82f, 24f), OnyxText.T("ОЧ. БАН", "CLR BAN"), new Color(0.9f, 0.36f, 0.36f))) OnyxAccess.ClearBans();
        if (SmallButton(new Rect(b.x + b.width - 86f, by + 1f, 82f, 24f), OnyxText.T("ОЧ. ВАЙТ", "CLR WHITE"), OnyxStyle.Current.Accent)) OnyxAccess.ClearWhites();
        by += 32f;
        if (SmallButton(new Rect(b.x + 2f, by, 112f, 24f), OnyxText.T("ИМПОРТ TXT", "IMPORT TXT"), OnyxStyle.Current.Accent)) OnyxAccess.ImportTxt();
        GUI.Label(new Rect(b.x + 122f, by, b.width - 122f, 24f), "Among Us/Onyx/BanList.txt · WhiteList.txt", _muted);

        b = Card(x, ref y, w, OnyxText.T("Ник-бан и история", "Nick ban & history"), 2f * RowH + 62f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Ник-бан (кик по нику)", "Nick ban (kick by name)"), OnyxConfig.AccessNickBanEnabled);
        Toggle(b.x, ref by, b.width, OnyxText.T("История ников по FriendCode", "Nick history by FriendCode"), OnyxConfig.NameHistory);
        _nickText = CustomText(new Rect(b.x + 2f, by, b.width - 168f, 26f), _nickText ?? "", "nickBan");
        if (SmallButton(new Rect(b.x + b.width - 160f, by + 1f, 158f, 24f), OnyxText.T("＋ НИК В БАН", "＋ NICK TO BAN"), OnyxStyle.Current.Accent) && !string.IsNullOrWhiteSpace(_nickText))
        {
            OnyxAccess.AddNickBan(_nickText);
            _nickText = "";
        }
        by += 32f;
        GUI.Label(new Rect(b.x + 2f, by, b.width * 0.5f, 26f), $"{OnyxText.T("Ников", "Nicks")}: <b>{OnyxAccess.NickBanCount}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 96f, by + 1f, 92f, 24f), OnyxText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.36f, 0.36f))) OnyxAccess.ClearNickBans();

        ListCard(x, ref y, w, OnyxText.T("Бан-лист", "Ban list"), OnyxAccess.BanEntries, OnyxAccess.RemoveBan);
        ListCard(x, ref y, w, OnyxText.T("Вайтлист", "Whitelist"), OnyxAccess.WhiteEntries, OnyxAccess.RemoveWhite);
        NickListCard(x, ref y, w);

        float lvlBody = 2f * RowH
            + (OnyxConfig.MinLevelEnabled.Value ? 50f + RowH : 0f)
            + (OnyxConfig.MaxLevelEnabled.Value ? 50f + RowH : 0f);
        b = Card(x, ref y, w, OnyxText.T("Уровень", "Level"), lvlBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Реакция на низкий уровень", "React to low level"), OnyxConfig.MinLevelEnabled);
        if (OnyxConfig.MinLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, OnyxText.T("Мин. уровень", "Min level"), OnyxConfig.MinLevel, 1, 500);
            ActionCycle(b.x, ref by, b.width, OnyxText.T("Действие", "Action"), OnyxConfig.MinLevelAction, KickBanVals, KickBanDisp);
        }
        Toggle(b.x, ref by, b.width, OnyxText.T("Реакция на высокий уровень", "React to high level"), OnyxConfig.MaxLevelEnabled);
        if (OnyxConfig.MaxLevelEnabled.Value)
        {
            SliderInt(b.x, ref by, b.width, OnyxText.T("Макс. уровень", "Max level"), OnyxConfig.MaxLevel, 1, 999);
            ActionCycle(b.x, ref by, b.width, OnyxText.T("Действие", "Action"), OnyxConfig.MaxLevelAction, KickBanVals, KickBanDisp);
        }

        b = Card(x, ref y, w, OnyxText.T("Войт-кик", "Vote-kick"), 2f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Блокировать войт-кики", "Block vote-kicks"), OnyxConfig.VoteKickProtect);
        ActionCycle(b.x, ref by, b.width, OnyxText.T("Реакция на голосующего", "React to voter"), OnyxConfig.VoteKickAction, VoteVals, VoteDisp);

        b = Card(x, ref y, w, OnyxText.T("Античит RPC (хост)", "RPC anticheat (host)"), 2f * RowH + 42f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Ловить невозможные RPC", "Catch impossible RPCs"), OnyxConfig.RpcGuard);
        ActionCycle(b.x, ref by, b.width, OnyxText.T("Реакция на читера", "React to cheater"), OnyxConfig.RpcGuardAction, VoteVals, VoteDisp);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), OnyxText.T("Саботаж мирным, вент без права и через систему, скан/анимация импостером, репорт/двери в H&S.", "Crew sabotage, illegal vent (both paths), impostor scan/anim, report/doors in H&S."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Анти-бан", "Anti-ban"), 2f * RowH + 42f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("На хосте: банить отправителя", "As host: punish sender"), OnyxConfig.AntiBanHost);
        ActionCycle(b.x, ref by, b.width, OnyxText.T("Реакция", "Action"), OnyxConfig.AntiBanHostAction, VoteVals, VoteDisp);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), OnyxText.T("Гасит краш-бан (vent-kick) пакет. Вне хоста работает всегда.", "Kills the crash-ban (vent-kick) packet. Off-host it is always on."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Цвета", "Colors"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Кик Fortegreen", "Kick Fortegreen"), OnyxConfig.KickFortegreen);

        b = Card(x, ref y, w, OnyxText.T("Анти-флуд", "Anti-flood"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Блок фейк-собраний и спавн-флуда", "Block fake meetings & spawn floods"), OnyxConfig.BlockFakeMeetings);

        b = Card(x, ref y, w, OnyxText.T("Модерация", "Moderation"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Кик/бан в матче (хост)", "Kick/ban in match (host)"), OnyxConfig.UnlockMatchKickBan);

        b = Card(x, ref y, w, OnyxText.T("Детект входящих", "Join detect"), RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Показывать платформу/ур./raw", "Show platform/lvl/raw"), OnyxConfig.JoinDetect);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Тост при заходе; ⚠ на подозрит. raw-имя.", "Toast on join; ⚠ on suspicious raw name."), _muted);
    }

    private void ReserveSelectedColor()
    {
        PlayerControl sel = OnyxMouseTools.Selected;
        if (sel == null || sel.Data == null || sel.Data.DefaultOutfit == null)
        {
            OnyxToast.Push(OnyxText.T("Резерв цвета", "Color reserve"), OnyxText.T("Выбери игрока (ЛКМ).", "Select a player (LMB)."), 2.5f, OnyxNotifyKind.Warning);
            return;
        }
        string fc = OnyxColorReservations.Fc(sel);
        if (string.IsNullOrWhiteSpace(fc))
        {
            OnyxToast.Push(OnyxText.T("Резерв цвета", "Color reserve"), OnyxText.T("Нет FriendCode.", "No FriendCode."), 2.5f, OnyxNotifyKind.Warning);
            return;
        }
        OnyxColorReservations.AddOrUpdate(fc, sel.Data.DefaultOutfit.ColorId, sel.Data.PlayerName);
        OnyxToast.Push(OnyxText.T("Резерв цвета", "Color reserve"), sel.Data.PlayerName, 2.5f, OnyxNotifyKind.Success);
    }

    private void DrawSnipe(float x, ref float y, float w)
    {
        bool on = OnyxConfig.SnipeColor.Value;
        int max = OnyxColorSnipe.Max();
        int cols = Mathf.Max(1, Mathf.FloorToInt((w - 36f + 6f) / 32f));
        int rows = Mathf.CeilToInt((max + 1f) / cols);

        Rect b = Card(x, ref y, w, OnyxText.T("Перехват цвета", "Color snipe"), RowH + (on ? rows * 32f + 24f : 0f));
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Ловить цвет в лобби", "Snipe color in lobby"), OnyxConfig.SnipeColor);
        if (!on) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        int want = Mathf.Clamp(OnyxConfig.SnipeColorId.Value, 0, max);
        for (int i = 0; i <= max; i++)
        {
            var r = new Rect(b.x + (i % cols) * 32f, by + (i / cols) * 32f, 26f, 26f);
            OnyxStyle.FillRounded(r, A(Color.black, 0.25f), 6);
            DrawColorDot(new Rect(r.x + 5f, r.y + 5f, 16f, 16f), i);
            bool busy = me != null && OnyxColorSnipe.Taken(i, me);
            if (busy) GUI.Label(r, "×", _star);
            OnyxStyle.StrokeRounded(r, i == want ? OnyxStyle.Current.Accent : A(Color.white, 0.10f), 6, i == want ? 2 : 1);
            if (GUI.Button(r, GUIContent.none, _invisible)) OnyxConfig.SnipeColorId.Value = i;
        }
        by += rows * 32f;

        bool free = me != null && !OnyxColorSnipe.Taken(want, me);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f),
            free ? OnyxText.T("Цвет свободен — беру.", "Color is free — taking it.") : OnyxText.T("Занят — жду, пока освободится.", "Taken — waiting for it to free up."), _muted);
    }

    private void DrawColorDot(Rect r, int colorId)
    {
        Color c = new Color(0.5f, 0.5f, 0.5f, 1f);
        try
        {
            if (Palette.PlayerColors != null && colorId >= 0 && colorId < Palette.PlayerColors.Length)
            {
                Color32 c32 = Palette.PlayerColors[colorId];
                c = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, 1f);
            }
        }
        catch { }
        OnyxStyle.FillRounded(r, c, 6);
        OnyxStyle.StrokeRounded(r, A(Color.white, 0.3f), 6, 1);
    }

    private void PlayerRow(float x, ref float y, float w, InnerNetClient net, ClientData c)
    {
        var r = new Rect(x, y, w, RowH + 2f);
        HoverFill(r);
        int nk = OnyxNameHistory.KnownNickCount(c.Character);
        string note = nk > 1 ? "  " + OnyxText.T($"·{nk} ников", $"·{nk} nicks") : string.Empty;
        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 200f, r.height),
            $"<b>{OnyxAccess.SafeName(c)}</b>   <color=#8A94AC><size=11>{ClientInfo(c)}{note}</size></color>", _rowName);
        float cy = r.y + (r.height - 24f) / 2f;
        string mfc = OnyxColorReservations.Fc(c.Character);
        bool muted = OnyxMuteList.IsMuted(mfc);
        if (SmallButton(new Rect(r.xMax - 188f, cy, 44f, 24f), OnyxText.T("МУТ", "MUTE"), muted ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.5f, 0.55f, 0.62f))) OnyxMuteList.Toggle(mfc);
        if (SmallButton(new Rect(r.xMax - 142f, cy, 44f, 24f), OnyxText.T("НИК", "NICK"), new Color(0.86f, 0.5f, 0.28f))) OnyxAccess.NickBanClient(net, c);
        if (SmallButton(new Rect(r.xMax - 96f, cy, 44f, 24f), OnyxText.T("БАН", "BAN"), new Color(0.9f, 0.36f, 0.36f))) OnyxAccess.BanClient(net, c);
        if (SmallButton(new Rect(r.xMax - 50f, cy, 44f, 24f), OnyxText.T("ВАЙТ", "WHITE"), OnyxStyle.Current.Accent)) OnyxAccess.WhiteClient(c);
        y += RowH + 6f;
    }

    private void NickListCard(float x, ref float y, float w)
    {
        IReadOnlyList<string> nicks = OnyxAccess.NickBanEntries;
        float body = nicks.Count > 0 ? nicks.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{OnyxText.T("Ник-бан список", "Nick ban list")} ({nicks.Count})", body);
        float by = b.y;
        if (nicks.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < nicks.Count; i++)
        {
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 46f, r.height), $"<b>{nicks[i]}</b>", _rowName);
            if (SmallButton(new Rect(r.xMax - 38f, r.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
            {
                OnyxAccess.RemoveNickBan(nicks[i]);
                break;
            }
            by += 30f;
        }
    }

    private void ListCard(float x, ref float y, float w, string title, IReadOnlyList<AccessEntry> entries, Action<string> onRemove)
    {
        float body = entries.Count > 0 ? entries.Count * 30f : 26f;
        Rect b = Card(x, ref y, w, $"{title} ({entries.Count})", body);
        float by = b.y;
        if (entries.Count == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Пусто.", "Empty."), _muted);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AccessEntry e = entries[i];
            var r = new Rect(b.x, by, b.width, 28f);
            HoverFill(r);
            string name = string.IsNullOrEmpty(e.Name) ? (string.IsNullOrEmpty(e.Code) ? OnyxText.T("гость", "guest") : e.Code) : e.Name;
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 46f, r.height),
                $"<b>{name}</b>   <color=#8A94AC><size=11>{EntrySub(e)}</size></color>", _rowName);
            if (SmallButton(new Rect(r.xMax - 38f, r.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f)))
            {
                onRemove(string.IsNullOrEmpty(e.Code) ? e.Puid : e.Code);
                break;
            }
            by += 30f;
        }
    }

    private static string EntrySub(AccessEntry e)
    {
        string s = string.Empty;
        if (!string.IsNullOrEmpty(e.Code)) s = e.Code;
        if (!string.IsNullOrEmpty(e.Puid))
        {
            string pu = e.Puid.Length > 14 ? e.Puid.Substring(0, 14) + "…" : e.Puid;
            s = s.Length > 0 ? s + " · PUID " + pu : "PUID " + pu;
        }
        return s;
    }

    private bool SmallButton(Rect r, string label, Color col)
    {
        bool hover = r.Contains(M);
        int id = RectId(r);
        Pressed(r, id);
        float k = PressK(id);

        Matrix4x4 m = GUI.matrix;
        if (k < 1f) GUIUtility.ScaleAroundPivot(new Vector2(k, k), r.center);
        OnyxStyle.FillRounded(r, hover ? A(col, 0.4f) : A(col, 0.2f), 7);
        OnyxStyle.Fill(new Rect(r.x + 4f, r.y + 2f, r.width - 8f, 1f), A(Color.white, 0.14f));
        _smallBtn.normal.textColor = Color.Lerp(col, Color.white, 0.55f);
        GUI.Label(r, label, _smallBtn);
        GUI.matrix = m;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private void ActionCycle(float x, ref float y, float w, string label, ConfigEntry<string> entry, string[] vals, string[] disp)
    {
        int i = Mathf.Max(0, Array.IndexOf(vals, entry.Value));
        CycleRow(x, ref y, w, label, disp[i], () => { entry.Value = vals[(i + 1) % vals.Length]; });
    }

    private void CollectClients(List<ClientData> into)
    {
        try
        {
            InnerNetClient net = GuardNet();
            if (net == null || net.allClients == null) return;
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id >= 0 && c.Id != net.ClientId) into.Add(c);
            }
        }
        catch { }
    }

    private PlayerControl NetSrc()
    {
        if (_netSrc == 255) return PlayerControl.LocalPlayer;
        _netPick.Clear();
        CollectPlayers(_netPick);
        for (int i = 0; i < _netPick.Count; i++)
            if (_netPick[i].PlayerId == _netSrc) return _netPick[i];
        _netSrc = 255;
        return PlayerControl.LocalPlayer;
    }

    private void NextNetSrc()
    {
        _netPick.Clear();
        CollectPlayers(_netPick);
        _netPick.RemoveAll((Predicate<PlayerControl>)(p => p == PlayerControl.LocalPlayer));
        if (_netPick.Count == 0) { _netSrc = 255; return; }
        if (_netSrc == 255) { _netSrc = _netPick[0].PlayerId; return; }
        int i = _netPick.FindIndex((Predicate<PlayerControl>)(p => p.PlayerId == _netSrc));
        _netSrc = i < 0 || i + 1 >= _netPick.Count ? (byte)255 : _netPick[i + 1].PlayerId;
    }

    private void CollectPlayers(List<PlayerControl> into)
    {
        try
        {
            if (PlayerControl.AllPlayerControls == null) return;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.Data != null && !pc.Data.Disconnected) into.Add(pc);
        }
        catch { }
    }

    private void WhisperRow(float x, ref float y, float w, PlayerControl pc)
    {
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 120f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), OnyxText.T("ШЕПНУТЬ", "WHISPER"), new Color(0.55f, 0.7f, 1f)))
            OnyxWhisper.Prefill(pc.PlayerId.ToString());
        y += 32f;
    }

    private void TpRow(float x, ref float y, float w, PlayerControl pc)
    {
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 120f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), OnyxText.T("К НЕМУ", "GO"), new Color(0.5f, 0.78f, 0.92f)))
            TpTo(pc);
        y += 32f;
    }

    private static void TpTo(PlayerControl pc)
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (pc == null || me == null || me.NetTransform == null) return;
            me.NetTransform.RpcSnapTo(pc.GetTruePosition());
        }
        catch { }
    }

    private void VotekickRow(float x, ref float y, float w, PlayerControl pc)
    {
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (IsHost(pc)) nm += "  [H]";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 200f, r.height), nm, _rowName);
        bool sel = OnyxVotekick.IsTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - 190f, r.y + 3f, 86f, 24f), sel ? OnyxText.T("АВТО ✓", "AUTO ✓") : OnyxText.T("АВТО", "AUTO"), sel ? OnyxStyle.Current.Accent : new Color(0.36f, 0.39f, 0.47f)))
            OnyxVotekick.ToggleTarget(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - 100f, r.y + 3f, 96f, 24f), OnyxText.T("ЗАЯВИТЬ", "VOTE"), new Color(0.78f, 0.42f, 0.95f)))
            OnyxVotekick.VoteOne(pc);
        y += 32f;
    }

    private void LoopRow(float x, ref float y, float w, PlayerControl pc)
    {
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        string nm = pc.Data != null ? pc.Data.PlayerName : "?";
        if (pc.Data != null && pc.Data.IsDead) nm += "  <size=80%>†</size>";
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 130f, r.height), nm, _rowName);

        bool run = OnyxLobbyPranks.LoopTarget == pc.PlayerId && OnyxLobbyPranks.LoopLeft > 0;
        string lbl = run ? OnyxText.T("СТОП ", "STOP ") + OnyxLobbyPranks.LoopLeft : OnyxText.T("УБИТЬ ×20", "KILL ×20");
        if (SmallButton(new Rect(r.xMax - 110f, r.y + 3f, 106f, 24f), lbl, run ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.78f, 0.42f, 0.95f)))
            OnyxToast.Push(OnyxText.T("Рофл", "Prank"), OnyxLobbyPranks.MurderLoop(pc, 20), 2f, OnyxNotifyKind.Info);
        y += 32f;
    }

    private static bool IsHost(PlayerControl pc)
    {
        try
        {
            ClientData h = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetHost() : null;
            return h != null && h.Character == pc;
        }
        catch { return false; }
    }

    private void RoleRow(float x, ref float y, float w, PlayerControl pc)
    {
        var r = new Rect(x, y, w, 30f);
        HoverFill(r);
        int colorId = pc.Data != null && pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        DrawColorDot(new Rect(r.x + 6f, r.y + 9f, 12f, 12f), colorId);
        GUI.Label(new Rect(r.x + 26f, r.y, r.width - 240f, r.height), pc.Data != null ? pc.Data.PlayerName : "?", _rowName);
        int idx = OnyxForceRoles.IndexOf(pc.PlayerId);
        Color col = idx == 0 ? new Color(0.5f, 0.55f, 0.62f) : OnyxStyle.Current.Accent;
        if (SmallButton(new Rect(r.xMax - 178f, r.y + 3f, 122f, 24f), OnyxForceRoles.Name(idx) + "  ▸", col))
            OnyxForceRoles.Cycle(pc.PlayerId);
        if (SmallButton(new Rect(r.xMax - 52f, r.y + 3f, 48f, 24f), OnyxText.T("ФОРС", "SET"), new Color(0.4f, 0.8f, 0.5f)))
            OnyxForceRoles.ForceNow(pc.PlayerId);
        y += 32f;
    }

    private static InnerNetClient GuardNet()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }

    private static string ClientInfo(ClientData c)
    {
        string s = string.Empty;
        try
        {
            string lvl = Patches.OnyxJoinLevels.Display(c.Id, c.Character);
            if (lvl != "?") s = OnyxText.T("ур.", "lvl") + lvl;
        }
        catch { }
        try
        {
            if (c.PlatformData != null)
            {
                string pl = Plat(c.PlatformData.Platform);
                if (pl.Length > 0) s = s.Length > 0 ? s + " · " + pl : pl;
            }
        }
        catch { }
        return s;
    }

    private static string Plat(Platforms p)
    {
        return (int)p switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS",
            5 => "Itch",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
            _ => string.Empty,
        };
    }

    private string _homeAbout, _homeImp;
    private float _homeAboutH, _homeImpH, _homeTextW = -1f;

    private void DrawHome(float x, ref float y, float w)
    {
        string about = OnyxText.T(
            "<b>Onyx</b> — клиент-сайд мод-меню для Among Us.",
            "<b>Onyx</b> — a client-side mod menu for Among Us.");
        MeasureHome(about, w);
        float ah = _homeAboutH;
        Rect b = Card(x, ref y, w, OnyxText.T("О моде", "About"), ah + 2f * 30f + 8f);
        GUI.Label(new Rect(b.x, b.y, b.width, ah), about, _rowLabel);
        float by = b.y + ah + 8f;
        InfoRow(b.x, ref by, b.width, OnyxText.T("Версия", "Version"), "v" + OnyxPlugin.PluginVersion);
        InfoRow(b.x, ref by, b.width, OnyxText.T("Автор", "Author"), "Kawasaki");

        b = Card(x, ref y, w, OnyxText.T("Горячие клавиши", "Hotkeys"), 12f * 30f);
        by = b.y;
        InfoRow(b.x, ref by, b.width, OnyxText.T("Меню", "Menu"), KeyDisp(OnyxConfig.MenuKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Код лобби", "Lobby code"), KeyDisp(OnyxConfig.CopyCodeKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Завершить матч", "End match"), KeyDisp(OnyxConfig.EndMatchKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Досчитать голоса", "Tally votes"), KeyDisp(OnyxConfig.CloseVotingKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Закрыть собрание", "Close meeting"), KeyDisp(OnyxConfig.CloseMeetingKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Открыть плеер", "Open player"), KeyDisp(OnyxConfig.MusicToggleKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Предыдущий трек", "Previous track"), KeyDisp(OnyxConfig.MusicPrevKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Следующий трек", "Next track"), KeyDisp(OnyxConfig.MusicNextKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Играть / Пауза", "Play / Pause"), KeyDisp(OnyxConfig.MusicPlayPauseKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Стоп", "Stop"), KeyDisp(OnyxConfig.MusicStopKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Громкость +", "Volume up"), KeyDisp(OnyxConfig.MusicVolumeUpKey));
        InfoRow(b.x, ref by, b.width, OnyxText.T("Громкость -", "Volume down"), KeyDisp(OnyxConfig.MusicVolumeDownKey));

        string imp = OnyxText.T(
            "<color=#FFD166>Мод может конфликтовать с другими модами. Перед запуском отключи или удали остальные — меньше вылетов и багов.</color>",
            "<color=#FFD166>The mod may conflict with other mods. Disable or remove other mods before launching to avoid crashes and bugs.</color>");
        float ih = _homeImpH;
        b = Card(x, ref y, w, OnyxText.T("Важно", "Important"), ih);
        GUI.Label(b, imp, _rowLabel);

        DrawUpdate(x, ref y, w);

        b = Card(x, ref y, w, OnyxText.T("Ссылки", "Links"), 2f * RowH);
        by = b.y;
        LinkRow(b.x, ref by, b.width, "GitHub", "https://github.com/Kawas-set1/OnyxMenu");
        LinkRow(b.x, ref by, b.width, "Discord", "https://discord.gg/cP4MrVUfM7");

        b = Card(x, ref y, w, OnyxText.T("Быстрые действия", "Quick actions"), 48f);
        ActionRow(b, OnyxIcon.Bell, OnyxText.T("Тест уведомления", "Test notification"), OnyxText.T("Проверить", "Test"), () => OnyxToast.Push(OnyxText.T("Onyx на связи ✓", "Onyx is live ✓")));
    }

    private void MeasureHome(string about, float w)
    {
        string imp = OnyxText.T(
            "<color=#FFD166>Мод может конфликтовать с другими модами. Перед запуском отключи или удали остальные — меньше вылетов и багов.</color>",
            "<color=#FFD166>The mod may conflict with other mods. Disable or remove other mods before launching to avoid crashes and bugs.</color>");
        if (Mathf.Abs(w - _homeTextW) < 0.5f && ReferenceEquals(about, _homeAbout) && ReferenceEquals(imp, _homeImp)) return;
        _homeTextW = w; _homeAbout = about; _homeImp = imp;
        float tw = w - 36f;
        _homeAboutH = _rowLabel.CalcHeight(new GUIContent(about), tw);
        _homeImpH = _rowLabel.CalcHeight(new GUIContent(imp), tw);
    }

    private void DrawUpdate(float x, ref float y, float w)
    {
        UpState st = OnyxUpdateCheck.State;
        Rect b = Card(x, ref y, w, OnyxText.T("Обновление", "Update"), 30f + 30f);
        float by = b.y;

        string info;
        switch (st)
        {
            case UpState.Checking: info = OnyxText.T("Проверяю…", "Checking…"); break;
            case UpState.Found: info = OnyxText.T("Доступна v", "Version v") + OnyxUpdateCheck.Latest; break;
            case UpState.Loading: info = OnyxText.T("Качаю…", "Downloading…"); break;
            case UpState.Done: info = OnyxText.T("Готово — перезапусти игру", "Done — restart the game"); break;
            case UpState.Fail: info = OnyxText.T("Ошибка: ", "Error: ") + OnyxUpdateCheck.Err; break;
            default: info = OnyxText.T("Установлена последняя: v", "Up to date: v") + OnyxPlugin.PluginVersion; break;
        }
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), info, _muted);
        by += 30f;

        float cw = (b.width - 10f) / 2f;
        bool busy = st == UpState.Checking || st == UpState.Loading;
        if (SmallButton(new Rect(b.x, by, cw, 26f), OnyxText.T("ПРОВЕРИТЬ", "CHECK"), busy ? new Color(0.5f, 0.5f, 0.58f) : OnyxStyle.Current.Accent) && !busy)
            OnyxUpdateCheck.Recheck();

        if (st == UpState.Done)
        {
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("ВЫЙТИ ИЗ ИГРЫ", "QUIT GAME"), new Color(0.9f, 0.4f, 0.4f)))
                OnyxUpdateCheck.Restart();
        }
        else if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("СКАЧАТЬ", "DOWNLOAD"), st == UpState.Found ? new Color(0.30f, 0.72f, 0.40f) : new Color(0.5f, 0.5f, 0.58f)) && st == UpState.Found)
            OnyxUpdateCheck.Download();
    }

    private void LinkRow(float x, ref float y, float w, string label, string url)
    {
        OnyxPalette p = OnyxStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        bool hover = r.Contains(M);
        OnyxStyle.FillRounded(r, hover ? A(p.Accent, 0.18f) : A(Color.white, 0.04f), 8);
        OnyxStyle.StrokeRounded(r, hover ? A(p.Accent, 0.5f) : A(Color.white, 0.08f), 8, 1);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width - 48f, r.height), label, _rowLabel);
        GUI.Label(new Rect(r.x + w - 40f, r.y, 34f, r.height), "↗", _value);
        if (!string.IsNullOrEmpty(url) && GUI.Button(r, GUIContent.none, _invisible))
            OpenLink(url);
        y += RowH;
    }

    private void DrawQoL(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, OnyxText.T("Отображение", "Display"), 4f * RowH + 108f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Счётчик FPS", "FPS counter"), OnyxConfig.ShowFps);
        Toggle(b.x, ref by, b.width, OnyxText.T("Таймер лобби", "Lobby timer"), OnyxConfig.ShowLobbyTimer);
        Toggle(b.x, ref by, b.width, OnyxText.T("Уведомления", "Notifications"), OnyxConfig.Toasts);
        Toggle(b.x, ref by, b.width, OnyxText.T("Лок FPS на 30", "Lock FPS to 30"), OnyxConfig.FpsLock30);
        FpsSlider(b.x, ref by, b.width, OnyxText.T("Лимит FPS (при анлоке)", "FPS limit (unlocked)"), OnyxConfig.FpsCap);
        Slider(b.x, ref by, b.width, OnyxText.T("Масштаб HUD", "HUD scale"), OnyxConfig.HudScale, 0.6f, 2f, "0.00");

        b = Card(x, ref y, w, OnyxText.T("Чат", "Chat"), 8f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Улучшенный чат", "Better chat"), OnyxConfig.BetterChat);
        Toggle(b.x, ref by, b.width, OnyxText.T("Инфо над пузырями (ур./платформа)", "Info above bubbles (lvl/platform)"), OnyxConfig.ChatBubbleSenderInfo);
        Toggle(b.x, ref by, b.width, OnyxText.T("Тёмный чат под тему", "Dark chat theme"), OnyxConfig.DarkChatTheme);
        Toggle(b.x, ref by, b.width, OnyxText.T("Чат всегда виден", "Chat always visible"), OnyxConfig.VisualAlwaysShowChat);
        Toggle(b.x, ref by, b.width, OnyxText.T("Видеть чат мёртвых", "See dead chat"), OnyxConfig.GhostChat);
        Toggle(b.x, ref by, b.width, OnyxText.T("Без лимита длины", "No length limit"), OnyxConfig.UnlimitedChatLength);
        Toggle(b.x, ref by, b.width, OnyxText.T("Без задержки чата", "No chat cooldown"), OnyxConfig.SkipChatCooldown);
        Toggle(b.x, ref by, b.width, OnyxText.T("Окно чата (перетаскиваемое)", "Chat window (draggable)"), OnyxConfig.ChatWindow);

        b = Card(x, ref y, w, OnyxText.T("Лог и фильтр чата", "Chat log & filter"), 6f * RowH + 46f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Лог чата в файл", "Log chat to file"), OnyxConfig.ChatLog);
        Toggle(b.x, ref by, b.width, OnyxText.T("Цензура бан-слов", "Censor banned words"), OnyxConfig.BanWords);
        Toggle(b.x, ref by, b.width, OnyxText.T("Команда /xmas (хост)", "/xmas command (host)"), OnyxConfig.ChatCmdXmas);
        Toggle(b.x, ref by, b.width, OnyxText.T("Колор-команды /c /color", "Color commands /c /color"), OnyxConfig.ColorCmd);
        Toggle(b.x, ref by, b.width, OnyxText.T("Показывать кто вписал", "Notify who used it"), OnyxConfig.ColorCmdNotify);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), OnyxText.T("Хост красит любого, не-хост — себя. Пример: /c red", "Host colors anyone, non-host colors self. e.g. /c red"), _muted);
        by += 22f;
        Toggle(b.x, ref by, b.width, OnyxText.T("Команды хоста (/kick /role /start…)", "Host commands (/kick /role /start…)"), OnyxConfig.ChatCmds);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), OnyxText.T("Пиши /help в чат — список команд.", "Type /help in chat for the list."), _muted);

        bool ev = OnyxConfig.EventNotify.Value;
        b = Card(x, ref y, w, OnyxText.T("Уведомления о событиях", "Event notifications"), (ev ? 9f : 2f) * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Показывать уведомления", "Show notifications"), OnyxConfig.EventNotify);
        Toggle(b.x, ref by, b.width, OnyxText.T("Консоль событий (окно)", "Event console (window)"), OnyxConfig.EventConsole);
        if (ev)
        {
            Toggle(b.x, ref by, b.width, OnyxText.T("Дублировать в чат", "Also in chat"), OnyxConfig.EventNotifyChat);
            Toggle(b.x, ref by, b.width, OnyxText.T("Войткики", "Votekicks"), OnyxConfig.NotifyVotekick);
            Toggle(b.x, ref by, b.width, OnyxText.T("Саботаж", "Sabotage"), OnyxConfig.NotifySabotage);
            Toggle(b.x, ref by, b.width, OnyxText.T("Убийства", "Kills"), OnyxConfig.NotifyKill);
            Toggle(b.x, ref by, b.width, OnyxText.T("Собрания", "Meetings"), OnyxConfig.NotifyMeeting);
            Toggle(b.x, ref by, b.width, OnyxText.T("Изгнания", "Ejections"), OnyxConfig.NotifyEject);
            Toggle(b.x, ref by, b.width, OnyxText.T("Защита (блоки)", "Guard (blocks)"), OnyxConfig.SecurityNotify);
        }

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int wsCount = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) wsCount++;
        b = Card(x, ref y, w, OnyxText.T("Шепот", "Whisper"), 26f + (wsCount > 0 ? wsCount * 32f : 26f));
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Или в чате: /w [ник или ID] сообщение", "Or in chat: /w [name or ID] message"), _muted);
        by += 26f;
        if (wsCount == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Нет других игроков.", "No other players."), _muted);
        else
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) WhisperRow(b.x, ref by, b.width, _roleClients[i]);
    }

    private struct RoleDef
    {
        public string Ru, En;
        public RoleTypes Role;
        public bool Imp;
        public float DetailH;
        public RoleDef(string ru, string en, RoleTypes role, bool imp, float dh) { Ru = ru; En = en; Role = role; Imp = imp; DetailH = dh; }
    }

    private static readonly RoleDef[] HostRoles =
    {
        new RoleDef("Учёный", "Scientist", RoleTypes.Scientist, false, 100f),
        new RoleDef("Инженер", "Engineer", RoleTypes.Engineer, false, 100f),
        new RoleDef("Ангел", "Guardian Angel", RoleTypes.GuardianAngel, false, 134f),
        new RoleDef("Следопыт", "Tracker", RoleTypes.Tracker, false, 150f),
        new RoleDef("Паникёр", "Noisemaker", RoleTypes.Noisemaker, false, 84f),
        new RoleDef("Детектив", "Detective", RoleTypes.Detective, false, 50f),
        new RoleDef("Оборотень", "Shapeshifter", RoleTypes.Shapeshifter, true, 134f),
        new RoleDef("Фантом", "Phantom", RoleTypes.Phantom, true, 100f),
        new RoleDef("Гадюка", "Viper", RoleTypes.Viper, true, 50f),
    };

    private void DrawHostTab(float x, ref float y, float w)
    {
        if (!OnyxLobbySettings.Ready())
        {
            Rect hb = Card(x, ref y, w, OnyxText.T("Правила лобби", "Lobby rules"), RowH + 24f);
            GUI.Label(new Rect(hb.x + 4f, hb.y, hb.width - 8f, 44f),
                OnyxText.T("Доступно только хосту в лобби. Создай комнату и стань хостом.",
                           "Host in lobby only. Create a room and become host."), _muted);
            return;
        }

        DrawPresets(x, ref y, w);

        Rect b = Card(x, ref y, w, OnyxText.T("Основное", "Basics"), 2f * RowH + 302f);
        float by = b.y;

        string[] maps = { "Skeld", "Mira HQ", "Polus", "Dleks", "Airship", "Fungle" };
        int mp = Mathf.Clamp(OnyxLobbySettings.Map(), 0, 5);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Карта", "Map"), maps[mp], () => OnyxLobbySettings.SetMap((mp + 1) % 6));

        int pl = OnyxLobbySettings.Players();
        int nPl = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Игроки", "Players"), pl, 4, 15, "");
        if (nPl != pl) OnyxLobbySettings.SetPlayers(nPl);

        int im = OnyxLobbySettings.Imps();
        int nIm = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Импостеры", "Impostors"), im, 1, 3, "");
        if (nIm != im) OnyxLobbySettings.SetImps(nIm);

        float kc = OnyxLobbySettings.KillCd();
        float nKc = SliderFloatVal(b.x, ref by, b.width, OnyxText.T("Кулдаун убийства", "Kill cooldown"), kc, 0f, 60f, 0.1f, "0.0", "с");
        if (Mathf.Abs(nKc - kc) > 0.001f) OnyxLobbySettings.SetKillCd(nKc);

        string[] dists = { OnyxText.T("Короткая", "Short"), OnyxText.T("Средняя", "Medium"), OnyxText.T("Длинная", "Long") };
        int kd = Mathf.Clamp(OnyxLobbySettings.KillDist(), 0, 2);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Дистанция убийств", "Kill distance"), dists[kd], () => OnyxLobbySettings.SetKillDist((kd + 1) % 3));

        int sp = Mathf.RoundToInt(OnyxLobbySettings.Speed() * 100f);
        int nSp = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Скорость", "Speed"), sp, 25, 300, "%");
        if (nSp != sp) OnyxLobbySettings.SetSpeed(nSp / 100f);

        int cv = Mathf.RoundToInt(OnyxLobbySettings.CrewVis() * 100f);
        int nCv = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Обзор мирных", "Crew vision"), cv, 25, 500, "%");
        if (nCv != cv) OnyxLobbySettings.SetCrewVis(nCv / 100f);

        int iv = Mathf.RoundToInt(OnyxLobbySettings.ImpVis() * 100f);
        int nIv = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Обзор предов", "Impostor vision"), iv, 25, 500, "%");
        if (nIv != iv) OnyxLobbySettings.SetImpVis(nIv / 100f);

        b = Card(x, ref y, w, OnyxText.T("Собрания и голосование", "Meetings & voting"), 2f * RowH + 210f);
        by = b.y;

        int me = OnyxLobbySettings.Meetings();
        int nMe = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Экстренных собраний", "Emergency meetings"), me, 0, 15, "");
        if (nMe != me) OnyxLobbySettings.SetMeetings(nMe);

        int mc = OnyxLobbySettings.MeetingCd();
        int nMc = SliderIntVal(b.x, ref by, b.width, OnyxText.T("КД собрания", "Meeting cd"), mc, 0, 60, "с");
        if (nMc != mc) OnyxLobbySettings.SetMeetingCd(nMc);

        int di = OnyxLobbySettings.Discuss();
        int nDi = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Обсуждение", "Discussion"), di, 0, 120, "с");
        if (nDi != di) OnyxLobbySettings.SetDiscuss(nDi);

        int vo = OnyxLobbySettings.Voting();
        int nVo = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Голосование", "Voting"), vo, 0, 300, "с");
        if (nVo != vo) OnyxLobbySettings.SetVoting(nVo);

        bool an = OnyxLobbySettings.Anon();
        bool nAn = ToggleVal(b.x, ref by, b.width, OnyxText.T("Анонимные голоса", "Anonymous votes"), an);
        if (nAn != an) OnyxLobbySettings.SetAnon(nAn);

        bool cf = OnyxLobbySettings.Confirm();
        bool nCf = ToggleVal(b.x, ref by, b.width, OnyxText.T("Подтверждать выброс", "Confirm ejects"), cf);
        if (nCf != cf) OnyxLobbySettings.SetConfirm(nCf);

        b = Card(x, ref y, w, OnyxText.T("Задания", "Tasks"), RowH + 194f);
        by = b.y;

        string[] taskBars = { OnyxText.T("Постоянно", "Always"), OnyxText.T("Только собрания", "Meetings only"), OnyxText.T("Скрыто", "Hidden") };
        int tb = Mathf.Clamp(OnyxLobbySettings.TaskBar(), 0, 2);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Шкала заданий", "Task bar"), taskBars[tb], () => OnyxLobbySettings.SetTaskBar((tb + 1) % 3));

        int co = OnyxLobbySettings.Common();
        int nCo = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Общие задания", "Common tasks"), co, 0, 8, "");
        if (nCo != co) OnyxLobbySettings.SetCommon(nCo);

        int lo = OnyxLobbySettings.Long();
        int nLo = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Длинные задания", "Long tasks"), lo, 0, 8, "");
        if (nLo != lo) OnyxLobbySettings.SetLong(nLo);

        int sh = OnyxLobbySettings.Short();
        int nSh = SliderIntVal(b.x, ref by, b.width, OnyxText.T("Короткие задания", "Short tasks"), sh, 0, 12, "");
        if (nSh != sh) OnyxLobbySettings.SetShort(nSh);

        bool vi = OnyxLobbySettings.Visual();
        bool nVi = ToggleVal(b.x, ref by, b.width, OnyxText.T("Визуальные задания", "Visual tasks"), vi);
        if (nVi != vi) OnyxLobbySettings.SetVisual(nVi);

        float rolesBody = 24f + HostRoles.Length * RowH;
        foreach (RoleDef d in HostRoles) if (_roleOpen.Contains((int)d.Role)) rolesBody += d.DetailH;
        b = Card(x, ref y, w, OnyxText.T("Роли (кол-во/шанс)", "Roles (count/chance)"), rolesBody);
        by = b.y;
        GUI.Label(new Rect(b.x + b.width - 190f, by, 76f, 20f), OnyxText.T("Кол-во", "Count"), _centerMuted);
        GUI.Label(new Rect(b.x + b.width - 100f, by, 96f, 20f), OnyxText.T("Шанс", "Chance"), _centerMuted);
        by += 24f;
        Color crew = new Color(0.42f, 0.80f, 0.72f);
        Color imp = new Color(0.92f, 0.38f, 0.38f);
        foreach (RoleDef d in HostRoles)
        {
            bool open = RoleRow(b.x, ref by, b.width, d, d.Imp ? imp : crew);
            if (open) DrawRoleDetails(b.x, ref by, b.width, d.Role);
        }
    }

    private void DrawPresets(float x, ref float y, float w)
    {
        var names = OnyxLobbyPresets.Names();
        int pn = names.Count;
        Rect b = Card(x, ref y, w, OnyxText.T("Пресеты", "Presets"), 34f + (pn > 0 ? pn * RowH : RowH) + 6f);
        float by = b.y;

        _presetName = CustomText(new Rect(b.x + 2f, by, b.width - 128f, 26f), _presetName ?? "", "onyxPreset");
        if (string.IsNullOrEmpty(_presetName) && _textFocus != "onyxPreset")
            GUI.Label(new Rect(b.x + 10f, by, b.width - 140f, 26f), OnyxText.T("Имя пресета…", "Preset name…"), _muted);
        if (SmallButton(new Rect(b.x + b.width - 120f, by + 1f, 118f, 24f), OnyxText.T("СОХРАНИТЬ", "SAVE"), OnyxStyle.Current.Accent))
        {
            OnyxToast.Push(OnyxText.T("Пресет", "Preset"), OnyxLobbyPresets.Save(_presetName), 2.5f, OnyxNotifyKind.Info);
            _presetName = "";
            _textFocus = null;
        }
        by += 34f;

        if (pn == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Пусто. Задай имя и сохрани текущие настройки.", "Empty. Name it and save current settings."), _muted);
            return;
        }

        for (int i = 0; i < pn; i++)
        {
            string name = names[i];
            var r = new Rect(b.x, by, b.width, RowH - 4f);
            HoverFill(r);
            if (GUI.Button(new Rect(r.x, r.y, r.width - 34f, r.height), GUIContent.none, _invisible))
                OnyxToast.Push(OnyxText.T("Пресет", "Preset"), OnyxLobbyPresets.Apply(name), 2f, OnyxNotifyKind.Success);
            OnyxStyle.FillRounded(new Rect(r.x + 10f, r.y + (r.height - 8f) / 2f, 8f, 8f), OnyxStyle.Current.Accent, 4);
            GUI.Label(new Rect(r.x + 26f, r.y, r.width - 70f, r.height), name, _rowLabel);
            GUI.Label(new Rect(r.x + r.width - 80f, r.y, 44f, r.height), "▸", _value);
            var del = new Rect(r.xMax - 28f, r.y + (r.height - 22f) / 2f, 24f, 22f);
            if (ArrowButton(del, "✕")) OnyxLobbyPresets.Delete(name);
            by += RowH;
        }
    }

    private bool RoleRow(float x, ref float y, float w, RoleDef d, Color team)
    {
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        int key = (int)d.Role;
        bool open = _roleOpen.Contains(key);
        if (GUI.Button(new Rect(r.x, r.y, r.width - 200f, r.height), GUIContent.none, _invisible))
        {
            if (open) _roleOpen.Remove(key); else _roleOpen.Add(key);
            open = !open;
        }
        GUI.Label(new Rect(r.x + 6f, r.y, 16f, r.height), open ? "▾" : "▸", _centerMuted);
        OnyxStyle.FillRounded(new Rect(r.x + 24f, r.y + (r.height - 8f) / 2f, 8f, 8f), team, 4);
        GUI.Label(new Rect(r.x + 38f, r.y, r.width - 238f, r.height), OnyxText.T(d.Ru, d.En), _rowLabel);

        int cnt = OnyxLobbySettings.RoleNum(d.Role);
        int chc = OnyxLobbySettings.RoleChance(d.Role);
        int nCnt = Stepper(new Rect(r.xMax - 190f, r.y, 76f, r.height), cnt, 0, 15, 1, "");
        int nChc = Stepper(new Rect(r.xMax - 100f, r.y, 96f, r.height), chc, 0, 100, 10, "%");
        if (nCnt != cnt || nChc != chc) OnyxLobbySettings.SetRole(d.Role, nCnt, nChc);
        y += RowH;
        return open;
    }

    private void DrawRoleDetails(float x, ref float y, float w, RoleTypes role)
    {
        switch (role)
        {
            case RoleTypes.Scientist:
                RoleFloat(role, x, ref y, w, "Перезарядка сканера", "Scan cooldown", OnyxLobbySettings.SciCd, OnyxLobbySettings.SetSciCd, 0, 60, "с");
                RoleFloat(role, x, ref y, w, "Время батареи", "Battery time", OnyxLobbySettings.SciBat, OnyxLobbySettings.SetSciBat, 0, 30, "с");
                break;
            case RoleTypes.Engineer:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", OnyxLobbySettings.EngCd, OnyxLobbySettings.SetEngCd, 0, 60, "с");
                RoleFloat(role, x, ref y, w, "Время в венте", "Vent time", OnyxLobbySettings.EngVent, OnyxLobbySettings.SetEngVent, 0, 60, "с");
                break;
            case RoleTypes.GuardianAngel:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", OnyxLobbySettings.GaCd, OnyxLobbySettings.SetGaCd, 0, 60, "с");
                RoleFloat(role, x, ref y, w, "Время щита", "Shield time", OnyxLobbySettings.GaDur, OnyxLobbySettings.SetGaDur, 0, 30, "с");
                RoleBool(x, ref y, w, "Преды видят щит", "Impostors see shield", OnyxLobbySettings.GaImpSee, OnyxLobbySettings.SetGaImpSee);
                break;
            case RoleTypes.Tracker:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", OnyxLobbySettings.TrCd, OnyxLobbySettings.SetTrCd, 0, 60, "с");
                RoleFloat(role, x, ref y, w, "Длительность", "Duration", OnyxLobbySettings.TrDur, OnyxLobbySettings.SetTrDur, 0, 60, "с");
                RoleFloat(role, x, ref y, w, "Задержка метки", "Ping delay", OnyxLobbySettings.TrDelay, OnyxLobbySettings.SetTrDelay, 0, 30, "с");
                break;
            case RoleTypes.Noisemaker:
                RoleFloat(role, x, ref y, w, "Длит. сигнала", "Alert duration", OnyxLobbySettings.NmDur, OnyxLobbySettings.SetNmDur, 0, 30, "с");
                RoleBool(x, ref y, w, "Преды видят сигнал", "Impostors see alert", OnyxLobbySettings.NmImpAlert, OnyxLobbySettings.SetNmImpAlert);
                break;
            case RoleTypes.Detective:
                RoleFloat(role, x, ref y, w, "Лимит улик", "Suspect limit", OnyxLobbySettings.DetLimit, OnyxLobbySettings.SetDetLimit, 0, 10, "");
                break;
            case RoleTypes.Shapeshifter:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", OnyxLobbySettings.SsCd, OnyxLobbySettings.SetSsCd, 0, 60, "с");
                RoleFloat(role, x, ref y, w, "Длительность", "Duration", OnyxLobbySettings.SsDur, OnyxLobbySettings.SetSsDur, 0, 60, "с");
                RoleBool(x, ref y, w, "Оставлять облик", "Leave skin", OnyxLobbySettings.SsSkin, OnyxLobbySettings.SetSsSkin);
                break;
            case RoleTypes.Phantom:
                RoleFloat(role, x, ref y, w, "Перезарядка", "Cooldown", OnyxLobbySettings.PhCd, OnyxLobbySettings.SetPhCd, 0, 60, "с");
                RoleFloat(role, x, ref y, w, "Длительность", "Duration", OnyxLobbySettings.PhDur, OnyxLobbySettings.SetPhDur, 0, 60, "с");
                break;
            case RoleTypes.Viper:
                RoleFloat(role, x, ref y, w, "Время растворения", "Dissolve time", OnyxLobbySettings.VpDis, OnyxLobbySettings.SetVpDis, 0, 60, "с");
                break;
        }
    }

    private void RoleFloat(RoleTypes role, float x, ref float y, float w, string ru, string en, Func<float> get, Action<float> set, int min, int max, string suffix)
    {
        int v = Mathf.RoundToInt(get());
        int nv = SliderIntVal(x + 14f, ref y, w - 14f, OnyxText.T(ru, en), v, min, max, suffix, ru.GetHashCode() ^ (((int)role + 1) * 397));
        if (nv != v) set(nv);
    }

    private void RoleBool(float x, ref float y, float w, string ru, string en, Func<bool> get, Action<bool> set)
    {
        bool v = get();
        bool nv = ToggleVal(x + 14f, ref y, w - 14f, OnyxText.T(ru, en), v);
        if (nv != v) set(nv);
    }

    private int Stepper(Rect r, int value, int min, int max, int step, string suffix)
    {
        value = Mathf.Clamp(value, min, max);
        var lb = new Rect(r.x, r.y + (r.height - 20f) / 2f, 20f, 20f);
        var rb = new Rect(r.xMax - 20f, r.y + (r.height - 20f) / 2f, 20f, 20f);
        if (ArrowButton(lb, "◂")) value = Mathf.Clamp(value - step, min, max);
        if (ArrowButton(rb, "▸")) value = Mathf.Clamp(value + step, min, max);
        GUI.Label(new Rect(lb.xMax, r.y, rb.x - lb.xMax, r.height), value + suffix, _valueC);
        return Mathf.Clamp(value, min, max);
    }

    private void DrawLobbyTab(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, OnyxText.T("Косметика", "Cosmetics"), 2f * RowH);
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Водяной знак Onyx", "Onyx watermark"), OnyxConfig.LobbyBrand);
        Toggle(b.x, ref by, b.width, OnyxText.T("Лобби-бар (СТАРТ, чипы)", "Lobby bar (START, chips)"), OnyxConfig.LobbyBar);

        b = Card(x, ref y, w, OnyxText.T("Старт", "Start"), 4f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Разблок. кнопку Старт", "Unlock Start button"), OnyxConfig.AlwaysUnlockStartButton);
        Toggle(b.x, ref by, b.width, OnyxText.T("Старт по Enter", "Start on Enter"), OnyxConfig.QuickStartOnEnter);
        Toggle(b.x, ref by, b.width, OnyxText.T("Мгновенный старт (Enter)", "Instant start (Enter)"), OnyxConfig.InstantStartOnEnter);
        Toggle(b.x, ref by, b.width, OnyxText.T("Авто-возврат в лобби", "Auto-return to lobby"), OnyxConfig.AutoReturnLobbyAfterMatch);

        b = Card(x, ref y, w, OnyxText.T("Прочее", "Misc"), 2f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Отключить музыку лобби", "Mute lobby music"), OnyxConfig.MuteLobbyMusic);
        Toggle(b.x, ref by, b.width, OnyxText.T("Расширенный браузер лобби", "Rich lobby browser"), OnyxConfig.RichLobbyRows);

        b = Card(x, ref y, w, OnyxText.T("Автохост", "Auto-host"), 7f * RowH + 100f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Включить автохост", "Enable auto-host"), OnyxConfig.AutoHostEnabled);
        Toggle(b.x, ref by, b.width, OnyxText.T("Мгновенный старт", "Instant start"), OnyxConfig.AutoHostInstantStart);
        Toggle(b.x, ref by, b.width, OnyxText.T("Возврат в лобби после матча", "Return to lobby after match"), OnyxConfig.AutoHostReturnAfterMatch);
        Toggle(b.x, ref by, b.width, OnyxText.T("Ждать загрузку игроков", "Wait for players to load"), OnyxConfig.AutoHostWaitLoadedPlayers);
        Toggle(b.x, ref by, b.width, OnyxText.T("Форс в последнюю минуту", "Force in last minute"), OnyxConfig.AutoHostForceLastMinute);
        Toggle(b.x, ref by, b.width, OnyxText.T("Уведомления автохоста", "Auto-host notifications"), OnyxConfig.AutoHostNotifications);
        bool gmBefore = OnyxConfig.GameMaster.Value;
        Toggle(b.x, ref by, b.width, OnyxText.T("Мастер игры", "Game master"), OnyxConfig.GameMaster);
        if (OnyxConfig.GameMaster.Value && !gmBefore) OnyxConfig.GhostAfterStart.Value = false;
        SliderInt(b.x, ref by, b.width, OnyxText.T("Минимум игроков", "Min players"), OnyxConfig.AutoHostMinPlayers, 1, 15);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Задержка старта, с", "Start delay, s"), OnyxConfig.AutoHostStartDelaySeconds, 0, 180);

        b = Card(x, ref y, w, OnyxText.T("Манекены (хост)", "Dummies (host)"), 4f * RowH + 40f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Включить манекенов", "Enable dummies"), OnyxConfig.DummyEnabled);
        Toggle(b.x, ref by, b.width, OnyxText.T("Делают таски", "Do tasks"), OnyxConfig.DummyDoTasks);
        Toggle(b.x, ref by, b.width, OnyxText.T("Чинят саботаж", "Fix sabotage"), OnyxConfig.DummyFixSabotage);
        Toggle(b.x, ref by, b.width, OnyxText.T("Репорт тел + чат + голос", "Report bodies + chat + vote"), OnyxConfig.DummyReportBodies);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 160f, 24f), $"{OnyxText.T("Клавиша спавна", "Spawn key")}: <b>{KeyName(OnyxConfig.DummyKey)}</b>", _muted);
        if (SmallButton(new Rect(b.x + b.width - 150f, by, 148f, 24f), OnyxText.T("СПАВН МАНЕКЕНА", "SPAWN DUMMY"), OnyxStyle.Current.Accent))
            OnyxToast.Push(OnyxText.T("Манекен", "Dummy"), OnyxDummies.SpawnNow(), 2.5f, OnyxNotifyKind.Info);

        b = Card(x, ref y, w, OnyxText.T("Геймплей лобби", "Lobby gameplay"), 7f * RowH + 6f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Без условий победы", "No win conditions"), OnyxConfig.NoWinConditions);
        Toggle(b.x, ref by, b.width, OnyxText.T("2 преда в прятках", "2 seekers in hide & seek"), OnyxConfig.HideAndSeekTwoSeekers);
        Toggle(b.x, ref by, b.width, OnyxText.T("Пред без форы (прятки)", "Seeker: skip head start"), OnyxConfig.SeekerInstantStart);
        Toggle(b.x, ref by, b.width, OnyxText.T("4 импостера (≥9 игроков)", "4 impostors (≥9 players)"), OnyxConfig.FourImpostors);
        Toggle(b.x, ref by, b.width, OnyxText.T("Снять лимиты настроек", "Unlock option limits"), OnyxConfig.LooseHostOptions);
        Toggle(b.x, ref by, b.width, OnyxText.T("Шаг настроек 0.1", "Option step 0.1"), OnyxConfig.ForceMinValues);
        Toggle(b.x, ref by, b.width, OnyxText.T("Копировать код при дисконнекте", "Copy code on disconnect"), OnyxConfig.CopyCodeOnDisconnect);

        b = Card(x, ref y, w, OnyxText.T("Фейк-карта (хост)", "Fake map (host)"), RowH + 30f);
        by = b.y;
        string[] fmNames = { "The Skeld", "MIRA HQ", "Polus", "dlekS", "Airship", "Fungle" };
        int fmi = Mathf.Clamp(OnyxConfig.FakeMapId.Value, 0, fmNames.Length - 1);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Карта", "Map"), fmNames[fmi], () => { OnyxConfig.FakeMapId.Value = (fmi + 1) % fmNames.Length; });
        bool fmOn = OnyxFakeMap.Active;
        if (SmallButton(new Rect(b.x + 2f, by, 176f, 24f), fmOn ? OnyxText.T("ВЫКЛ ФЕЙК-КАРТУ", "FAKE MAP OFF") : OnyxText.T("ВКЛ ФЕЙК-КАРТУ", "FAKE MAP ON"), fmOn ? new Color(0.9f, 0.4f, 0.4f) : OnyxStyle.Current.Accent))
        {
            if (fmOn) OnyxFakeMap.DisableAndRestoreLobby();
            else OnyxFakeMap.Enable(OnyxConfig.FakeMapId.Value);
        }

        int wt = Mathf.Clamp(OnyxConfig.LobbyWeather.Value, 0, 4);
        b = Card(x, ref y, w, OnyxText.T("Оформление лобби", "Lobby appearance"), 4f * RowH + 24f + (wt > 0 ? 50f : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Тёмная тема лобби", "Dark lobby theme"), OnyxConfig.LobbyTheme);
        Toggle(b.x, ref by, b.width, OnyxText.T("Своя картинка в главном меню", "Own main menu picture"), OnyxConfig.MainMenuArt);
        Toggle(b.x, ref by, b.width, OnyxText.T("Анимации старта и панели", "Start & panel animations"), OnyxConfig.LobbyAnims);
        string[] wNames =
        {
            OnyxText.T("Выкл", "Off"), OnyxText.T("Снег", "Snow"), OnyxText.T("Дождь", "Rain"),
            OnyxText.T("Листья", "Leaves"), OnyxText.T("Конфетти", "Confetti")
        };
        CycleRow(b.x, ref by, b.width, OnyxText.T("Погода", "Weather"), wNames[wt], () => { OnyxConfig.LobbyWeather.Value = (wt + 1) % 5; });
        if (wt > 0) SliderInt(b.x, ref by, b.width, OnyxText.T("Частиц", "Particles"), OnyxConfig.LobbySnowAmount, 10, 400);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Погоду видишь только ты.", "Only you see the weather."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Клоны лобби", "Lobby clones"), 5f * RowH + 450f + (OnyxConfig.CloneFormationAnim.Value ? 50f : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Режим клонов (ЛКМ=спавн, ПКМ=удал.)", "Clone mode (LMB=spawn, RMB=remove)"), OnyxConfig.LobbyCloneMode);
        Toggle(b.x, ref by, b.width, OnyxText.T("Клон-тень", "Shadow clone"), OnyxConfig.LobbyCloneShadow);
        Toggle(b.x, ref by, b.width, OnyxText.T("Клоны-охрана (кружат)", "Guard clones (orbit)"), OnyxConfig.LobbyCloneGuard);
        Toggle(b.x, ref by, b.width, OnyxText.T("Клоны бродят", "Clones wander"), OnyxConfig.LobbyCloneDrift);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Макс. клонов", "Max clones"), OnyxConfig.LobbyCloneMax, 1, 2000);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Клонов за клик", "Clones per click"), OnyxConfig.LobbyCloneSpawnCount, 1, 20);
        Slider(b.x, ref by, b.width, OnyxText.T("Радиус охраны", "Guard radius"), OnyxConfig.LobbyCloneGuardRadius, 1f, 8f, "0.0");
        Slider(b.x, ref by, b.width, OnyxText.T("Масштаб клонов", "Clone scale"), OnyxConfig.LobbyCloneScale, 0.4f, 2f, "0.00");
        SliderInt(b.x, ref by, b.width, OnyxText.T("Цвет (-1 = свой)", "Color (-1 = own)"), OnyxConfig.LobbyCloneColorId, -1, 17);

        string[] formNames = { OnyxText.T("Линия", "Line"), OnyxText.T("Круг", "Circle"), OnyxText.T("Треугольник", "Triangle"), OnyxText.T("Звезда", "Star"), OnyxText.T("Сердце", "Heart"), OnyxText.T("Ромб", "Diamond"), OnyxText.T("Спираль", "Spiral"), OnyxText.T("Крест", "Cross"), OnyxText.T("Волна", "Wave"), OnyxText.T("Дракон", "Dragon"), OnyxText.T("Персонаж", "Character"), OnyxText.T("Бесконечность", "Infinity"), OnyxText.T("Стрела", "Arrow"), OnyxText.T("Корона", "Crown"), OnyxText.T("Молния", "Lightning"), OnyxText.T("Цветок", "Flower"), OnyxText.T("Полумесяц", "Crescent"), OnyxText.T("Клевер", "Clover"), OnyxText.T("Ёлка", "Tree") };
        int cfi = Mathf.Clamp(OnyxConfig.CloneFormation.Value, 0, formNames.Length - 1);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Формация", "Formation"), formNames[cfi], () => { OnyxConfig.CloneFormation.Value = (cfi + 1) % formNames.Length; });
        Slider(b.x, ref by, b.width, OnyxText.T("Размер формации", "Formation size"), OnyxConfig.CloneFormationScale, 0.3f, 3f, "0.00");
        SliderInt(b.x, ref by, b.width, OnyxText.T("Копии формации", "Formation copies"), OnyxConfig.LobbyCloneFormationCopies, 1, 5);
        Toggle(b.x, ref by, b.width, OnyxText.T("Живые формации", "Living formations"), OnyxConfig.CloneFormationAnim);
        if (OnyxConfig.CloneFormationAnim.Value)
            Slider(b.x, ref by, b.width, OnyxText.T("Скорость движения", "Motion speed"), OnyxConfig.CloneFormationAnimSpeed, 0.2f, 3f, "0.0");
        if (SmallButton(new Rect(b.x + 2f, by, 132f, 24f), OnyxText.T("ПОСТРОИТЬ", "BUILD"), OnyxStyle.Current.Accent))
            OnyxLobbyClones.Instance?.BuildFormation(cfi);
        if (SmallButton(new Rect(b.x + 142f, by, 120f, 24f), OnyxText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.4f, 0.4f)))
            OnyxLobbyClones.Instance?.ClearAll();
        by += 32f;

        _cloneText = CustomText(new Rect(b.x + 2f, by, b.width - 168f, 26f), _cloneText ?? "", "cloneText");
        if (SmallButton(new Rect(b.x + b.width - 160f, by + 1f, 158f, 24f), OnyxText.T("ТЕКСТ ИЗ КЛОНОВ", "TEXT FROM CLONES"), OnyxStyle.Current.Accent))
            OnyxLobbyClones.Instance?.BuildText(_cloneText);

        bool ncReady = OnyxTwins.Ready();
        b = Card(x, ref y, w, OnyxText.T("Сетевые клоны", "Networked clones"), (ncReady ? 4f * RowH + 208f : 26f) + 32f);
        by = b.y;
        if (!ncReady)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Только хост в лобби.", "Host in lobby only."), _muted);
            by += 26f;
        }
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Копии тебя, видят все. Убираются на старте.", "Copies of you, everyone sees. Cleared on start."), _muted);
            by += 26f;
            Toggle(b.x, ref by, b.width, OnyxText.T("Режим клика (ЛКМ=спавн, ПКМ=удал.)", "Click mode (LMB=spawn, RMB=remove)"), OnyxConfig.NetCloneMode);

            PlayerControl nsrc = NetSrc();
            bool mine = nsrc == null || nsrc == PlayerControl.LocalPlayer;
            string sname = mine ? OnyxText.T("Я", "Me") : (nsrc.Data != null ? nsrc.Data.PlayerName : "?");
            CycleRow(b.x, ref by, b.width, OnyxText.T("Внешность", "Look like"), sname, NextNetSrc);
            if (!mine) DrawColorDot(new Rect(b.x + b.width - 132f, by - RowH + 11f, 12f, 12f), nsrc.Data != null && nsrc.Data.DefaultOutfit != null ? nsrc.Data.DefaultOutfit.ColorId : 0);

            int ncf = Mathf.Clamp(OnyxConfig.CloneFormation.Value, 0, formNames.Length - 1);
            CycleRow(b.x, ref by, b.width, OnyxText.T("Формация", "Formation"), formNames[ncf], () => { OnyxConfig.CloneFormation.Value = (ncf + 1) % formNames.Length; });
            Slider(b.x, ref by, b.width, OnyxText.T("Размер формации", "Formation size"), OnyxConfig.CloneFormationScale, 0.3f, 3f, "0.00");
            SliderInt(b.x, ref by, b.width, OnyxText.T("Клонов", "Clones"), OnyxConfig.NetCloneCount, 1, 2000);
            by += 4f;
            float ncw = (b.width - 10f) / 2f;
            if (SmallButton(new Rect(b.x, by, ncw, 26f), mine ? OnyxText.T("КЛОН СЕБЯ", "CLONE SELF") : OnyxText.T("КЛОН ЕГО", "CLONE THEM"), OnyxStyle.Current.Accent))
                OnyxToast.Push(OnyxText.T("Клоны", "Clones"), OnyxTwins.CloneOf(nsrc), 2.5f, OnyxNotifyKind.Info);
            if (SmallButton(new Rect(b.x + ncw + 10f, by, ncw, 26f), OnyxText.T("ФОРМАЦИЯ", "FORMATION"), new Color(0.4f, 0.75f, 1f)))
                OnyxToast.Push(OnyxText.T("Клоны", "Clones"), OnyxTwins.Formation(ncf, OnyxConfig.NetCloneCount.Value, nsrc), 2.5f, OnyxNotifyKind.Info);
            by += 32f;
            _netText = CustomText(new Rect(b.x, by, b.width - 132f, 26f), _netText ?? "", "netCloneText");
            if (SmallButton(new Rect(b.x + b.width - 128f, by + 1f, 128f, 24f), OnyxText.T("ТЕКСТ ИЗ КЛОНОВ", "TEXT FROM CLONES"), new Color(0.4f, 0.75f, 1f)))
                OnyxToast.Push(OnyxText.T("Клоны", "Clones"), OnyxTwins.Text(_netText, nsrc), 2.5f, OnyxNotifyKind.Info);
            by += 32f;
            string ncInfo = $"{OnyxText.T("Клонов", "Clones")}: <b>{OnyxTwins.Count}</b>";
            if (OnyxTwins.Queued > 0) ncInfo += $"   {OnyxText.T("в очереди", "queued")}: <b>{OnyxTwins.Queued}</b>";
            if (OnyxTwins.Figures > 0) ncInfo += $"   {OnyxText.T("фигур", "figures")}: <b>{OnyxTwins.Figures}</b>";
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), ncInfo, _muted);
            by += 26f;
            float ncw2 = (b.width - 10f) / 2f;
            if (SmallButton(new Rect(b.x, by, ncw2, 26f), OnyxText.T("УБРАТЬ ПОСЛЕДНЮЮ", "REMOVE LAST"), new Color(0.92f, 0.62f, 0.30f)))
                OnyxToast.Push(OnyxText.T("Клоны", "Clones"), OnyxTwins.DropLast(), 2f, OnyxNotifyKind.Info);
            if (SmallButton(new Rect(b.x + ncw2 + 10f, by, ncw2, 26f), OnyxText.T("УБРАТЬ ВСЕХ", "CLEAR ALL"), new Color(0.9f, 0.4f, 0.4f)))
                OnyxTwins.ClearAll();
            by += 32f;
        }

        float lbw = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, lbw, 26f), OnyxText.T("РАЗРУШИТЬ ЛОББИ", "DESTROY LOBBY"), new Color(0.9f, 0.4f, 0.4f)))
            OnyxToast.Push(OnyxText.T("Лобби", "Lobby"), OnyxLobbyTools.DestroyLobby(), 2.5f, OnyxNotifyKind.Warning);
        if (SmallButton(new Rect(b.x + lbw + 10f, by, lbw, 26f), OnyxText.T("СОЗДАТЬ ЛОББИ", "CREATE LOBBY"), OnyxStyle.Current.Accent))
            OnyxToast.Push(OnyxText.T("Лобби", "Lobby"), OnyxLobbyTools.CreateLobby(), 2.5f, OnyxNotifyKind.Success);

        IReadOnlyList<OnyxColorReservations.Entry> res = OnyxColorReservations.All();
        b = Card(x, ref y, w, OnyxText.T("Резерв цветов", "Color reservations"), 2f * RowH + (res.Count > 0 ? res.Count * 30f : 26f));
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Закреплять цвет (хост)", "Reserve color (host)"), OnyxConfig.ColorReservationsEnabled);
        if (SmallButton(new Rect(b.x + 2f, by, 260f, 24f), OnyxText.T("ЗАРЕЗЕРВИРОВАТЬ ВЫБРАННОГО", "RESERVE SELECTED"), OnyxStyle.Current.Accent))
            ReserveSelectedColor();
        by += 30f;
        if (res.Count == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Пусто. Выбор игрока — ЛКМ (нужен «Выбор мышью»).", "Empty. Select a player with LMB (needs Mouse select)."), _muted);
        else
            for (int i = 0; i < res.Count; i++)
            {
                OnyxColorReservations.Entry en = res[i];
                var rr = new Rect(b.x, by, b.width, 28f);
                HoverFill(rr);
                DrawColorDot(new Rect(rr.x + 6f, rr.y + 8f, 12f, 12f), en.ColorId);
                GUI.Label(new Rect(rr.x + 26f, rr.y, rr.width - 66f, rr.height), $"<b>{en.Name}</b>   <color=#8A94AC><size=11>{en.Fc}</size></color>", _rowName);
                if (SmallButton(new Rect(rr.xMax - 38f, rr.y + 3f, 34f, 22f), "✕", new Color(0.9f, 0.4f, 0.4f))) { OnyxColorReservations.Remove(en.Fc); break; }
                by += 30f;
            }
    }

    private void DrawCheats(float x, ref float y, float w)
    {
        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int vkCount = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) vkCount++;
        float vkBody = 40f + 30f * 5f + RowH * 3f + (vkCount > 0 ? vkCount * 32f + 22f : 26f);
        Rect b = Card(x, ref y, w, OnyxText.T("Войткик", "Votekick"), vkBody);
        float by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), OnyxText.T("Кик нужен от 3 разных клиентов. Авто: всем → выход → перезаход добирает.", "Kick needs 3 unique clients. Auto: vote all → leave → rejoin stacks."), _muted);
        by += 40f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), OnyxVotekick.Armed ? OnyxText.T("АВТО: ВКЛ — СТОП", "AUTO: ON — STOP") : OnyxText.T("АВТО-ВОЙТКИК", "AUTO VOTEKICK"), OnyxVotekick.Armed ? new Color(0.9f, 0.4f, 0.4f) : OnyxStyle.Current.Accent))
            OnyxVotekick.ToggleAuto();
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), OnyxText.T("ГОЛОСА ВСЕМ + ОСТАТЬСЯ", "VOTE ALL + STAY"), OnyxStyle.Current.Accent))
            OnyxVotekick.VoteAllStay();
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), OnyxText.T("ЗАЯВИТЬ ВСЕМ ПО ОЧЕРЕДИ", "VOTE EACH IN TURN"), new Color(0.78f, 0.42f, 0.95f)))
            OnyxVotekick.RapidAll();
        by += 30f;
        string autoLbl = OnyxVotekick.AutoTargeting
            ? OnyxText.T("АВТО ПО ЦЕЛЯМ: СТОП", "AUTO TARGETS: STOP")
            : OnyxText.T("АВТО ПО ЦЕЛЯМ", "AUTO TARGETS") + " (" + OnyxVotekick.TargetCount + ")";
        if (SmallButton(new Rect(b.x, by, b.width - 130f, 26f), autoLbl, OnyxVotekick.AutoTargeting ? new Color(0.9f, 0.4f, 0.4f) : OnyxStyle.Current.Accent))
            OnyxVotekick.ToggleTargetAuto();
        if (SmallButton(new Rect(b.x + b.width - 124f, by, 124f, 26f), OnyxText.T("СБРОС ЦЕЛЕЙ", "CLEAR TARGETS"), new Color(0.5f, 0.5f, 0.58f)))
            OnyxVotekick.ClearTargets();
        by += 30f;
        bool hostSel = OnyxVotekick.HostIsTarget();
        if (SmallButton(new Rect(b.x, by, b.width, 26f), hostSel ? OnyxText.T("ПРЕСЕТ ХОСТ ✓ — СНЯТЬ", "PRESET HOST ✓ — REMOVE") : OnyxText.T("ПРЕСЕТ: ОТМЕТИТЬ ХОСТА", "PRESET: MARK HOST"), hostSel ? OnyxStyle.Current.Accent : new Color(0.92f, 0.62f, 0.30f)))
            OnyxVotekick.ToggleHostTarget();
        by += 30f;
        Toggle(b.x, ref by, b.width, OnyxText.T("Копировать код лобби", "Copy lobby code"), OnyxConfig.VkCopyCode);
        Toggle(b.x, ref by, b.width, OnyxText.T("Авто-перезаход по коду", "Auto rejoin by code"), OnyxConfig.VkRejoin);
        Toggle(b.x, ref by, b.width, OnyxText.T("Перезаход если войткикают (2 голоса)", "Rejoin if votekicked (2 votes)"), OnyxConfig.VkAutoRejoin);
        if (vkCount == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Нет других игроков.", "No other players."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), OnyxText.T("Выборочно — отметь «АВТО» у нужных:", "Selective — mark AUTO on players:"), _muted);
            by += 22f;
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) VotekickRow(b.x, ref by, b.width, _roleClients[i]);
        }

        bool inMatch = ShipStatus.Instance != null;
        int mapId = OnyxNav.CurrentMapId();
        bool fungle = mapId == 5;
        bool hasO2 = mapId == 0 || mapId == 1 || mapId == 3;
        Color acc = OnyxStyle.Current.Accent;
        Color red = new Color(0.9f, 0.4f, 0.4f);
        float cw = 0f;

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int loopN = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) loopN++;

        b = Card(x, ref y, w, OnyxText.T("Рофл: цикл смерти", "Prank: murder loop"), inMatch ? 38f + (loopN > 0 ? loopN * 32f : 26f) : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 34f),
                OnyxText.T("Только хост, только для своих — в пабликах забанят.", "Host only, friends only — publics will ban you."), _muted);
            by += 38f;
            if (loopN == 0)
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Нет других игроков.", "No other players."), _muted);
            else
                for (int i = 0; i < _roleClients.Count; i++)
                    if (_roleClients[i] != PlayerControl.LocalPlayer) LoopRow(b.x, ref by, b.width, _roleClients[i]);
        }

        bool reach = OnyxConfig.BuffKillReach.Value;
        b = Card(x, ref y, w, OnyxText.T("Бафы ролей", "Role buffs"), 38f + RowH * 23f + 208f + (reach ? 54f : 4f));
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 34f), OnyxText.T("Применяются в матче. Работают и вне хоста (ванильный хост), мод-хост/античит режет сетевые.", "Apply in match. Work off-host too (vanilla host), modded host / anti-cheat blocks networked ones."), _muted);
        by += 38f;

        Sub(b.x, ref by, b.width, OnyxText.T("Общее", "General"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Без кулдаунов", "No cooldowns"), OnyxConfig.BuffNoCd);
        Toggle(b.x, ref by, b.width, OnyxText.T("Ходить внутри вента", "Move inside vent"), OnyxConfig.BuffVentWalk);
        Toggle(b.x, ref by, b.width, OnyxText.T("Лестницы/зиплайн без кд", "No ladder/zipline cd"), OnyxConfig.BuffMapCd);

        Sub(b.x, ref by, b.width, OnyxText.T("Импостер", "Impostor"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Дальность убийства", "Kill reach"), OnyxConfig.BuffKillReach);
        if (reach) Slider(b.x, ref by, b.width, OnyxText.T("Радиус", "Radius"), OnyxConfig.BuffKillDist, 1f, 12f, "0.0");
        Toggle(b.x, ref by, b.width, OnyxText.T("Убивать кого угодно", "Kill anyone"), OnyxConfig.BuffKillAny);
        Toggle(b.x, ref by, b.width, OnyxText.T("Аура убийства", "Kill aura"), OnyxConfig.BuffKillAura);
        Toggle(b.x, ref by, b.width, OnyxText.T("Кулдаун убийства 0 (хост)", "Kill cooldown 0 (host)"), OnyxConfig.BuffNoKillCd);
        Toggle(b.x, ref by, b.width, OnyxText.T("Кулдаун килла 0.1 (вне хоста)", "Kill cooldown 0.1 (off-host)"), OnyxConfig.KillCdSelf);
        Toggle(b.x, ref by, b.width, OnyxText.T("Венты любой ролью", "Vents with any role"), OnyxConfig.BuffVentAny);
        Toggle(b.x, ref by, b.width, OnyxText.T("Таски предом", "Tasks as impostor"), OnyxConfig.BuffImpTasks);
        Toggle(b.x, ref by, b.width, OnyxText.T("Авто-репорт своих киллов", "Auto-report own kills"), OnyxConfig.BuffAutoReport);
        Toggle(b.x, ref by, b.width, OnyxText.T("Саботаж из вента", "Sabotage from vent"), OnyxConfig.BuffVentSab);

        Sub(b.x, ref by, b.width, OnyxText.T("Оборотень", "Shapeshifter"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Вечная маскировка", "Endless shapeshift"), OnyxConfig.BuffSsForever);
        Toggle(b.x, ref by, b.width, OnyxText.T("Без анимации", "No animation"), OnyxConfig.BuffSsQuiet);

        Sub(b.x, ref by, b.width, OnyxText.T("Фантом", "Phantom"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Убийство в невидимости", "Kill while vanished"), OnyxConfig.BuffVanishKill);
        Toggle(b.x, ref by, b.width, OnyxText.T("Вечная невидимость", "Endless invisibility"), OnyxConfig.BuffPhVanish);
        Toggle(b.x, ref by, b.width, OnyxText.T("Видеть невидимого", "See vanished"), OnyxConfig.SeePhantoms);

        Sub(b.x, ref by, b.width, OnyxText.T("Инженер", "Engineer"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Вечный вент", "Endless vent"), OnyxConfig.BuffEngVent);
        Toggle(b.x, ref by, b.width, OnyxText.T("Без кулдауна", "No cooldown"), OnyxConfig.BuffEngCd);

        Sub(b.x, ref by, b.width, OnyxText.T("Учёный", "Scientist"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Вечная батарея", "Endless battery"), OnyxConfig.BuffSciBat);
        Toggle(b.x, ref by, b.width, OnyxText.T("Без кулдауна", "No cooldown"), OnyxConfig.BuffSciCd);

        Sub(b.x, ref by, b.width, OnyxText.T("Детектив", "Detective"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Любая дистанция", "Any range"), OnyxConfig.BuffDetReach);

        Sub(b.x, ref by, b.width, OnyxText.T("Ангел", "Guardian Angel"));
        Toggle(b.x, ref by, b.width, OnyxText.T("Видеть щит", "See shield"), OnyxConfig.SeeProtections);

        b = Card(x, ref y, w, OnyxText.T("Двери", "Doors"), inMatch ? 30f * 2f + RowH + 6f : 26f);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Только в матче.", "In match only."), _muted);
        else
        {
            cw = (b.width - 10f) / 2f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), OnyxText.T("ЗАКРЫТЬ ВСЕ", "CLOSE ALL"), acc)) OnyxDoors.CloseAll();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("ОТКРЫТЬ ВСЕ", "OPEN ALL"), acc)) OnyxDoors.OpenAll();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), OnyxText.T("ЗАПИНИТЬ ВСЕ", "PIN ALL"), acc)) OnyxDoors.PinAll();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("СНЯТЬ ПИНЫ", "UNPIN"), red)) OnyxDoors.UnpinAll();
            by += 30f;
            Toggle(b.x, ref by, b.width, OnyxText.T("Авто-открытие (без миниигры)", "Auto-open (no minigame)"), OnyxConfig.DoorKeepOpen);
        }

        float sabBody = 30f * 4f + RowH * 2f + 4f;
        b = Card(x, ref y, w, OnyxText.T("Саботаж", "Sabotage"), (inMatch ? sabBody : 26f) + RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Обход саботажа связи", "Bypass comms sabotage"), OnyxConfig.CommsBypass);
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Только в матче.", "In match only."), _muted);
        else
        {
            cw = (b.width - 10f) / 2f;
            string mainLbl = mapId == 2 ? OnyxText.T("СЕЙСМИКА", "SEISMIC") : mapId == 4 ? OnyxText.T("КРУШЕНИЕ", "CRASH") : OnyxText.T("РЕАКТОР", "REACTOR");
            if (SmallButton(new Rect(b.x, by, b.width, 26f), OnyxText.T("ПОЧИНИТЬ ВСЁ", "FIX ALL"), new Color(0.30f, 0.72f, 0.40f))) OnyxSabotage.Fix();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), OnyxText.T("САБОТАЖ ВСЕГО", "SABOTAGE ALL"), red)) OnyxSabotage.All();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("СЛУЧАЙНЫЙ", "RANDOM"), acc)) OnyxSabotage.Random();
            by += 30f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), mainLbl, acc)) OnyxSabotage.Main();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("СВЯЗЬ", "COMMS"), acc)) OnyxSabotage.Comms();
            by += 30f;
            if (!fungle && SmallButton(new Rect(b.x, by, hasO2 ? cw : b.width, 26f), OnyxText.T("СВЕТ", "LIGHTS"), acc)) OnyxSabotage.Lights();
            if (hasO2 && SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("КИСЛОРОД", "OXYGEN"), acc)) OnyxSabotage.Oxygen();
            if (!fungle || hasO2) by += 30f;
            if (fungle && SmallButton(new Rect(b.x, by, b.width, 26f), OnyxText.T("ГРИБЫ (MIXUP)", "MUSHROOM MIXUP"), acc)) { OnyxSabotage.Mush(); }
            if (fungle) by += 30f;
            Toggle(b.x, ref by, b.width, OnyxText.T("Спам главного саботажа", "Spam main sabotage"), OnyxConfig.SabSpamReactor);
            if (!fungle) Toggle(b.x, ref by, b.width, OnyxText.T("Держать свет выключенным", "Keep lights off"), OnyxConfig.SabAutoLights);
            if (fungle) Toggle(b.x, ref by, b.width, OnyxText.T("Бесконечные грибы", "Infinite mushroom"), OnyxConfig.SabInfMushroom);
        }

        float fakeBody = inMatch ? 40f + 30f + RowH * 2f : 26f;
        b = Card(x, ref y, w, OnyxText.T("Фейк-задания", "Fake tasks"), fakeBody);
        by = b.y;
        if (!inMatch)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Только в матче.", "In match only."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 36f), OnyxText.T("Проиграть анимацию задания для окружающих (казаться занятым).", "Play a task animation for others (look busy)."), _muted);
            by += 40f;
            cw = (b.width - 20f) / 3f;
            if (SmallButton(new Rect(b.x, by, cw, 26f), OnyxText.T("ЩИТЫ", "SHIELDS"), acc)) OnyxFakeTasks.Shields();
            if (SmallButton(new Rect(b.x + cw + 10f, by, cw, 26f), OnyxText.T("АСТЕРОИДЫ", "ASTEROIDS"), acc)) OnyxFakeTasks.Asteroids();
            if (SmallButton(new Rect(b.x + (cw + 10f) * 2f, by, cw, 26f), OnyxText.T("МУСОР", "GARBAGE"), acc)) OnyxFakeTasks.Garbage();
            by += 30f;
            Toggle(b.x, ref by, b.width, OnyxText.T("Скан в медбэе (постоянно)", "Medbay scan (held)"), OnyxConfig.FakeScan);
            if (OnyxFakeTasks.HasCams())
                Toggle(b.x, ref by, b.width, OnyxText.T("Камеры «заняты»", "Cameras in use"), OnyxConfig.FakeCams);
            else
                GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Камеры — не на этой карте.", "Cameras — not on this map."), _muted);
        }

        bool autoOn = OnyxConfig.AutoTasks.Value;
        b = Card(x, ref y, w, OnyxText.T("Авто-задания", "Auto tasks"), RowH + (autoOn ? 76f : 24f));
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Делать задания сами", "Finish tasks by itself"), OnyxConfig.AutoTasks);
        if (autoOn)
        {
            Slider(b.x, ref by, b.width, OnyxText.T("Пауза между заданиями", "Gap between tasks"), OnyxConfig.AutoTasksDelay, 0.8f, 6f, "0.0");
            int left = OnyxAutoTasks.Left();
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f),
                inMatch ? OnyxText.T("Осталось: ", "Left: ") + $"<b>{left}</b>" : OnyxText.T("Только в матче, мирным.", "In match, crewmate only."), _muted);
        }
        else
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("По одному, не в собрании. Не для предов.", "One by one, never in a meeting. Not for impostors."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Выживание", "Survival"), 2f * RowH + 46f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Бессмертие (God Mode)", "God Mode"), OnyxConfig.GodMode);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Не убить (фейк-вент). От голосования не спасает.", "Can't be killed (fake vent). Not vs vote-out."), _muted);
        by += 24f;
        Toggle(b.x, ref by, b.width, OnyxText.T("Невидимость", "Invisibility"), OnyxConfig.Invisible);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Для других уезжаешь за карту. Взаимодействий/килла нет. ⚠ античит.", "You warp off-map for others. No interaction/kill. ⚠ anti-cheat."), _muted);

        bool lc = OnyxConfig.LagComp.Value;
        bool lcj = lc && OnyxConfig.LagCompJitter.Value;
        float lch = RowH + 40f + (lc ? 2f * RowH + (lcj ? 100f : 0f) : 0f);
        b = Card(x, ref y, w, OnyxText.T("Мираж", "Mirage"), lch);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Включить", "Enable"), OnyxConfig.LagComp);
        if (lc)
        {
            Toggle(b.x, ref by, b.width, OnyxText.T("Стоп-кадр (замер для других)", "Freeze frame (frozen to others)"), OnyxConfig.LagCompFreeze);
            Toggle(b.x, ref by, b.width, OnyxText.T("Мерцание (рваный сигнал)", "Flicker (broken signal)"), OnyxConfig.LagCompJitter);
            if (lcj)
            {
                SliderInt(b.x, ref by, b.width, OnyxText.T("Мерцание мин (кадры)", "Flicker min (frames)"), OnyxConfig.LagCompJitterMin, 1, 30);
                SliderInt(b.x, ref by, b.width, OnyxText.T("Мерцание макс (кадры)", "Flicker max (frames)"), OnyxConfig.LagCompJitterMax, 1, 30);
            }
        }
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 40f), OnyxText.T("Только для других — у себя двигаешься нормально. ⚠ палит античит.", "Others only — you move normally. ⚠ anti-cheat risk."), _muted);

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        int tpCount = 0;
        for (int i = 0; i < _roleClients.Count; i++) if (_roleClients[i] != PlayerControl.LocalPlayer) tpCount++;
        b = Card(x, ref y, w, OnyxText.T("Телепорт к игроку", "Teleport to player"), 24f + (tpCount > 0 ? tpCount * 32f : 26f));
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Прыжок к выбранному (сетевой снап).", "Snap to the selected player."), _muted);
        by += 24f;
        if (tpCount == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Нет других игроков.", "No other players."), _muted);
        else
            for (int i = 0; i < _roleClients.Count; i++)
                if (_roleClients[i] != PlayerControl.LocalPlayer) TpRow(b.x, ref by, b.width, _roleClients[i]);

        b = Card(x, ref y, w, OnyxText.T("Отправить в чат", "Chat sender"), 3f * RowH + 34f);
        by = b.y;
        _chatSend = CustomText(new Rect(b.x + 2f, by, b.width - 4f, 26f), _chatSend ?? "", "chatSend");
        OnyxChatSender.Message = _chatSend ?? "";
        by += 32f;
        float chHalf = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, chHalf, 26f), OnyxText.T("ОТПРАВИТЬ", "SEND"), OnyxStyle.Current.Accent)) OnyxChatSender.SendNow();
        if (SmallButton(new Rect(b.x + chHalf + 10f, by, chHalf, 26f), OnyxChatSender.Spamming ? OnyxText.T("СПАМ: ВКЛ", "SPAM: ON") : OnyxText.T("СПАМ: ВЫКЛ", "SPAM: OFF"), OnyxChatSender.Spamming ? new Color(0.9f, 0.4f, 0.4f) : OnyxStyle.Current.Accent)) OnyxChatSender.Spamming = !OnyxChatSender.Spamming;
        by += 30f;
        Slider(b.x, ref by, b.width, OnyxText.T("Задержка (с)", "Delay (s)"), OnyxConfig.ChatSpamDelay, 1.5f, 10f, "0.0");
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Чат в лобби/митинге. Малая задержка — кик за флуд.", "Chat in lobby/meeting. Low delay — flood kick."), _muted);

        bool spd = OnyxConfig.SpeedMod.Value;
        b = Card(x, ref y, w, OnyxText.T("Движение", "Movement"), 2f * RowH + 22f + (spd ? 50f : 0f));
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Своя скорость", "Custom speed"), OnyxConfig.SpeedMod);
        if (spd)
            Slider(b.x, ref by, b.width, OnyxText.T("Множитель", "Multiplier"), OnyxConfig.SpeedMult, 0f, 3f, "0.0");
        Toggle(b.x, ref by, b.width, OnyxText.T("Инверт управления", "Invert controls"), OnyxConfig.InvertControls);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Клиентское. Большие значения палит античит.", "Client-side. High values trip anti-cheat."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Мышь и призрак", "Mouse & ghost"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Телепорт по ПКМ", "Teleport on RMB"), OnyxConfig.MouseTeleport);
        Toggle(b.x, ref by, b.width, OnyxText.T("Выбор мышью + ресайз колёсиком", "Mouse select + wheel resize"), OnyxConfig.MouseSelect);
        bool gaBefore = OnyxConfig.GhostAfterStart.Value;
        Toggle(b.x, ref by, b.width, OnyxText.T("Призрак после старта", "Ghost after start"), OnyxConfig.GhostAfterStart);
        if (OnyxConfig.GhostAfterStart.Value && !gaBefore) OnyxConfig.GameMaster.Value = false;

        b = Card(x, ref y, w, OnyxText.T("Фан (хост)", "Fun (host)"), 5f * 30f + 24f);
        by = b.y;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("В игре, не в лобби. На офиц. сервере может кикнуть.", "In-game only. May kick on official servers."), _muted);
        by += 24f;
        float cw2 = (b.width - 10f) / 2f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), OnyxText.T("ВСЕ В ЯЙЦА", "ALL TO EGGS"), OnyxStyle.Current.Accent)) FunToast(OnyxLobbyPranks.MassMorphToEgg());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), OnyxText.T("МОРФ В ВЫБРАННОГО", "MORPH TO TARGET"), OnyxStyle.Current.Accent)) FunToast(OnyxLobbyPranks.MorphAllIntoSelected());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), OnyxText.T("РАДУГА", "RAINBOW") + St(OnyxLobbyPranks.RainbowActive), FunCol(OnyxLobbyPranks.RainbowActive))) FunToast(OnyxLobbyPranks.ToggleRainbow());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), OnyxText.T("ЦИКЛ КОСМЕТИКИ", "COSMETIC CYCLE") + St(OnyxLobbyPranks.SkinCycleActive), FunCol(OnyxLobbyPranks.SkinCycleActive))) FunToast(OnyxLobbyPranks.ToggleSkinCycle());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), OnyxText.T("ТАКТ: ", "BEAT: ") + OnyxLobbyPranks.SyncName(), OnyxStyle.Current.Accent)) FunToast(OnyxLobbyPranks.ToggleSync());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), OnyxText.T("РАЗМЕР: ", "SIZE: ") + OnyxLobbyPranks.ScaleName(), OnyxStyle.Current.Accent)) FunToast(OnyxLobbyPranks.CycleScale());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, cw2, 26f), OnyxText.T("ДВИЖЕНИЕ: ", "MOTION: ") + OnyxLobbyPranks.SpinName(), OnyxStyle.Current.Accent)) FunToast(OnyxLobbyPranks.CycleSpin());
        if (SmallButton(new Rect(b.x + cw2 + 10f, by, cw2, 26f), OnyxText.T("АНИМАЦИЯ: ", "ANIM: ") + OnyxLobbyPranks.AnimName(), OnyxStyle.Current.Accent)) FunToast(OnyxLobbyPranks.CycleAnim());
        by += 30f;
        if (SmallButton(new Rect(b.x, by, b.width, 26f), OnyxText.T("СБРОС ОБЛИКА", "RESET LOOK"), new Color(0.9f, 0.4f, 0.4f))) FunToast(OnyxLobbyPranks.ResetAppearance());
    }

    private void DrawPlayers(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, OnyxText.T("Роли и инфо", "Roles & info"), (OnyxConfig.RevealVotes.Value ? 5f : 4f) * RowH);
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Показывать роли всех", "Reveal all roles"), OnyxConfig.RevealRoles);
        Toggle(b.x, ref by, b.width, OnyxText.T("Инфо над игроками (лобби)", "Info above players (lobby)"), OnyxConfig.VisualPlayerInfoNames);
        Toggle(b.x, ref by, b.width, OnyxText.T("Раскрыть Оборотня", "Unmask shapeshifter"), OnyxConfig.UnmaskShapeshifter);
        Toggle(b.x, ref by, b.width, OnyxText.T("Голоса на собрании", "Votes in meeting"), OnyxConfig.RevealVotes);
        if (OnyxConfig.RevealVotes.Value)
            Toggle(b.x, ref by, b.width, OnyxText.T("Раскрывать анонимные", "De-anonymize voters"), OnyxConfig.RevealAnonVotes);

        _roleClients.Clear();
        CollectPlayers(_roleClients);
        float rbBody = 30f + (_roleClients.Count > 0 ? _roleClients.Count * 32f : 26f);
        b = Card(x, ref y, w, OnyxText.T("Форс ролей (хост)", "Force roles (host)"), rbBody);
        by = b.y;
        if (!OnyxForceRoles.Host())
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Только хост. Роли выдаются на старте матча.", "Host only. Roles apply on match start."), _muted);
        else if (_roleClients.Count == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Нет игроков в лобби.", "No players in lobby."), _muted);
        else
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 110f, 24f), $"{OnyxText.T("Назначено", "Assigned")}: <b>{OnyxForceRoles.Count}</b>   {OnyxText.T("выдача на старте", "applied on start")}", _muted);
            if (SmallButton(new Rect(b.x + b.width - 96f, by, 92f, 24f), OnyxText.T("СБРОС", "CLEAR"), new Color(0.9f, 0.4f, 0.4f))) OnyxForceRoles.Clear();
            by += 30f;
            for (int i = 0; i < _roleClients.Count; i++) RoleRow(b.x, ref by, b.width, _roleClients[i]);
        }

        InnerNetClient net = GuardNet();
        _guardClients.Clear();
        CollectClients(_guardClients);
        float listBody = _guardClients.Count > 0 ? _guardClients.Count * (RowH + 6f) : 28f;
        b = Card(x, ref y, w, OnyxText.T("Игроки в лобби", "Players in lobby"), listBody);
        by = b.y;
        if (_guardClients.Count == 0)
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 26f), OnyxText.T("Зайди в лобби как хост.", "Join a lobby as host."), _muted);
        else
            for (int i = 0; i < _guardClients.Count; i++)
                PlayerRow(b.x, ref by, b.width, net, _guardClients[i]);
    }

    private static void FunToast(string msg) => OnyxToast.Push(OnyxText.T("Фан", "Fun"), msg, 2.5f, OnyxNotifyKind.Info);
    private static string St(bool on) => on ? OnyxText.T(": вкл", ": on") : OnyxText.T(": выкл", ": off");
    private static Color FunCol(bool on) => on ? OnyxStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f);

    private static readonly string[] PlatNames = { "Epic", "Steam", "Mac", "MS Store", "Itch", "iOS", "Android", "Switch", "Xbox", "PS", "Starlight" };

    private void DrawNet(float x, ref float y, float w)
    {
        int ping = -1;
        try { if (AmongUsClient.Instance != null) ping = ((InnerNetClient)AmongUsClient.Instance).Ping; }
        catch { ping = -1; }

        Rect b = Card(x, ref y, w, OnyxText.T("Соединение", "Connection"), 3f * 30f);
        float by = b.y;
        InfoRow(b.x, ref by, b.width, OnyxText.T("Пинг", "Ping"), ping >= 0 ? ping + " ms" : "—");
        InfoRow(b.x, ref by, b.width, "FPS", OnyxHud.CurrentFps.ToString());
        InfoRow(b.x, ref by, b.width, OnyxText.T("Лобби", "Lobby"), LobbyBehaviour.Instance != null ? OnyxText.T("в лобби", "in lobby") : OnyxText.T("нет", "no"));

        float spoofBody = 5f * RowH + 30f + (OnyxConfig.SpoofLevelEnabled.Value ? 50f : 0f);
        b = Card(x, ref y, w, OnyxText.T("Спуф", "Spoof"), spoofBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Спуф платформы", "Spoof platform"), OnyxConfig.SpoofPlatformEnabled);
        int pi = Mathf.Clamp(OnyxConfig.SpoofPlatformIndex.Value, 0, 10);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Платформа", "Platform"), PlatNames[pi], () => { OnyxConfig.SpoofPlatformIndex.Value = (pi + 1) % PlatNames.Length; });
        Toggle(b.x, ref by, b.width, OnyxText.T("Спуф уровня", "Spoof level"), OnyxConfig.SpoofLevelEnabled);
        if (OnyxConfig.SpoofLevelEnabled.Value)
            SliderInt(b.x, ref by, b.width, OnyxText.T("Уровень", "Level"), OnyxConfig.SpoofLevelValue, 1, 9999);
        Toggle(b.x, ref by, b.width, OnyxText.T("Спуф Device ID", "Spoof Device ID"), OnyxConfig.SpoofDeviceId);
        if (SmallButton(new Rect(b.x, by, b.width, 24f), OnyxText.T("РАНДОМ ОБРАЗА", "RANDOM OUTFIT"), OnyxStyle.Current.Accent))
            OnyxToast.Push(OnyxText.T("Образ", "Outfit"), OnyxOutfits.Randomize(PlayerControl.LocalPlayer), 2f, OnyxNotifyKind.Info);
        by += 30f;
        Toggle(b.x, ref by, b.width, OnyxText.T("Рандом → сохранять в профиль", "Random → save to profile"), OnyxConfig.RandomOutfitSave);
    }

    private void DrawVisual(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, OnyxText.T("Косметика", "Cosmetics"), 3f * RowH + 6f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Разблокировать косметику", "Unlock cosmetics"), OnyxConfig.FreeCosmetics);
        Toggle(b.x, ref by, b.width, OnyxText.T("Прятать косметику в матче", "Hide cosmetics in match"), OnyxConfig.HideCosmeticsInMatch);
        Toggle(b.x, ref by, b.width, OnyxText.T("Одинаковые цвета (хост)", "Duplicate colors (host)"), OnyxConfig.AllowDuplicateColors);

        DrawSnipe(x, ref y, w);

        float camBody = 3f * RowH
            + (OnyxConfig.VisualFreeCamera.Value ? 50f : 0f)
            + (OnyxConfig.VisualCameraZoom.Value ? RowH : 0f);
        b = Card(x, ref y, w, OnyxText.T("Камера", "Camera"), camBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Свободная камера (WASD)", "Free camera (WASD)"), OnyxConfig.VisualFreeCamera);
        if (OnyxConfig.VisualFreeCamera.Value)
            SliderInt(b.x, ref by, b.width, OnyxText.T("Скорость камеры", "Camera speed"), OnyxConfig.VisualFreeCameraSpeed, 4, 30);
        Toggle(b.x, ref by, b.width, OnyxText.T("Зум колёсиком (безлимит)", "Wheel zoom (unlimited)"), OnyxConfig.VisualCameraZoom);
        if (OnyxConfig.VisualCameraZoom.Value)
            Toggle(b.x, ref by, b.width, OnyxText.T("Держать зум на тасках", "Keep zoom during tasks"), OnyxConfig.ZoomDuringTasks);
        Toggle(b.x, ref by, b.width, OnyxText.T("Ноклип", "No-clip"), OnyxConfig.VisualNoClip);

        float espRows = OnyxConfig.EspBoxes.Value ? 6f : 4f;
        b = Card(x, ref y, w, "ESP", espRows * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Боксы сквозь стены", "Boxes through walls"), OnyxConfig.EspBoxes);
        if (OnyxConfig.EspBoxes.Value)
        {
            Toggle(b.x, ref by, b.width, OnyxText.T("Ник и дистанция", "Name and distance"), OnyxConfig.EspNames);
            Toggle(b.x, ref by, b.width, OnyxText.T("Показывать мёртвых", "Show dead players"), OnyxConfig.EspDead);
        }
        Toggle(b.x, ref by, b.width, OnyxText.T("Трейсеры до игроков", "Tracers to players"), OnyxConfig.Tracers);
        Toggle(b.x, ref by, b.width, OnyxText.T("Трейсеры к телам", "Tracers to bodies"), OnyxConfig.TracerBodies);
        Toggle(b.x, ref by, b.width, OnyxText.T("КД килла над убийцами", "Kill CD over killers"), OnyxConfig.KillTimers);

        b = Card(x, ref y, w, OnyxText.T("Обзор", "Vision"), 3f * RowH + 22f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Видеть игроков в вентах", "See players in vents"), OnyxConfig.SeeVents);
        Toggle(b.x, ref by, b.width, OnyxText.T("Видеть призраков", "See ghosts"), OnyxConfig.SeeGhosts);
        Toggle(b.x, ref by, b.width, OnyxText.T("Wallhack (без тьмы)", "Wallhack (no darkness)"), OnyxConfig.Wallhack);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), OnyxText.T("Убирает завесу обзора — видно всю карту и всех.", "Removes the vision veil — see the whole map and everyone."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Радар (миникарта)", "Radar (minimap)"), 3f * RowH + 130f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Показывать радар", "Show radar"), OnyxConfig.Radar);
        Toggle(b.x, ref by, b.width, OnyxText.T("Тела на радаре", "Bodies on radar"), OnyxConfig.RadarBodies);
        Toggle(b.x, ref by, b.width, OnyxText.T("Телепорт по ПКМ на радаре", "Right-click radar to teleport"), OnyxConfig.RadarTeleport);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Размер, %", "Size, %"), OnyxConfig.RadarSize, 60, 180);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Прозрачность, %", "Opacity, %"), OnyxConfig.RadarOpacity, 30, 100);
        by += 4f;
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 20f), OnyxText.T("Двигать — ЛКМ по радару. Скелет карты + точки игроков.", "Drag with LMB. Map skeleton + player dots."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Стелс", "Stealth"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Скрыть MOD-штамп", "Hide MOD stamp"), OnyxConfig.HideModStamp);

        b = Card(x, ref y, w, OnyxText.T("Пропуск анимаций", "Skip animations"), 2f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Интро «Shhh»", "'Shhh' intro"), OnyxConfig.SkipShhh);
        Toggle(b.x, ref by, b.width, OnyxText.T("Анимация килла", "Kill animation"), OnyxConfig.SkipKillAnim);

        b = Card(x, ref y, w, OnyxText.T("Избранные образы", "Favorite outfits"), 4f * 30f + 30f);
        by = b.y;
        for (int i = 0; i < 4; i++) FavoriteRow(b.x, ref by, b.width, i);
        if (SmallButton(new Rect(b.x, by, b.width, 24f), OnyxText.T("ОБРАЗ ВЫБРАННОГО → МНЕ", "TARGET'S OUTFIT → ME"), OnyxStyle.Current.Accent))
        {
            string cap = OnyxOutfits.Capture(OnyxMouseTools.Selected);
            OnyxToast.Push(OnyxText.T("Образ", "Outfit"), cap.Length > 0 && OnyxOutfits.Apply(PlayerControl.LocalPlayer, cap) ? OnyxText.T("скопирован", "copied") : OnyxText.T("нет цели", "no target"), 2f, OnyxNotifyKind.Info);
        }

        b = Card(x, ref y, w, OnyxText.T("Режим тела", "Body mode"), RowH);
        by = b.y;
        string[] bmVals = { "Disabled", "Horse", "Seeker", "Long", "LongHorse" };
        string[] bmDisp = { OnyxText.T("Выкл", "Off"), OnyxText.T("Лошадь", "Horse"), OnyxText.T("Сикер", "Seeker"), OnyxText.T("Длинный", "Long"), OnyxText.T("Длинная лошадь", "Long horse") };
        int bi = Mathf.Max(0, Array.IndexOf(bmVals, OnyxConfig.BodyMode.Value));
        CycleRow(b.x, ref by, b.width, OnyxText.T("Стиль тела", "Body style"), bmDisp[bi], () => { OnyxConfig.BodyMode.Value = bmVals[(bi + 1) % bmVals.Length]; });

        b = Card(x, ref y, w, OnyxText.T("Цветной ник", "Colored name"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Включить цветной ник", "Enable colored name"), OnyxConfig.NameColor);
        int nci = OnyxNameColor.Clamp(OnyxConfig.NameColorStyle.Value);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Стиль", "Style"), OnyxNameColor.StyleName(nci), () => { OnyxConfig.NameColorStyle.Value = OnyxNameColor.Next(nci); });
        Toggle(b.x, ref by, b.width, OnyxText.T("Анимация", "Animation"), OnyxConfig.NameColorAnimated);
    }

    private void FavoriteRow(float x, ref float y, float w, int i)
    {
        ConfigEntry<string> slot = OnyxConfig.FavoriteOutfits != null && i < OnyxConfig.FavoriteOutfits.Length ? OnyxConfig.FavoriteOutfits[i] : null;
        string data = slot != null ? slot.Value : "";
        bool has = !string.IsNullOrWhiteSpace(data);
        var r = new Rect(x, y, w, 26f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 4f, r.y, 52f, 26f), OnyxText.T($"Слот {i + 1}", $"Slot {i + 1}"), _rowLabel);
        GUI.Label(new Rect(r.x + 58f, r.y, 88f, 26f), has ? OnyxOutfits.Summary(data) : OnyxText.T("пусто", "empty"), _muted);

        var apply = new Rect(r.xMax - 208f, r.y + 1f, 66f, 24f);
        var mine = new Rect(r.xMax - 138f, r.y + 1f, 48f, 24f);
        var sel = new Rect(r.xMax - 86f, r.y + 1f, 52f, 24f);
        var clr = new Rect(r.xMax - 30f, r.y + 1f, 26f, 24f);
        if (SmallButton(apply, OnyxText.T("НАДЕТЬ", "APPLY"), has ? OnyxStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f)) && has)
            OnyxToast.Push(OnyxText.T("Образ", "Outfit"), OnyxOutfits.Apply(PlayerControl.LocalPlayer, data) ? OnyxText.T("надет", "applied") : OnyxText.T("не готов", "not ready"), 2f, OnyxNotifyKind.Info);
        if (SmallButton(mine, OnyxText.T("МОЙ", "MINE"), new Color(0.5f, 0.55f, 0.62f)) && slot != null)
        { string cap = OnyxOutfits.Capture(PlayerControl.LocalPlayer); if (cap.Length > 0) slot.Value = cap; }
        if (SmallButton(sel, OnyxText.T("ВЫБР.", "SEL."), new Color(0.5f, 0.55f, 0.62f)) && slot != null)
        { string cap = OnyxOutfits.Capture(OnyxMouseTools.Selected); if (cap.Length > 0) slot.Value = cap; }
        if (SmallButton(clr, "✕", new Color(0.9f, 0.4f, 0.4f)) && slot != null) slot.Value = "";
        y += 30f;
    }

    private void DrawSettings(float x, ref float y, float w)
    {
        float gw = w - 56f;
        Rect b = Card(x, ref y, w, OnyxText.T("Интерфейс", "Interface"), RowH + 30f + ThemeGridHeight(gw));
        float by = b.y;
        CycleRow(b.x, ref by, b.width, OnyxText.T("Язык", "Language"), OnyxText.LangName, () => OnyxText.Toggle());
        GUI.Label(new Rect(b.x + 12f, by, b.width - 24f, 20f), OnyxText.T("Тема (акцент)", "Theme (accent)"), _muted);
        by += 24f;
        ThemeSwatches(b.x + 10f, by, b.width - 20f);

        b = Card(x, ref y, w, OnyxText.T("Горячие клавиши", "Hotkeys"), 8f * RowH + 24f);
        by = b.y;
        KeyRow(b.x, ref by, b.width, OnyxText.T("Меню", "Menu"), OnyxConfig.MenuKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Код лобби", "Lobby code"), OnyxConfig.CopyCodeKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Завершить матч", "End match"), OnyxConfig.EndMatchKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Досчитать голоса", "Tally votes"), OnyxConfig.CloseVotingKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Закрыть собрание", "Close meeting"), OnyxConfig.CloseMeetingKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Радиал (удержание)", "Radial (hold)"), OnyxConfig.RadialKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Консоль событий", "Event console"), OnyxConfig.EventConsoleKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Окно чата", "Chat window"), OnyxConfig.ChatWindowKey);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Клик — назначить, ПКМ — сброс, Esc — отмена.", "Click to set, right-click to clear, Esc to cancel."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Клавиши читов", "Cheat keys"), 11f * RowH);
        by = b.y;
        KeyRow(b.x, ref by, b.width, OnyxText.T("God Mode", "God Mode"), OnyxConfig.GodModeKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Мираж", "Mirage"), OnyxConfig.MirageKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Невидимость", "Invisibility"), OnyxConfig.InvisibleKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Ноклип", "No-clip"), OnyxConfig.NoClipKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Зум", "Zoom"), OnyxConfig.ZoomKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Авто-войткик", "Auto votekick"), OnyxConfig.VotekickKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Войткик всех", "Votekick everyone"), OnyxConfig.VotekickAllKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Войткик хоста", "Votekick host"), OnyxConfig.VotekickHostKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Перезайти в игру", "Rejoin last game"), OnyxConfig.RejoinLastKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Саботаж всего", "Sabotage all"), OnyxConfig.SabotageKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Закрыть двери", "Close doors"), OnyxConfig.DoorsKey);

        b = Card(x, ref y, w, OnyxText.T("Призраки и лобби", "Ghosts & lobby"), 4f * RowH);
        by = b.y;
        KeyRow(b.x, ref by, b.width, OnyxText.T("Призрак после старта", "Ghost after start"), OnyxConfig.GhostKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Видеть призраков", "See ghosts"), OnyxConfig.SeeGhostsKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Спавн лобби (хост)", "Spawn lobby (host)"), OnyxConfig.SpawnLobbyKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Деспавн лобби (хост)", "Despawn lobby (host)"), OnyxConfig.DespawnLobbyKey);

        b = Card(x, ref y, w, OnyxText.T("Фан (хост)", "Fun (host)"), 9f * RowH);
        by = b.y;
        KeyRow(b.x, ref by, b.width, OnyxText.T("Все в яйца", "All to eggs"), OnyxConfig.FunEggKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Морф в выбранного", "Morph to target"), OnyxConfig.FunMorphKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Радуга", "Rainbow"), OnyxConfig.FunRainbowKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Цикл косметики", "Cosmetic cycle"), OnyxConfig.FunSkinCycleKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Такт", "Beat"), OnyxConfig.FunBeatKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Размер", "Size"), OnyxConfig.FunSizeKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Движение", "Motion"), OnyxConfig.FunMotionKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Анимация", "Animation"), OnyxConfig.FunAnimKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Сброс облика", "Reset look"), OnyxConfig.FunResetKey);

        b = Card(x, ref y, w, OnyxText.T("Клавиши плеера", "Player keys"), 7f * RowH);
        by = b.y;
        KeyRow(b.x, ref by, b.width, OnyxText.T("Открыть плеер", "Open player"), OnyxConfig.MusicToggleKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Предыдущий трек", "Previous track"), OnyxConfig.MusicPrevKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Следующий трек", "Next track"), OnyxConfig.MusicNextKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Играть / Пауза", "Play / Pause"), OnyxConfig.MusicPlayPauseKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Стоп", "Stop"), OnyxConfig.MusicStopKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Громкость +", "Volume up"), OnyxConfig.MusicVolumeUpKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Громкость -", "Volume down"), OnyxConfig.MusicVolumeDownKey);

        b = Card(x, ref y, w, OnyxText.T("Аккаунт / штрафы", "Account / penalties"), 3f * RowH + 6f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Сбросить штраф за выход", "Clear disconnect penalty"), OnyxConfig.ClearDisconnectPenalty);
        Toggle(b.x, ref by, b.width, OnyxText.T("Гостю доп. функции", "Guest extra features"), OnyxConfig.RemoveGuestLimits);
        Toggle(b.x, ref by, b.width, OnyxText.T("Игнор. ограничения", "Ignore restrictions"), OnyxConfig.RemoveMinorLimits);

        b = Card(x, ref y, w, OnyxText.T("Приватность", "Privacy"), RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Блок телеметрии / данных", "Block telemetry / data"), OnyxConfig.BlockTelemetry);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Применяется при запуске игры.", "Applied on game start."), _muted);
    }

    private void DrawEmpty(Rect area, OnyxIcon icon, string msg)
    {
        OnyxPalette p = OnyxStyle.Current;
        float s = 66f;
        var ic = new Rect(area.center.x - s / 2f, area.y + area.height * 0.30f, s, s);
        OnyxStyle.FillRounded(new Rect(ic.x - 12f, ic.y - 12f, s + 24f, s + 24f), A(p.Accent, 0.06f), 22);
        OnyxIcons.Draw(icon, ic, A(p.Muted, 0.55f));
        GUI.Label(new Rect(area.x, ic.yMax + 16f, area.width, 24f), msg, _centerMuted);
    }

    private void ActionRow(Rect body, OnyxIcon icon, string label, string btn, Action onClick)
    {
        OnyxPalette p = OnyxStyle.Current;
        var row = new Rect(body.x, body.y, body.width, 44f);
        var sq = new Rect(row.x, row.y + 2f, 40f, 40f);
        OnyxStyle.FillRounded(sq, A(p.Accent, 0.14f), 10);
        OnyxIcons.Draw(icon, new Rect(sq.x + 10f, sq.y + 10f, 20f, 20f), p.Accent);
        GUI.Label(new Rect(sq.xMax + 14f, row.y, row.width - 170f, 44f), label, _rowLabel);

        var bt = new Rect(row.xMax - 112f, row.y + 5f, 112f, 34f);
        bool hover = bt.Contains(M);
        OnyxStyle.FillRounded(bt, p.Accent, 9);
        OnyxStyle.FillRounded(new Rect(bt.x + 1.5f, bt.y + 1.5f, bt.width - 3f, bt.height - 3f), hover ? A(p.Accent, 0.30f) : p.Panel, 8);
        OnyxStyle.Fill(new Rect(bt.x + 8f, bt.y + 4f, bt.width - 16f, 1f), A(Color.white, 0.10f));
        GUI.Label(bt, btn, _btnLabel);
        if (GUI.Button(bt, GUIContent.none, _invisible)) onClick();
    }

    private void InfoRow(float x, ref float y, float w, string label, string value)
    {
        GUI.Label(new Rect(x + 2f, y, w * 0.5f, 28f), label, _muted);
        GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f - 2f, 28f), value, _value);
        y += 30f;
    }

    private void Toggle(float x, ref float y, float w, string label, ConfigEntry<bool> entry)
    {
        if (entry == null) { y += RowH; return; }
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        if (GUI.Button(r, GUIContent.none, _invisible))
            entry.Value = !entry.Value;

        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _rowLabel);
        DrawPill(new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f), entry.Value, entry);
        y += RowH;
    }

    private void Slider(float x, ref float y, float w, string label, ConfigEntry<float> entry, float min, float max, string fmt)
    {
        if (entry == null) { y += RowH; return; }
        OnyxPalette p = OnyxStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value.ToString(fmt), _value);
        y += 26f;

        var track = new Rect(x + 10f, y + 2f, w - 20f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x - 8f, track.y - 10f, track.width + 16f, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        float kx = track.x + track.width * t;
        OnyxStyle.FillRounded(track, p.Button, 4);
        OnyxStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        OnyxStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        OnyxStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
    }

    private void SliderInt(float x, ref float y, float w, string label, ConfigEntry<int> entry, int min, int max)
    {
        if (entry == null) { y += RowH; return; }
        OnyxPalette p = OnyxStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) entry.Value = Mathf.Clamp(entry.Value - 1, min, max);
        if (ArrowButton(rightBtn, "▸")) entry.Value = Mathf.Clamp(entry.Value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        float kx = track.x + track.width * t;
        OnyxStyle.FillRounded(track, p.Button, 4);
        OnyxStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        OnyxStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        OnyxStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
    }

    private int SliderIntVal(float x, ref float y, float w, string label, int value, int min, int max, string suffix, int key = 0)
    {
        OnyxPalette p = OnyxStyle.Current;
        value = Mathf.Clamp(value, min, max);
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), value + suffix, _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) value = Mathf.Clamp(value - 1, min, max);
        if (ArrowButton(rightBtn, "▸")) value = Mathf.Clamp(value + 1, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = key != 0 ? key : label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width))), min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, value);
        float kx = track.x + track.width * t;
        OnyxStyle.FillRounded(track, p.Button, 4);
        OnyxStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        OnyxStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        OnyxStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
        return value;
    }

    private float SliderFloatVal(float x, ref float y, float w, string label, float value, float min, float max, float step, string fmt, string suffix)
    {
        OnyxPalette p = OnyxStyle.Current;
        value = Mathf.Clamp(value, min, max);
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), value.ToString(fmt) + suffix, _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) value = Mathf.Clamp(value - step, min, max);
        if (ArrowButton(rightBtn, "▸")) value = Mathf.Clamp(value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                float raw = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                value = Mathf.Clamp(Mathf.Round(raw / step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, value);
        float kx = track.x + track.width * t;
        OnyxStyle.FillRounded(track, p.Button, 4);
        OnyxStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        OnyxStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        OnyxStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
        return value;
    }

    private bool ToggleVal(float x, ref float y, float w, string label, bool value)
    {
        OnyxPalette p = OnyxStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        bool clicked = GUI.Button(r, GUIContent.none, _invisible);
        GUI.Label(new Rect(r.x + 10f, r.y, r.width - 74f, r.height), label, _rowLabel);
        var pill = new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f);
        OnyxStyle.FillRounded(pill, value ? A(p.Accent, 0.9f) : A(Color.white, 0.10f), 12);
        float kx = value ? pill.xMax - 22f : pill.x + 2f;
        OnyxStyle.FillRounded(new Rect(kx, pill.y + 2f, 20f, 20f), Color.white, 10);
        y += RowH;
        return clicked ? !value : value;
    }

    private void FpsSlider(float x, ref float y, float w, string label, ConfigEntry<int> entry)
    {
        if (entry == null) { y += RowH; return; }
        const int min = 30, max = 300, step = 5;
        OnyxPalette p = OnyxStyle.Current;
        GUI.Label(new Rect(x + 10f, y, w - 74f, 22f), label, _rowLabel);
        GUI.Label(new Rect(x + w - 58f, y, 54f, 22f), entry.Value >= max ? "∞" : entry.Value.ToString(), _value);
        y += 26f;

        const float arrowW = 24f;
        var leftBtn = new Rect(x + 8f, y - 3f, arrowW, 20f);
        var rightBtn = new Rect(x + w - arrowW - 8f, y - 3f, arrowW, 20f);
        if (ArrowButton(leftBtn, "◂")) entry.Value = Mathf.Clamp(entry.Value - step, min, max);
        if (ArrowButton(rightBtn, "▸")) entry.Value = Mathf.Clamp(entry.Value + step, min, max);

        var track = new Rect(leftBtn.xMax + 8f, y + 2f, rightBtn.x - leftBtn.xMax - 16f, 8f);
        int id = label.GetHashCode();
        Event e = Event.current;
        var hit = new Rect(track.x, track.y - 10f, track.width, 28f);
        if (e != null && e.type == EventType.MouseDown && hit.Contains(e.mousePosition)) { _slider = id; e.Use(); }
        if (_slider == id && e != null)
        {
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
            {
                int raw = Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width)));
                entry.Value = Mathf.Clamp(Mathf.RoundToInt(raw / (float)step) * step, min, max);
                e.Use();
            }
            else if (e.type == EventType.MouseUp) { _slider = 0; e.Use(); }
        }

        float t = Mathf.InverseLerp(min, max, entry.Value);
        float kx = track.x + track.width * t;
        OnyxStyle.FillRounded(track, p.Button, 4);
        OnyxStyle.FillRounded(new Rect(track.x, track.y, Mathf.Max(8f, track.width * t), track.height), p.Accent, 4);
        OnyxStyle.FillRounded(new Rect(kx - 9f, track.y + track.height / 2f - 9f, 18f, 18f), A(p.Accent, 0.35f), 9);
        OnyxStyle.FillRounded(new Rect(kx - 6f, track.y + track.height / 2f - 6f, 12f, 12f), Color.white, 6);
        y += 24f;
    }

    private bool ArrowButton(Rect r, string glyph)
    {
        OnyxPalette p = OnyxStyle.Current;
        bool hover = r.Contains(M);
        OnyxStyle.FillRounded(r, hover ? A(p.Accent, 0.22f) : A(Color.white, 0.05f), 6);
        GUI.Label(r, glyph, _arrow);
        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private const float SwW = 28f, SwH = 22f, SwGap = 6f;

    private static int ThemeCols(float w) => Mathf.Max(1, Mathf.FloorToInt((w + SwGap) / (SwW + SwGap)));

    private static float ThemeGridHeight(float w) =>
        Mathf.CeilToInt((float)OnyxStyle.ThemeCount / ThemeCols(w)) * (SwH + SwGap) - SwGap;

    private void ThemeSwatches(float x, float y, float w)
    {
        int n = OnyxStyle.ThemeCount;
        int cols = ThemeCols(w);
        int cur = OnyxConfig.ThemeIndex.Value;
        for (int i = 0; i < n; i++)
        {
            var r = new Rect(x + (i % cols) * (SwW + SwGap), y + (i / cols) * (SwH + SwGap), SwW, SwH);
            OnyxStyle.FillRounded(r, OnyxStyle.ThemeAt(i).Accent, 6);
            OnyxStyle.StrokeRounded(r, i == cur ? Color.white : A(Color.black, 0.30f), 6, i == cur ? 2 : 1);
            if (GUI.Button(r, GUIContent.none, _invisible)) OnyxConfig.ThemeIndex.Value = i;
        }
    }

    private void DrawSearchBar(Rect r)
    {
        string prev = _search;
        _search = CustomText(r, _search ?? "", "onyxSearch");
        if (_search != prev) _scroll = 0f;
        if (string.IsNullOrEmpty(_search) && _textFocus != "onyxSearch")
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), OnyxText.T("Поиск фич…", "Search features…"), _muted);
        if (!string.IsNullOrEmpty(_search))
        {
            var clr = new Rect(r.xMax - 24f, r.y, 22f, r.height);
            if (GUI.Button(clr, GUIContent.none, _invisible)) { _search = ""; _textFocus = null; }
            GUI.Label(clr, "×", _star);
        }
    }

    private void DrawSearch(float x, ref float y, float w)
    {
        if (_search != _searchDone)
        {
            _searchDone = _search;
            string q = _search.Trim().ToLowerInvariant();
            _searchHits.Clear();
            foreach (QuickItem it in OnyxQuick.Items)
                if (it.Ru.ToLowerInvariant().Contains(q) || it.En.ToLowerInvariant().Contains(q))
                    _searchHits.Add(it);
        }

        int n = _searchHits.Count;
        Rect b = Card(x, ref y, w, OnyxText.T("Поиск", "Search") + "  (" + n + ")", n > 0 ? n * RowH + 6f : 30f);
        float by = b.y;
        if (n == 0)
        {
            GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Ничего не найдено.", "Nothing found."), _muted);
            return;
        }
        for (int i = 0; i < n; i++) QuickRow(b.x, ref by, b.width, _searchHits[i]);
    }

    private void QuickRow(float x, ref float y, float w, QuickItem it)
    {
        var r = new Rect(x, y, w, RowH - 2f);
        HoverFill(r);
        bool fav = OnyxQuick.IsFav(it.Id);
        var star = new Rect(r.x + 6f, r.y, 26f, r.height);
        if (GUI.Button(star, GUIContent.none, _invisible)) OnyxQuick.ToggleFav(it.Id);
        _star.normal.textColor = fav ? OnyxStyle.Current.Accent : A(OnyxStyle.Current.Muted, 0.7f);
        GUI.Label(star, fav ? "★" : "☆", _star);
        GUI.Label(new Rect(r.x + 36f, r.y, r.width - 92f, r.height), it.Label, _rowLabel);
        var pill = new Rect(r.xMax - 50f, r.y + (r.height - 24f) / 2f, 46f, 24f);
        if (GUI.Button(pill, GUIContent.none, _invisible)) it.Cfg.Value = !it.Cfg.Value;
        DrawPill(pill, it.Cfg.Value, it.Cfg);
        y += RowH;
    }

    private void CycleRow(float x, ref float y, float w, string label, string valueText, Action onClick)
    {
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        if (GUI.Button(r, GUIContent.none, _invisible)) onClick();
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.45f, r.height), label, _rowLabel);
        GUI.Label(new Rect(r.x + r.width - 168f, r.y, 164f, r.height), valueText + "   ▸", _value);
        y += RowH;
    }

    private void KeyRow(float x, ref float y, float w, string label, ConfigEntry<KeyCode> entry)
    {
        if (entry == null) { y += RowH; return; }
        OnyxPalette p = OnyxStyle.Current;
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.5f, r.height), label, _rowLabel);

        bool listening = _rebinding && ReferenceEquals(_rebindTarget, entry);
        float bw = listening ? 132f : 96f;
        var badge = new Rect(r.xMax - bw - 4f, r.y + (r.height - 24f) / 2f, bw, 24f);
        OnyxStyle.FillRounded(badge, listening ? A(p.Accent, 0.85f) : A(Color.white, 0.06f), 7);
        OnyxStyle.StrokeRounded(badge, listening ? A(p.Accent, 0.9f) : A(Color.white, 0.10f), 7, 1);
        _keyBadge.normal.textColor = listening ? Color.white : p.Text;
        string txt = listening ? OnyxText.T("Нажми клавишу…", "Press a key…") : (entry.Value == KeyCode.None ? "—" : entry.Value.ToString());
        GUI.Label(badge, txt, _keyBadge);

        Event e = Event.current;
        bool rightClear = e != null && e.type == EventType.MouseDown && e.button == 1 && badge.Contains(e.mousePosition);
        bool clicked = GUI.Button(badge, GUIContent.none, _invisible);
        if (rightClear)
        {
            entry.Value = KeyCode.None;
            _rebinding = false; _rebindTarget = null; Rebinding = false;
            e.Use();
        }
        else if (clicked)
        {
            if (listening) { _rebinding = false; _rebindTarget = null; }
            else { _rebinding = true; _rebindTarget = entry; }
            Rebinding = _rebinding;
        }
        y += RowH;
    }

    private void HandleRebindCapture()
    {
        Rebinding = _rebinding;
        if (!_rebinding || _rebindTarget == null) return;
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        KeyCode kc = e.keyCode;
        if (kc == KeyCode.Escape) { _rebinding = false; _rebindTarget = null; }
        else if (kc != KeyCode.None) { _rebindTarget.Value = kc; _rebinding = false; _rebindTarget = null; }
        Rebinding = _rebinding;
        e.Use();
    }

    private void DrawPill(Rect track, bool on, object key)
    {
        OnyxPalette p = OnyxStyle.Current;
        float target = on ? 1f : 0f;
        float a = target, v = 0f;
        if (key != null)
        {
            if (!_pillAnim.TryGetValue(key, out a)) a = target;
            _pillVel.TryGetValue(key, out v);
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                v += (-300f * (a - target) - 24f * v) * dt;
                a += v * dt;
                _pillAnim[key] = a;
                _pillVel[key] = v;
            }
        }
        float e = Mathf.Clamp01(a);
        float kpos = Mathf.Clamp(a, -0.12f, 1.12f);
        int r = Mathf.RoundToInt(track.height / 2f);

        if (e > 0.01f)
        {
            OnyxStyle.FillRounded(new Rect(track.x - 5f, track.y - 5f, track.width + 10f, track.height + 10f), A(p.Accent, 0.12f * e), r + 5);
            OnyxStyle.FillRounded(new Rect(track.x - 2f, track.y - 2f, track.width + 4f, track.height + 4f), A(p.Accent, 0.28f * e), r + 2);
        }
        OnyxStyle.FillRounded(track, Color.Lerp(p.Button, p.Accent, e), r);
        if (e < 0.99f)
            OnyxStyle.StrokeRounded(track, A(Color.white, 0.06f * (1f - e)), r, 1);
        if (e > 0.01f)
        {
            OnyxStyle.Fill(new Rect(track.x + 5f, track.y + 2f, track.width - 10f, 1f), A(Color.white, 0.30f * e));
            OnyxStyle.Fill(new Rect(track.x + 5f, track.yMax - 3f, track.width - 10f, 1f), A(Color.black, 0.16f * e));
        }

        float knob = track.height - 6f;
        float kx = Mathf.Lerp(track.x + 3f, track.xMax - knob - 3f, kpos);
        int kr = Mathf.RoundToInt(knob / 2f);
        OnyxStyle.FillRounded(new Rect(kx, track.y + 5f, knob, knob), A(Color.black, 0.34f), kr);
        OnyxStyle.FillRounded(new Rect(kx, track.y + 3f, knob, knob), Color.Lerp(p.Muted, Color.white, e), kr);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);

    private void DrawStars(float w, float h)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        float t = Time.unscaledTime;
        for (int i = 0; i < 26; i++)
        {
            float fx = Frac(i * 0.61803399f + 0.11f);
            float fy = Frac(i * 0.75487767f + 0.29f);
            float drift = Frac(fy + 0.02f * t * (0.4f + fx));
            float sz = 1.3f + 1.7f * fy;
            float tw = 0.5f + 0.5f * Mathf.Sin(t * (0.7f + fx) + i);
            Color col = (i % 5 == 0) ? OnyxStyle.Current.Accent : Color.white;
            OnyxStyle.FillRounded(new Rect(fx * w, drift * h, sz, sz), A(col, 0.05f + 0.06f * tw), 1);
        }
    }

    private void HoverFill(Rect r)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        if (!r.Contains(Event.current.mousePosition)) return;
        OnyxPalette p = OnyxStyle.Current;
        OnyxStyle.FillRounded(r, A(p.Accent, 0.06f), 8);
        OnyxStyle.FillRounded(new Rect(r.x, r.y, r.width * 0.42f, r.height), A(p.Accent, 0.05f), 8);
        OnyxStyle.FillRounded(new Rect(r.x + 1f, r.y + 4f, 3f, r.height - 8f), p.Accent, 2);
    }

    private string CustomText(Rect r, string text, string key)
    {
        OnyxPalette p = OnyxStyle.Current;
        bool focused = _textFocus == key;
        OnyxStyle.FillRounded(r, p.Button, 7);
        OnyxStyle.StrokeRounded(r, focused ? A(p.Accent, 0.8f) : A(Color.white, 0.08f), 7, 1);

        Event e = Event.current;
        if (e != null && e.type == EventType.MouseDown)
            _textFocus = r.Contains(e.mousePosition) ? key : (_textFocus == key ? null : _textFocus);

        if (focused && e != null && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Backspace) { if (text.Length > 0) text = text.Substring(0, text.Length - 1); e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { _textFocus = null; e.Use(); }
            else if (e.character != '\0' && !char.IsControl(e.character) && text.Length < 24) { text += e.character; e.Use(); }
        }

        bool caret = focused && Time.unscaledTime % 1f < 0.5f;
        GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), text + (caret ? "|" : ""), _rowLabel);
        return text;
    }

    private static void OpenLink(string url)
    {
        try { GUIUtility.systemCopyBuffer = url; } catch { }
        try { Application.OpenURL(url); } catch { }
        OnyxToast.Push(OnyxText.T("Ссылка", "Link"), OnyxText.T("Открыта в браузере · скопирована", "Opened in browser · copied"), 2.5f, OnyxNotifyKind.Info);
    }

    private static string KeyName(ConfigEntry<KeyCode> key) => key != null ? key.Value.ToString() : "?";
    private static string KeyDisp(ConfigEntry<KeyCode> key) => key == null || key.Value == KeyCode.None ? "—" : key.Value.ToString();
    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static Rect LerpRect(Rect a, Rect b, float t) =>
        new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }

    private void Build()
    {
        int ti = OnyxConfig.ThemeIndex != null ? OnyxConfig.ThemeIndex.Value : 0;
        if (_built && ti == _themeBuilt) return;
        _built = true;
        _themeBuilt = ti;
        OnyxPalette p = OnyxStyle.Current;

        _windowBg = new GUIStyle();
        _invisible = new GUIStyle();
        _gradient = OnyxStyle.BuildGradient((int)_window.width, (int)FullH, Rgb(30, 30, 32), Rgb(14, 14, 15), 16);
        _gloss = OnyxStyle.BuildVFade(96, Color.white, 0.05f, 0f);

        _brand = Label(p.Text, 25, FontStyle.Bold, TextAnchor.MiddleLeft);
        _verPill = Label(p.Accent, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        _cardTitle = Label(A(p.Text, 0.82f), 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _rowLabel = Label(p.Text, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabel.wordWrap = true;
        _value = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleRight);
        _valueC = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _muted = Label(p.Muted, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
        _footer = Label(p.Muted, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
        _btnLabel = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
        _centerMuted = Label(A(p.Muted, 0.8f), 14, FontStyle.Normal, TextAnchor.MiddleCenter);
        _arrow = Label(p.Accent, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        _smallBtn = Label(Color.white, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
        _rowName = Label(p.Text, 14, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowName.wordWrap = false;
        _rowName.clipping = TextClipping.Clip;
        _tabStyle = Label(p.Muted, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
        _keyBadge = Label(p.Text, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        _star = Label(p.Accent, 17, FontStyle.Normal, TextAnchor.MiddleCenter);
    }

    private static GUIStyle Label(Color color, int size, FontStyle style, TextAnchor anchor)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = style,
            alignment = anchor,
            richText = true
        };
        s.normal.textColor = color;
        s.padding = OnyxStyle.Offset(0, 0, 0, 0);
        return s;
    }

    private static Color Rgb(byte r, byte g, byte b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
}
