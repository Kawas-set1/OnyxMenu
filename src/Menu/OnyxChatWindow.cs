using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Onyx;

public sealed class OnyxChatWindow : MonoBehaviour
{
    private const int Cap = 80;
    private const float HeadH = 30f;
    private const float RowMinH = 18f;
    private const int MaxInput = 100;

    private static readonly string[] Syms =
    {
        "★", "☆", "♥", "♦", "♣", "♠", "♪", "♫", "☺", "☻", "●", "○", "◆", "◇", "■", "□",
        "▲", "▼", "◄", "►", "←", "→", "↑", "↓", "«", "»", "‹", "›", "„", "”", "…", "—",
        "•", "✦", "✿", "！", "？", "™", "©", "°", "§", "±", "×", "÷"
    };

    private sealed class Line { public string Text; public float H; }

    private static readonly List<Line> _lines = new List<Line>(Cap);
    private static bool _stick = true;
    private static string _lastKey = "";
    private static float _lastAt = -10f;

    internal static bool Open => OnyxConfig.ChatWindow != null && OnyxConfig.ChatWindow.Value;
    internal static bool Typing { get; private set; }

    internal static void Feed(PlayerControl src, string chatText)
    {
        if (string.IsNullOrWhiteSpace(chatText)) return;
        try
        {
            string name = "?";
            bool local = false, dead = false;
            if (src != null && src.Data != null)
            {
                name = src.Data.PlayerName;
                local = src == PlayerControl.LocalPlayer;
                dead = src.Data.IsDead;
            }
            string sn = Clean(name, 24), st = Clean(chatText, 200);
            if (st.Length == 0) return;

            float now = Time.unscaledTime;
            string key = sn + "|" + st;
            if (key == _lastKey && now - _lastAt < 0.75f) return;
            _lastKey = key; _lastAt = now;

            OnyxPalette p = OnyxStyle.Current;
            Color nc = local ? p.Accent : (dead ? new Color(0.84f, 0.72f, 1f) : p.Text);
            string line = $"<color=#8892A0>[{DateTime.Now:HH:mm}]</color> <color=#{Hex(nc)}>{sn}</color>: {st}";
            while (_lines.Count >= Cap) _lines.RemoveAt(0);
            _lines.Add(new Line { Text = line });
            _stick = true;
        }
        catch { }
    }

    private Rect _win = new Rect(24f, 320f, 560f, 440f);
    private float _scroll;
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private GUIStyle _head, _clock, _row, _input, _btn, _empty;
    private readonly GUIContent _gc = new GUIContent();
    private string _text = "";
    private bool _edit;
    private bool _syms;
    private bool _moveBlocked;
    private bool _wasMoveable;

    public void Update()
    {
        bool block = Open && _edit;
        Typing = block;
        PlayerControl pc = PlayerControl.LocalPlayer;
        if (block && pc != null)
        {
            if (!_moveBlocked) { _wasMoveable = pc.moveable; _moveBlocked = true; }
            pc.moveable = false;
        }
        else if (_moveBlocked)
        {
            try { if (pc != null) pc.moveable = _wasMoveable; } catch { }
            _moveBlocked = false;
        }
    }

    internal void DrawGui()
    {
        if (!Open) { _edit = false; return; }
        Build();

        Event e = Event.current;
        _win.width = Mathf.Clamp(_win.width, 360f, Screen.width);
        _win.height = Mathf.Clamp(_win.height, 240f, Screen.height);
        _win.x = Mathf.Clamp(_win.x, 0f, Screen.width - _win.width);
        _win.y = Mathf.Clamp(_win.y, 0f, Screen.height - _win.height);

        OnyxPalette p = OnyxStyle.Current;
        float w = _win.width, h = _win.height;

        OnyxStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
        OnyxStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
        OnyxStyle.Fill(new Rect(_win.x + 10f, _win.y + HeadH - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

        _head.normal.textColor = p.Text;
        OnyxStyle.FillRounded(new Rect(_win.x + 12f, _win.y + 9f, 3f, 12f), p.Accent, 1);
        GUI.Label(new Rect(_win.x + 20f, _win.y + 5f, w - 175f, 20f), OnyxText.T("ЧАТ", "CHAT"), _head);
        _clock.normal.textColor = p.Muted;
        GUI.Label(new Rect(_win.x + w - 150f, _win.y + 7f, 56f, 16f), _lines.Count + "/" + Cap, _clock);

        Rect cpy = new Rect(_win.x + w - 80f, _win.y + 6f, 22f, 18f);
        Rect clr = new Rect(_win.x + w - 54f, _win.y + 6f, 22f, 18f);
        Rect cls = new Rect(_win.x + w - 28f, _win.y + 6f, 22f, 18f);
        if (Hover(cpy)) OnyxStyle.FillRounded(cpy, A(p.Accent, 0.20f), 6);
        if (Hover(clr)) OnyxStyle.FillRounded(clr, A(Color.white, 0.07f), 6);
        if (Hover(cls)) OnyxStyle.FillRounded(cls, A(new Color(0.9f, 0.3f, 0.3f), 0.25f), 6);
        Icon(cpy, OnyxIcon.Copy, Hover(cpy) ? p.Text : p.Muted);
        Icon(clr, OnyxIcon.Trash, Hover(clr) ? p.Text : p.Muted);
        Icon(cls, OnyxIcon.Close, Hover(cls) ? new Color(0.95f, 0.5f, 0.5f) : p.Muted);
        if (GUI.Button(cpy, GUIContent.none, GUIStyle.none)) CopyAll();
        if (GUI.Button(clr, GUIContent.none, GUIStyle.none)) _lines.Clear();
        if (GUI.Button(cls, GUIContent.none, GUIStyle.none)) { _edit = false; OnyxConfig.ChatWindow.Value = false; return; }

        int perRow = Mathf.Max(1, Mathf.FloorToInt((w - 12f) / 30f));
        int symRows = _syms ? Mathf.CeilToInt((float)Syms.Length / perRow) : 0;
        float symH = symRows * 28f;
        Rect inp = new Rect(_win.x + 8f, _win.y + h - 34f, w - 190f, 26f);
        Rect send = new Rect(inp.xMax + 6f, inp.y, 80f, 26f);
        Rect symBtn = new Rect(send.xMax + 6f, inp.y, 80f, 26f);
        float symTop = inp.y - symH - (symH > 0f ? 4f : 0f);
        float bodyTop = _win.y + HeadH + 4f;
        float bodyBottom = symH > 0f ? symTop : inp.y - 4f;
        Rect body = new Rect(_win.x + 6f, bodyTop, w - 12f, Mathf.Max(40f, bodyBottom - bodyTop));

        Log(body, p, e);

        if (symH > 0f)
        {
            float bw = (w - 16f - (perRow - 1) * 4f) / perRow;
            for (int i = 0; i < Syms.Length; i++)
            {
                Rect sb = new Rect(_win.x + 8f + (i % perRow) * (bw + 4f), symTop + (i / perRow) * 28f, bw, 24f);
                if (Btn(sb, Syms[i], A(Color.white, Hover(sb) ? 0.14f : 0.07f)))
                    if (_text.Length + Syms[i].Length <= MaxInput) _text += Syms[i];
            }
        }

        InputBox(inp, p, e);
        if (Btn(send, OnyxText.T("Отпр.", "Send"), Hover(send) ? A(p.Accent, 0.9f) : A(p.Accent, 0.72f))) Send();
        if (Btn(symBtn, "☺ +", _syms ? A(p.Accent, 0.7f) : A(Color.white, Hover(symBtn) ? 0.14f : 0.07f))) _syms = !_syms;

        Drag(e, new Rect(_win.x, _win.y, w - 90f, HeadH));
    }

    private void Log(Rect body, OnyxPalette p, Event e)
    {
        if (e != null && e.type == EventType.ScrollWheel && body.Contains(e.mousePosition))
        {
            _scroll += e.delta.y * 18f;
            _stick = false;
            e.Use();
        }

        float textW = body.width - 12f;
        Remeasure(textW);
        float total = 0f;
        for (int i = 0; i < _lines.Count; i++) total += RowHeight(_lines[i], textW);

        float maxScroll = Mathf.Max(0f, total - body.height);
        if (_stick) _scroll = maxScroll;
        _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);
        if (_scroll >= maxScroll - 1f) _stick = true;

        if (_lines.Count == 0)
        {
            _empty.normal.textColor = A(p.Muted, 0.8f);
            GUI.Label(body, OnyxText.T("Пока пусто.", "Empty for now."), _empty);
            return;
        }

        GUI.BeginGroup(body);
        float y = -_scroll;
        for (int i = 0; i < _lines.Count; i++)
        {
            float rh = RowHeight(_lines[i], textW);
            if (y + rh >= 0f && y <= body.height)
            {
                _row.normal.textColor = p.Text;
                GUI.Label(new Rect(4f, y + 1f, textW, rh), _lines[i].Text, _row);
            }
            y += rh;
        }
        GUI.EndGroup();

        if (maxScroll > 1f && total > 0f)
        {
            float th = Mathf.Max(24f, body.height * (body.height / total));
            float ty = body.y + (body.height - th) * (_scroll / maxScroll);
            OnyxStyle.FillRounded(new Rect(body.xMax - 3f, ty, 3f, th), A(p.Accent, 0.5f), 1);
        }
    }

    private void InputBox(Rect r, OnyxPalette p, Event e)
    {
        OnyxStyle.FillRounded(r, p.Button, 7);
        OnyxStyle.StrokeRounded(r, _edit ? A(p.Accent, 0.8f) : A(Color.white, 0.08f), 7, 1);

        if (e != null && e.type == EventType.MouseDown)
        {
            if (r.Contains(e.mousePosition)) _edit = true;
            else if (!_win.Contains(e.mousePosition)) _edit = false;
        }

        if (_edit && e != null && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Backspace) { if (_text.Length > 0) _text = _text.Substring(0, _text.Length - 1); e.Use(); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Send(); e.Use(); }
            else if (e.keyCode == KeyCode.Escape) { _edit = false; e.Use(); }
            else if (e.character != '\0' && !char.IsControl(e.character) && _text.Length < MaxInput) { _text += e.character; e.Use(); }
        }

        bool caret = _edit && Time.unscaledTime % 1f < 0.5f;
        string shown = _text.Length == 0 && !_edit ? OnyxText.T("Сообщение…", "Message…") : _text + (caret ? "|" : "");
        _input.normal.textColor = _text.Length == 0 && !_edit ? A(p.Muted, 0.7f) : p.Text;
        GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), shown, _input);
    }

    private void Send()
    {
        string msg = (_text ?? "").Trim();
        if (msg.Length == 0) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) { OnyxToast.Push(OnyxText.T("Чат", "Chat"), OnyxText.T("Не в игре.", "Not in game."), 1.8f, OnyxNotifyKind.Warning); return; }
        try { me.RpcSendChat(msg); } catch { }
        _text = "";
        _stick = true;
    }

    private float _measuredAt;

    private float RowHeight(Line l, float textW)
    {
        if (l.H > 0f) return l.H;
        _gc.text = l.Text;
        l.H = Mathf.Max(RowMinH, _row.CalcHeight(_gc, textW) + 2f);
        return l.H;
    }

    private void Remeasure(float textW)
    {
        if (Mathf.Abs(textW - _measuredAt) < 0.5f) return;
        _measuredAt = textW;
        for (int i = 0; i < _lines.Count; i++) _lines[i].H = 0f;
    }

    private bool Btn(Rect r, string label, Color bg)
    {
        OnyxStyle.FillRounded(r, bg, 6);
        _btn.normal.textColor = OnyxStyle.Current.Text;
        GUI.Label(r, label, _btn);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    private void Drag(Event e, Rect head)
    {
        if (e == null) return;
        if (e.type == EventType.MouseDown && e.button == 0 && head.Contains(e.mousePosition))
        {
            _drag = true;
            _grab = e.mousePosition - new Vector2(_win.x, _win.y);
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseDrag)
        {
            _win.x = e.mousePosition.x - _grab.x;
            _win.y = e.mousePosition.y - _grab.y;
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseUp) _drag = false;
    }

    private static void CopyAll()
    {
        if (_lines.Count == 0) return;
        var sb = new StringBuilder();
        for (int i = 0; i < _lines.Count; i++) sb.Append(StripTags(_lines[i].Text)).Append('\n');
        try { GUIUtility.systemCopyBuffer = sb.ToString(); } catch { }
        OnyxToast.Push(OnyxText.T("Скопировано", "Copied"), null, 1.6f, OnyxNotifyKind.Success);
    }

    private static string Clean(string v, int max)
    {
        if (string.IsNullOrEmpty(v)) return "";
        string s = StripTags(v).Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length > max ? s.Substring(0, max) : s;
    }

    private static string StripTags(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool tag = false;
        foreach (char c in s)
        {
            if (c == '<') tag = true;
            else if (c == '>') tag = false;
            else if (!tag) sb.Append(c);
        }
        return sb.ToString();
    }

    private static void Icon(Rect r, OnyxIcon icon, Color c) => OnyxIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), c);
    private static bool Hover(Rect r) => Event.current != null && r.Contains(Event.current.mousePosition);
    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        _head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleLeft };
        _clock = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
        _input = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        _btn = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter };
        _empty = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class OnyxChatWindowFeed
{
    public static void Postfix(PlayerControl sourcePlayer, string chatText) => OnyxChatWindow.Feed(sourcePlayer, chatText);
}
