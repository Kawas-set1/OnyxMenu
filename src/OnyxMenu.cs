using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using InnerNet;
using UnityEngine;

namespace Onyx;

public sealed class OnyxMenu : MonoBehaviour
{
    private const int WindowId = 770731;
    private const float FullH = 560f;
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
        new TabDef(OnyxIcon.People, "Лобби", "Lobby"),
        new TabDef(OnyxIcon.Eye, "Визуальные", "Visual"),
        new TabDef(OnyxIcon.Person, "Игроки", "Players"),
        new TabDef(OnyxIcon.Shield, "Защита", "Guard"),
        new TabDef(OnyxIcon.Wifi, "Сеть", "Network"),
    };

    internal static bool Opened;

    private bool _open;
    private bool _collapsed;
    private int _tab;
    private int _slider;
    private float _scroll;
    private float _h = FullH;
    private int _resize;
    private Rect _window = new Rect(300f, 110f, 720f, FullH);

    private bool _built;
    private Texture2D _gradient;
    private GUIStyle _windowBg;
    private GUIStyle _brand;
    private GUIStyle _verPill;
    private GUIStyle _tabStyle;
    private GUIStyle _cardTitle;
    private GUIStyle _rowLabel;
    private GUIStyle _value;
    private GUIStyle _muted;
    private GUIStyle _footer;
    private GUIStyle _btnLabel;
    private GUIStyle _centerMuted;
    private GUIStyle _arrow;
    private GUIStyle _smallBtn;
    private GUIStyle _rowName;
    private GUIStyle _invisible;
    private GUIStyle _keyBadge;

    private readonly List<ClientData> _guardClients = new List<ClientData>();
    private string _cloneText = "";
    private string _nickText = "";
    private string _textFocus;
    private bool _wasTyping;

    private bool _rebinding;
    private ConfigEntry<KeyCode> _rebindTarget;
    internal static bool Rebinding;

    private float _openAt = -1f;
    private float _closeAt = -1f;
    private float _fade = 1f;

    private int _tabDir = 1;
    private float _tabAnimAt = -1f;
    private Rect _tabHi;
    private bool _tabHiInit;
    private readonly Dictionary<object, float> _pillAnim = new Dictionary<object, float>();

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

    private static void SetMoveable(bool value)
    {
        try { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.moveable = value; }
        catch { }
    }

    public void OnGUI()
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

        OnyxStyle.Fill(new Rect(0f, 0f, vw, vh), new Color(0.02f, 0.03f, 0.06f, 0.34f * fade));

        ClampWindow(vw, vh);
        if (fade >= 1f) HandleResize(vw, vh);

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fade);
        DrawShadow(_window);
        _window = GUI.Window(WindowId, _window, (GUI.WindowFunction)DrawWindow, string.Empty, _windowBg);
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
        OnyxStyle.Fill(new Rect(16f, 1f, w - 32f, 1f), A(Color.white, 0.07f));

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
            SetTab(Mathf.Clamp(_tab + (Event.current.delta.y > 0f ? 1 : -1), 0, 7));
            Event.current.Use();
        }

        var settingsRect = new Rect(14f, h - FooterH - TabH - 8f, SidebarW - 26f, TabH);
        Rect activeRect = _tab == 7 ? settingsRect : new Rect(14f, HeaderH + 14f + _tab * (TabH + 4f), SidebarW - 26f, TabH);
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
        DrawTab(settingsRect, OnyxIcon.Gear, OnyxText.T("Настройки", "Settings"), 7);

        var area = new Rect(SidebarW, HeaderH + 6f, w - SidebarW - 16f, h - HeaderH - 6f - FooterH);
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
        float x = 4f;
        float cx = x + slide;
        float cw = area.width - 20f;
        float startY = 6f - _scroll;
        float cy = startY;
        var localArea = new Rect(slide, 0f, area.width, area.height);
        switch (_tab)
        {
            case 0: DrawHome(cx, ref cy, cw); break;
            case 1: DrawQoL(cx, ref cy, cw); break;
            case 2: DrawLobbyTab(cx, ref cy, cw); break;
            case 3: DrawVisual(cx, ref cy, cw); break;
            case 4: DrawPlayers(cx, ref cy, cw); break;
            case 5: DrawGuard(cx, ref cy, cw); break;
            case 6: DrawNet(cx, ref cy, cw); break;
            case 7: DrawSettings(cx, ref cy, cw); break;
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
        GUI.Label(new Rect(67f, 15f, w - 200f, 34f), "ONYX", _brand);

        var ver = new Rect(w - 168f, 20f, 62f, 24f);
        OnyxStyle.FillRounded(ver, A(p.Accent, 0.14f), 8);
        GUI.Label(ver, "v" + OnyxPlugin.PluginVersion, _verPill);

        var minB = new Rect(w - 96f, 20f, 26f, 24f);
        var closeB = new Rect(w - 62f, 20f, 26f, 24f);
        if (WindowButton(minB, OnyxIcon.Minimize, p)) _collapsed = !_collapsed;
        if (WindowButton(closeB, OnyxIcon.Close, p)) RequestClose();

        OnyxStyle.Fill(new Rect(20f, HeaderH - 1f, w - 40f, 1f), A(Color.white, 0.05f));
        OnyxStyle.Fill(new Rect(w * 0.5f - 80f, HeaderH - 1f, 160f, 1f), A(p.Accent, 0.35f));
    }

    private bool WindowButton(Rect r, OnyxIcon icon, OnyxPalette p)
    {
        bool hover = r.Contains(M);
        if (hover) OnyxStyle.FillRounded(r, A(Color.white, 0.07f), 7);
        OnyxIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), hover ? p.Text : p.Muted);
        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private void DrawLogo(Rect box, OnyxPalette p)
    {
        Vector2 c = box.center;
        OnyxStyle.FillRounded(new Rect(c.x - 20f, c.y - 20f, 40f, 40f), A(p.Accent, 0.16f), 20);

        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, c);
        float s = box.width * 0.66f;
        var sq = new Rect(c.x - s / 2f, c.y - s / 2f, s, s);
        OnyxStyle.FillRounded(sq, p.Accent, 5);
        OnyxStyle.FillRounded(new Rect(sq.x, sq.y, sq.width, sq.height * 0.5f), A(Color.white, 0.18f), 5);
        OnyxStyle.Fill(new Rect(sq.x + 2f, c.y - 0.6f, sq.width - 4f, 1.2f), A(Color.black, 0.20f));
        OnyxStyle.Fill(new Rect(c.x - 0.6f, sq.y + 2f, 1.2f, sq.height - 4f), A(Color.black, 0.20f));
        GUI.matrix = m;

        OnyxStyle.FillRounded(new Rect(box.x + 9f, box.y + 8f, 4f, 4f), A(Color.white, 0.85f), 2);
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
        OnyxStyle.FillRounded(card, p.Panel, 12);
        OnyxStyle.FillRounded(card, A(Color.white, 0.02f), 12);
        OnyxStyle.FillRounded(new Rect(card.x, card.y, card.width, head), A(Color.white, 0.03f), 12);
        OnyxStyle.StrokeRounded(card, A(Color.white, 0.08f), 12, 1);
        OnyxStyle.Fill(new Rect(card.x + 12f, card.y + 1f, card.width - 24f, 1f), A(Color.white, 0.10f));
        OnyxStyle.Fill(new Rect(card.x + 12f, card.yMax - 2f, card.width - 24f, 1f), A(Color.black, 0.20f));

        OnyxStyle.FillRounded(new Rect(card.x + 18f, card.y + head / 2f - 7f, 3f, 14f), p.Accent, 2);
        GUI.Label(new Rect(card.x + 32f, card.y, card.width - 70f, head), title.ToUpperInvariant(), _cardTitle);
        OnyxIcons.Draw(OnyxIcon.Chevron, new Rect(card.xMax - 32f, card.y + head / 2f - 6f, 13f, 13f), p.Muted);
        OnyxStyle.Fill(new Rect(card.x + 18f, card.y + head - 1f, card.width - 36f, 1f), A(Color.white, 0.05f));

        y = card.yMax + 14f;
        return new Rect(card.x + 18f, card.y + head + 8f, card.width - 36f, bodyH);
    }

    private static readonly string[] KickBanVals = { "Kick", "Ban" };
    private static readonly string[] VoteVals = { "Null", "Warn", "Kick", "Ban" };
    private static string[] KickBanDisp => new[] { OnyxText.T("Кик", "Kick"), OnyxText.T("Бан", "Ban") };
    private static string[] VoteDisp => new[] { OnyxText.T("Нулл", "Null"), OnyxText.T("Варн", "Warn"), OnyxText.T("Кик", "Kick"), OnyxText.T("Бан", "Ban") };

    private void DrawGuard(float x, ref float y, float w)
    {
        InnerNetClient net = GuardNet();

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

        b = Card(x, ref y, w, OnyxText.T("Цвета", "Colors"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Кик Fortegreen", "Kick Fortegreen"), OnyxConfig.KickFortegreen);

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
        OnyxStyle.FillRounded(r, hover ? A(col, 0.4f) : A(col, 0.2f), 7);
        OnyxStyle.Fill(new Rect(r.x + 4f, r.y + 2f, r.width - 8f, 1f), A(Color.white, 0.14f));
        _smallBtn.normal.textColor = Color.Lerp(col, Color.white, 0.55f);
        GUI.Label(r, label, _smallBtn);
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
            if (Patches.OnyxJoinLevels.TryGet(c.Id, out uint lvlRaw))
                s = OnyxText.T("ур.", "lvl") + (lvlRaw + 1u);
            else if (c.Character != null && c.Character.Data != null && c.Character.Data.PlayerLevel != uint.MaxValue && c.Character.Data.PlayerLevel <= 9999u)
                s = OnyxText.T("ур.", "lvl") + (c.Character.Data.PlayerLevel + 1u);
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

    private void DrawHome(float x, ref float y, float w)
    {
        string about = OnyxText.T(
            "<b>Onyx</b> — клиент-сайд мод-меню для Among Us.",
            "<b>Onyx</b> — a client-side mod menu for Among Us.");
        float ah = _rowLabel.CalcHeight(new GUIContent(about), w - 36f);
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
        float ih = _rowLabel.CalcHeight(new GUIContent(imp), w - 36f);
        b = Card(x, ref y, w, OnyxText.T("Важно", "Important"), ih);
        GUI.Label(b, imp, _rowLabel);

        b = Card(x, ref y, w, OnyxText.T("Ссылки", "Links"), 2f * RowH);
        by = b.y;
        LinkRow(b.x, ref by, b.width, "GitHub", "");
        LinkRow(b.x, ref by, b.width, "Discord", "https://discord.gg/cP4MrVUfM7");

        b = Card(x, ref y, w, OnyxText.T("Быстрые действия", "Quick actions"), 48f);
        ActionRow(b, OnyxIcon.Bell, OnyxText.T("Тест уведомления", "Test notification"), OnyxText.T("Проверить", "Test"), () => OnyxToast.Push(OnyxText.T("Onyx на связи ✓", "Onyx is live ✓")));
    }

    private void LinkRow(float x, ref float y, float w, string label, string url)
    {
        var r = new Rect(x, y, w, RowH - 4f);
        HoverFill(r);
        GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.7f, r.height), label, _rowLabel);
        GUI.Label(new Rect(r.x + w - 40f, r.y, 34f, r.height), "↗", _value);
        if (!string.IsNullOrEmpty(url) && GUI.Button(r, GUIContent.none, _invisible))
            Application.OpenURL(url);
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

        b = Card(x, ref y, w, OnyxText.T("Чат", "Chat"), 7f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Улучшенный чат", "Better chat"), OnyxConfig.BetterChat);
        Toggle(b.x, ref by, b.width, OnyxText.T("Инфо над пузырями (ур./платформа)", "Info above bubbles (lvl/platform)"), OnyxConfig.ChatBubbleSenderInfo);
        Toggle(b.x, ref by, b.width, OnyxText.T("Тёмный чат под тему", "Dark chat theme"), OnyxConfig.DarkChatTheme);
        Toggle(b.x, ref by, b.width, OnyxText.T("Чат всегда виден", "Chat always visible"), OnyxConfig.VisualAlwaysShowChat);
        Toggle(b.x, ref by, b.width, OnyxText.T("Видеть чат мёртвых", "See dead chat"), OnyxConfig.GhostChat);
        Toggle(b.x, ref by, b.width, OnyxText.T("Без лимита длины", "No length limit"), OnyxConfig.UnlimitedChatLength);
        Toggle(b.x, ref by, b.width, OnyxText.T("Без задержки чата", "No chat cooldown"), OnyxConfig.SkipChatCooldown);

        b = Card(x, ref y, w, OnyxText.T("Лог и фильтр чата", "Chat log & filter"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Лог чата в файл", "Log chat to file"), OnyxConfig.ChatLog);
        Toggle(b.x, ref by, b.width, OnyxText.T("Цензура бан-слов", "Censor banned words"), OnyxConfig.BanWords);
        Toggle(b.x, ref by, b.width, OnyxText.T("Команда /xmas (хост)", "/xmas command (host)"), OnyxConfig.ChatCmdXmas);
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

        b = Card(x, ref y, w, OnyxText.T("Автохост", "Auto-host"), 6f * RowH + 100f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Включить автохост", "Enable auto-host"), OnyxConfig.AutoHostEnabled);
        Toggle(b.x, ref by, b.width, OnyxText.T("Мгновенный старт", "Instant start"), OnyxConfig.AutoHostInstantStart);
        Toggle(b.x, ref by, b.width, OnyxText.T("Возврат в лобби после матча", "Return to lobby after match"), OnyxConfig.AutoHostReturnAfterMatch);
        Toggle(b.x, ref by, b.width, OnyxText.T("Ждать загрузку игроков", "Wait for players to load"), OnyxConfig.AutoHostWaitLoadedPlayers);
        Toggle(b.x, ref by, b.width, OnyxText.T("Форс в последнюю минуту", "Force in last minute"), OnyxConfig.AutoHostForceLastMinute);
        Toggle(b.x, ref by, b.width, OnyxText.T("Уведомления автохоста", "Auto-host notifications"), OnyxConfig.AutoHostNotifications);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Минимум игроков", "Min players"), OnyxConfig.AutoHostMinPlayers, 1, 15);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Задержка старта, с", "Start delay, s"), OnyxConfig.AutoHostStartDelaySeconds, 0, 180);

        b = Card(x, ref y, w, OnyxText.T("Геймплей лобби", "Lobby gameplay"), 6f * RowH + 32f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Без условий победы", "No win conditions"), OnyxConfig.NoWinConditions);
        Toggle(b.x, ref by, b.width, OnyxText.T("2 преда в прятках", "2 seekers in hide & seek"), OnyxConfig.HideAndSeekTwoSeekers);
        Toggle(b.x, ref by, b.width, OnyxText.T("4 импостера (≥9 игроков)", "4 impostors (≥9 players)"), OnyxConfig.FourImpostors);
        Toggle(b.x, ref by, b.width, OnyxText.T("Снять лимиты настроек", "Unlock option limits"), OnyxConfig.LooseHostOptions);
        Toggle(b.x, ref by, b.width, OnyxText.T("Шаг настроек 0.1", "Option step 0.1"), OnyxConfig.ForceMinValues);
        Toggle(b.x, ref by, b.width, OnyxText.T("Копировать код при дисконнекте", "Copy code on disconnect"), OnyxConfig.CopyCodeOnDisconnect);
        if (SmallButton(new Rect(b.x + 2f, by, 138f, 24f), OnyxText.T("РАЗРУШИТЬ ЛОББИ", "DESTROY LOBBY"), new Color(0.9f, 0.4f, 0.4f)))
            OnyxToast.Push(OnyxText.T("Лобби", "Lobby"), OnyxLobbyTools.DestroyLobby(), 2.5f, OnyxNotifyKind.Warning);
        if (SmallButton(new Rect(b.x + 148f, by, 138f, 24f), OnyxText.T("СОЗДАТЬ ЛОББИ", "CREATE LOBBY"), OnyxStyle.Current.Accent))
            OnyxToast.Push(OnyxText.T("Лобби", "Lobby"), OnyxLobbyTools.CreateLobby(), 2.5f, OnyxNotifyKind.Success);

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

        b = Card(x, ref y, w, OnyxText.T("Оформление лобби", "Lobby appearance"), 2f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Тёмная тема лобби", "Dark lobby theme"), OnyxConfig.LobbyTheme);
        Toggle(b.x, ref by, b.width, OnyxText.T("Анимации старта и панели", "Start & panel animations"), OnyxConfig.LobbyAnims);

        b = Card(x, ref y, w, OnyxText.T("Клоны лобби", "Lobby clones"), 4f * RowH + 372f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Режим клонов (ЛКМ=спавн, ПКМ=удал.)", "Clone mode (LMB=spawn, RMB=remove)"), OnyxConfig.LobbyCloneMode);
        Toggle(b.x, ref by, b.width, OnyxText.T("Клон-тень", "Shadow clone"), OnyxConfig.LobbyCloneShadow);
        Toggle(b.x, ref by, b.width, OnyxText.T("Клоны-охрана (кружат)", "Guard clones (orbit)"), OnyxConfig.LobbyCloneGuard);
        Toggle(b.x, ref by, b.width, OnyxText.T("Клоны бродят", "Clones wander"), OnyxConfig.LobbyCloneDrift);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Макс. клонов", "Max clones"), OnyxConfig.LobbyCloneMax, 1, 100);
        SliderInt(b.x, ref by, b.width, OnyxText.T("Клонов за клик", "Clones per click"), OnyxConfig.LobbyCloneSpawnCount, 1, 20);
        Slider(b.x, ref by, b.width, OnyxText.T("Радиус охраны", "Guard radius"), OnyxConfig.LobbyCloneGuardRadius, 1f, 8f, "0.0");
        Slider(b.x, ref by, b.width, OnyxText.T("Масштаб клонов", "Clone scale"), OnyxConfig.LobbyCloneScale, 0.4f, 2f, "0.00");
        SliderInt(b.x, ref by, b.width, OnyxText.T("Цвет (-1 = свой)", "Color (-1 = own)"), OnyxConfig.LobbyCloneColorId, -1, 17);

        string[] formNames = { OnyxText.T("Линия", "Line"), OnyxText.T("Круг", "Circle"), OnyxText.T("Треугольник", "Triangle"), OnyxText.T("Звезда", "Star"), OnyxText.T("Сердце", "Heart"), OnyxText.T("Ромб", "Diamond"), OnyxText.T("Спираль", "Spiral"), OnyxText.T("Крест", "Cross"), OnyxText.T("Волна", "Wave") };
        int cfi = Mathf.Clamp(OnyxConfig.CloneFormation.Value, 0, formNames.Length - 1);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Формация", "Formation"), formNames[cfi], () => { OnyxConfig.CloneFormation.Value = (cfi + 1) % formNames.Length; });
        SliderInt(b.x, ref by, b.width, OnyxText.T("Копии формации", "Formation copies"), OnyxConfig.LobbyCloneFormationCopies, 1, 5);
        if (SmallButton(new Rect(b.x + 2f, by, 132f, 24f), OnyxText.T("ПОСТРОИТЬ", "BUILD"), OnyxStyle.Current.Accent))
            OnyxLobbyClones.Instance?.BuildFormation(cfi);
        if (SmallButton(new Rect(b.x + 142f, by, 120f, 24f), OnyxText.T("ОЧИСТИТЬ", "CLEAR"), new Color(0.9f, 0.4f, 0.4f)))
            OnyxLobbyClones.Instance?.ClearAll();
        by += 32f;

        _cloneText = CustomText(new Rect(b.x + 2f, by, b.width - 168f, 26f), _cloneText ?? "", "cloneText");
        if (SmallButton(new Rect(b.x + b.width - 160f, by + 1f, 158f, 24f), OnyxText.T("ТЕКСТ ИЗ КЛОНОВ", "TEXT FROM CLONES"), OnyxStyle.Current.Accent))
            OnyxLobbyClones.Instance?.BuildText(_cloneText);
    }

    private void DrawPlayers(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, OnyxText.T("Роли и инфо", "Roles & info"), 3f * RowH);
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Показывать роли всех", "Reveal all roles"), OnyxConfig.RevealRoles);
        Toggle(b.x, ref by, b.width, OnyxText.T("Инфо над игроками (лобби)", "Info above players (lobby)"), OnyxConfig.VisualPlayerInfoNames);
        Toggle(b.x, ref by, b.width, OnyxText.T("Голоса на собрании", "Votes in meeting"), OnyxConfig.RevealVotes);

        b = Card(x, ref y, w, OnyxText.T("Модерация", "Moderation"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Кик/бан в матче (хост)", "Kick/ban in match (host)"), OnyxConfig.UnlockMatchKickBan);

        b = Card(x, ref y, w, OnyxText.T("Детект входящих", "Join detect"), RowH + 26f);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Показывать платформу/ур./raw", "Show platform/lvl/raw"), OnyxConfig.JoinDetect);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 24f), OnyxText.T("Тост при заходе; ⚠ на подозрит. raw-имя.", "Toast on join; ⚠ on suspicious raw name."), _muted);

        b = Card(x, ref y, w, OnyxText.T("Режим тела", "Body mode"), RowH);
        by = b.y;
        string[] bmVals = { "Disabled", "Horse", "Seeker", "Long", "LongHorse" };
        string[] bmDisp = { OnyxText.T("Выкл", "Off"), OnyxText.T("Лошадь", "Horse"), OnyxText.T("Сикер", "Seeker"), OnyxText.T("Длинный", "Long"), OnyxText.T("Длинная лошадь", "Long horse") };
        int bi = Mathf.Max(0, Array.IndexOf(bmVals, OnyxConfig.BodyMode.Value));
        CycleRow(b.x, ref by, b.width, OnyxText.T("Стиль тела", "Body style"), bmDisp[bi], () => { OnyxConfig.BodyMode.Value = bmVals[(bi + 1) % bmVals.Length]; });

        b = Card(x, ref y, w, OnyxText.T("Мышь и призрак", "Mouse & ghost"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Телепорт по ПКМ", "Teleport on RMB"), OnyxConfig.MouseTeleport);
        Toggle(b.x, ref by, b.width, OnyxText.T("Выбор мышью + ресайз колёсиком", "Mouse select + wheel resize"), OnyxConfig.MouseSelect);
        Toggle(b.x, ref by, b.width, OnyxText.T("Призрак после старта", "Ghost after start"), OnyxConfig.GhostAfterStart);

        b = Card(x, ref y, w, OnyxText.T("Цветной ник", "Colored name"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Включить цветной ник", "Enable colored name"), OnyxConfig.NameColor);
        int nci = OnyxNameColor.Clamp(OnyxConfig.NameColorStyle.Value);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Стиль", "Style"), OnyxNameColor.StyleName(nci), () => { OnyxConfig.NameColorStyle.Value = OnyxNameColor.Next(nci); });
        Toggle(b.x, ref by, b.width, OnyxText.T("Анимация", "Animation"), OnyxConfig.NameColorAnimated);

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

    private static void FunToast(string msg) => OnyxToast.Push(OnyxText.T("Фан", "Fun"), msg, 2.5f, OnyxNotifyKind.Info);
    private static string St(bool on) => on ? OnyxText.T(": вкл", ": on") : OnyxText.T(": выкл", ": off");
    private static Color FunCol(bool on) => on ? OnyxStyle.Current.Accent : new Color(0.5f, 0.55f, 0.62f);

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

        float spoofBody = 3f * RowH + (OnyxConfig.SpoofLevelEnabled.Value ? 50f : 0f);
        b = Card(x, ref y, w, OnyxText.T("Спуф", "Spoof"), spoofBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Спуф платформы", "Spoof platform"), OnyxConfig.SpoofPlatformEnabled);
        string[] platNames = { "Epic", "Steam", "Mac", "MS Store", "Itch", "iOS", "Android", "Switch", "Xbox", "PS", "Starlight" };
        int pi = Mathf.Clamp(OnyxConfig.SpoofPlatformIndex.Value, 0, 10);
        CycleRow(b.x, ref by, b.width, OnyxText.T("Платформа", "Platform"), platNames[pi], () => { OnyxConfig.SpoofPlatformIndex.Value = (pi + 1) % platNames.Length; });
        Toggle(b.x, ref by, b.width, OnyxText.T("Спуф уровня", "Spoof level"), OnyxConfig.SpoofLevelEnabled);
        if (OnyxConfig.SpoofLevelEnabled.Value)
            SliderInt(b.x, ref by, b.width, OnyxText.T("Уровень", "Level"), OnyxConfig.SpoofLevelValue, 1, 9999);
    }

    private void DrawVisual(float x, ref float y, float w)
    {
        Rect b = Card(x, ref y, w, OnyxText.T("Косметика", "Cosmetics"), 3f * RowH + 6f);
        float by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Разблокировать косметику", "Unlock cosmetics"), OnyxConfig.FreeCosmetics);
        Toggle(b.x, ref by, b.width, OnyxText.T("Прятать косметику в матче", "Hide cosmetics in match"), OnyxConfig.HideCosmeticsInMatch);
        Toggle(b.x, ref by, b.width, OnyxText.T("Одинаковые цвета (хост)", "Duplicate colors (host)"), OnyxConfig.AllowDuplicateColors);

        float camBody = 3f * RowH
            + (OnyxConfig.VisualFreeCamera.Value ? 50f : 0f)
            + (OnyxConfig.VisualCameraZoom.Value ? 50f : 0f);
        b = Card(x, ref y, w, OnyxText.T("Камера", "Camera"), camBody);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Свободная камера (WASD)", "Free camera (WASD)"), OnyxConfig.VisualFreeCamera);
        if (OnyxConfig.VisualFreeCamera.Value)
            SliderInt(b.x, ref by, b.width, OnyxText.T("Скорость камеры", "Camera speed"), OnyxConfig.VisualFreeCameraSpeed, 4, 30);
        Toggle(b.x, ref by, b.width, OnyxText.T("Зум колёсиком", "Wheel zoom"), OnyxConfig.VisualCameraZoom);
        if (OnyxConfig.VisualCameraZoom.Value)
            SliderInt(b.x, ref by, b.width, OnyxText.T("Макс. зум", "Max zoom"), OnyxConfig.VisualCameraMaxZoom, 4, 18);
        Toggle(b.x, ref by, b.width, OnyxText.T("Ноклип", "No-clip"), OnyxConfig.VisualNoClip);

        b = Card(x, ref y, w, "ESP", 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Трейсеры до игроков", "Tracers to players"), OnyxConfig.Tracers);
        Toggle(b.x, ref by, b.width, OnyxText.T("Трейсеры к телам", "Tracers to bodies"), OnyxConfig.TracerBodies);
        Toggle(b.x, ref by, b.width, OnyxText.T("КД килла над убийцами", "Kill CD over killers"), OnyxConfig.KillTimers);

        b = Card(x, ref y, w, OnyxText.T("Стелс", "Stealth"), RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Скрыть MOD-штамп", "Hide MOD stamp"), OnyxConfig.HideModStamp);

        b = Card(x, ref y, w, OnyxText.T("Пропуск анимаций", "Skip animations"), 3f * RowH);
        by = b.y;
        Toggle(b.x, ref by, b.width, OnyxText.T("Интро «Shhh»", "'Shhh' intro"), OnyxConfig.SkipShhh);
        Toggle(b.x, ref by, b.width, OnyxText.T("Выдача ролей", "Role reveal"), OnyxConfig.SkipRoleIntro);
        Toggle(b.x, ref by, b.width, OnyxText.T("Анимация килла", "Kill animation"), OnyxConfig.SkipKillAnim);

        b = Card(x, ref y, w, OnyxText.T("Избранные образы", "Favorite outfits"), 4f * 30f + 30f);
        by = b.y;
        for (int i = 0; i < 4; i++) FavoriteRow(b.x, ref by, b.width, i);
        if (SmallButton(new Rect(b.x, by, b.width, 24f), OnyxText.T("ОБРАЗ ВЫБРАННОГО → МНЕ", "TARGET'S OUTFIT → ME"), OnyxStyle.Current.Accent))
        {
            string cap = OnyxOutfits.Capture(OnyxMouseTools.Selected);
            OnyxToast.Push(OnyxText.T("Образ", "Outfit"), cap.Length > 0 && OnyxOutfits.Apply(PlayerControl.LocalPlayer, cap) ? OnyxText.T("скопирован", "copied") : OnyxText.T("нет цели", "no target"), 2f, OnyxNotifyKind.Info);
        }
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
        Rect b = Card(x, ref y, w, OnyxText.T("Интерфейс", "Interface"), RowH);
        float by = b.y;
        CycleRow(b.x, ref by, b.width, OnyxText.T("Язык", "Language"), OnyxText.LangName, () => OnyxText.Toggle());

        b = Card(x, ref y, w, OnyxText.T("Горячие клавиши", "Hotkeys"), 5f * RowH + 24f);
        by = b.y;
        KeyRow(b.x, ref by, b.width, OnyxText.T("Меню", "Menu"), OnyxConfig.MenuKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Код лобби", "Lobby code"), OnyxConfig.CopyCodeKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Завершить матч", "End match"), OnyxConfig.EndMatchKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Досчитать голоса", "Tally votes"), OnyxConfig.CloseVotingKey);
        KeyRow(b.x, ref by, b.width, OnyxText.T("Закрыть собрание", "Close meeting"), OnyxConfig.CloseMeetingKey);
        GUI.Label(new Rect(b.x + 2f, by, b.width - 2f, 22f), OnyxText.T("Клик — назначить, ПКМ — сброс, Esc — отмена.", "Click to set, right-click to clear, Esc to cancel."), _muted);

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
        float a = target;
        if (key != null)
        {
            if (!_pillAnim.TryGetValue(key, out a)) a = target;
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                a = Mathf.MoveTowards(a, target, Time.unscaledDeltaTime * 7f);
                _pillAnim[key] = a;
            }
        }
        float e = SmoothSat(a);
        int r = Mathf.RoundToInt(track.height / 2f);

        if (e > 0.01f)
            OnyxStyle.FillRounded(new Rect(track.x - 2f, track.y - 2f, track.width + 4f, track.height + 4f), A(p.Accent, 0.25f * e), r + 2);
        OnyxStyle.FillRounded(track, Color.Lerp(p.Button, p.Accent, e), r);
        if (e < 0.99f)
            OnyxStyle.StrokeRounded(track, A(Color.white, 0.06f * (1f - e)), r, 1);
        if (e > 0.01f)
            OnyxStyle.Fill(new Rect(track.x + 5f, track.y + 2f, track.width - 10f, 1f), A(Color.white, 0.28f * e));

        float knob = track.height - 6f;
        float kx = Mathf.Lerp(track.x + 3f, track.xMax - knob - 3f, e);
        OnyxStyle.FillRounded(new Rect(kx, track.y + 3f, knob, knob), Color.Lerp(p.Muted, Color.white, e), Mathf.RoundToInt(knob / 2f));
    }

    private void HoverFill(Rect r)
    {
        if (Event.current != null && Event.current.type == EventType.Repaint && r.Contains(Event.current.mousePosition))
            OnyxStyle.FillRounded(r, A(Color.white, 0.045f), 8);
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
        if (_built) return;
        _built = true;
        OnyxPalette p = OnyxStyle.Current;

        _windowBg = new GUIStyle();
        _invisible = new GUIStyle();
        _gradient = OnyxStyle.BuildGradient((int)_window.width, (int)FullH, Rgb(24, 32, 54), Rgb(11, 15, 27), 16);

        _brand = Label(p.Text, 25, FontStyle.Bold, TextAnchor.MiddleLeft);
        _verPill = Label(p.Accent, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
        _cardTitle = Label(A(p.Text, 0.82f), 11, FontStyle.Bold, TextAnchor.MiddleLeft);
        _rowLabel = Label(p.Text, 15, FontStyle.Normal, TextAnchor.MiddleLeft);
        _rowLabel.wordWrap = true;
        _value = Label(p.Accent, 13, FontStyle.Bold, TextAnchor.MiddleRight);
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
