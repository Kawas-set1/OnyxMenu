using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Onyx;

public sealed class OnyxEventLog : MonoBehaviour
{
    private sealed class Row { public string Clock; public string Text; public OnyxNotifyKind Kind; public float H; }

    private const int Cap = 200;
    private const float HeadH = 30f;
    private const float RowH = 18f;

    private static readonly List<Row> _rows = new List<Row>(Cap);
    private static readonly List<Row> _chat = new List<Row>(Cap);
    private static bool ConsoleOn => OnyxConfig.EventConsole != null && OnyxConfig.EventConsole.Value;
    private static bool _stick = true;

    internal static void Add(string text, OnyxNotifyKind kind)
    {
        if (string.IsNullOrEmpty(text)) return;
        while (_rows.Count >= Cap) _rows.RemoveAt(0);
        _rows.Add(new Row { Clock = DateTime.Now.ToString("HH:mm:ss"), Text = text, Kind = kind });
        _stick = true;
    }

    private Rect _win = new Rect(24f, 130f, 430f, 320f);
    private float _scroll;
    private bool _drag;
    private Vector2 _grab;
    private bool _built;
    private int _tab;
    private float _chatAt = -99f;
    private GUIStyle _head, _clock, _row, _tabS;
    private readonly GUIContent _gc = new GUIContent();

    private float _measuredAt;

    private List<Row> Active => _tab == 1 ? _chat : _rows;

    private void LoadChat()
    {
        _chat.Clear();
        try
        {
            string path = Patches.OnyxChatLog.FilePath;
            if (!File.Exists(path)) return;
            string[] all = File.ReadAllLines(path);
            int start = Mathf.Max(0, all.Length - Cap);
            for (int i = start; i < all.Length; i++)
            {
                string s = all[i];
                if (!string.IsNullOrWhiteSpace(s)) _chat.Add(ParseChat(s));
            }
        }
        catch { }
    }

    private static Row ParseChat(string line)
    {
        string clock = string.Empty, text = line;
        try
        {
            if (line.StartsWith("[") && line.IndexOf(']') > 0)
            {
                int rb = line.IndexOf(']');
                string ts = line.Substring(1, rb - 1);
                int sp = ts.LastIndexOf(' ');
                clock = sp >= 0 ? ts.Substring(sp + 1) : ts;
                string rest = line.Substring(rb + 1).Trim();
                string[] parts = rest.Split(new[] { " | " }, StringSplitOptions.None);
                text = parts.Length >= 2 ? parts[0].Trim() + ": " + parts[parts.Length - 1].Trim() : rest;
            }
        }
        catch { text = line; }
        return new Row { Clock = clock, Text = text, Kind = OnyxNotifyKind.Info };
    }

    private float RowHeight(Row r, float textW)
    {
        if (r.H > 0f) return r.H;
        _gc.text = r.Text;
        r.H = Mathf.Max(RowH, _row.CalcHeight(_gc, textW) + 2f);
        return r.H;
    }

    private void Remeasure(float textW)
    {
        if (Mathf.Abs(textW - _measuredAt) < 0.5f) return;
        _measuredAt = textW;
        for (int i = 0; i < _rows.Count; i++) _rows[i].H = 0f;
        for (int i = 0; i < _chat.Count; i++) _chat[i].H = 0f;
    }

    internal void DrawGui()
    {
        if (!ConsoleOn) return;
        Build();

        Event e = Event.current;
        _win.width = Mathf.Clamp(_win.width, 300f, Screen.width);
        _win.height = Mathf.Clamp(_win.height, 150f, Screen.height);
        _win.x = Mathf.Clamp(_win.x, 0f, Screen.width - _win.width);
        _win.y = Mathf.Clamp(_win.y, 0f, Screen.height - _win.height);

        OnyxPalette p = OnyxStyle.Current;
        float w = _win.width, h = _win.height;

        OnyxStyle.FillRounded(_win, A(p.Window, 0.95f), 12);
        OnyxStyle.StrokeRounded(_win, A(p.Accent, 0.28f), 12, 1);
        OnyxStyle.Fill(new Rect(_win.x + 10f, _win.y + HeadH - 1f, w - 20f, 1f), A(p.Accent, 0.35f));

        OnyxStyle.FillRounded(new Rect(_win.x + 12f, _win.y + 9f, 3f, 12f), p.Accent, 1);
        Tab(new Rect(_win.x + 20f, _win.y + 5f, 84f, 20f), 0, OnyxText.T("СОБЫТИЯ", "EVENTS"), p);
        Tab(new Rect(_win.x + 108f, _win.y + 5f, 52f, 20f), 1, OnyxText.T("ЧАТ", "CHAT"), p);

        if (_tab == 1 && Time.unscaledTime - _chatAt > 1f)
        {
            _chatAt = Time.unscaledTime;
            bool bottom = _stick;
            LoadChat();
            _stick = bottom;
        }

        List<Row> list = Active;

        _clock.normal.textColor = p.Muted;
        GUI.Label(new Rect(_win.x + w - 150f, _win.y + 7f, 56f, 16f), list.Count + "/" + Cap, _clock);

        Rect cpy = new Rect(_win.x + w - 80f, _win.y + 6f, 22f, 18f);
        Rect clr = new Rect(_win.x + w - 54f, _win.y + 6f, 22f, 18f);
        Rect cls = new Rect(_win.x + w - 28f, _win.y + 6f, 22f, 18f);
        if (Hover(cpy)) OnyxStyle.FillRounded(cpy, A(p.Accent, 0.20f), 6);
        if (Hover(clr)) OnyxStyle.FillRounded(clr, A(Color.white, 0.07f), 6);
        if (Hover(cls)) OnyxStyle.FillRounded(cls, A(new Color(0.9f, 0.3f, 0.3f), 0.25f), 6);
        Icon(cpy, OnyxIcon.Copy, Hover(cpy) ? p.Text : p.Muted);
        Icon(clr, OnyxIcon.Trash, Hover(clr) ? p.Text : p.Muted);
        Icon(cls, OnyxIcon.Close, Hover(cls) ? new Color(0.95f, 0.5f, 0.5f) : p.Muted);
        if (GUI.Button(cpy, GUIContent.none, GUIStyle.none)) CopyAll(list);
        if (GUI.Button(clr, GUIContent.none, GUIStyle.none)) list.Clear();
        if (GUI.Button(cls, GUIContent.none, GUIStyle.none)) { OnyxConfig.EventConsole.Value = false; return; }

        Rect body = new Rect(_win.x + 6f, _win.y + HeadH + 4f, w - 12f, h - HeadH - 10f);
        if (e != null && e.type == EventType.ScrollWheel && body.Contains(e.mousePosition))
        {
            _scroll += e.delta.y * 18f;
            _stick = false;
            e.Use();
        }

        float textW = body.width - 66f;
        Remeasure(textW);
        float total = 0f;
        for (int i = 0; i < list.Count; i++) total += RowHeight(list[i], textW);

        float maxScroll = Mathf.Max(0f, total - body.height);
        if (_stick) _scroll = maxScroll;
        _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);
        if (_scroll >= maxScroll - 1f) _stick = true;

        GUI.BeginGroup(body);
        Event ge = Event.current;
        float y = -_scroll;
        for (int i = 0; i < list.Count; i++)
        {
            Row r = list[i];
            float rh = RowHeight(r, textW);
            if (y + rh >= 0f && y <= body.height)
            {
                Rect rr = new Rect(0f, y, body.width, rh);
                bool hov = ge != null && rr.Contains(ge.mousePosition);
                if (hov) OnyxStyle.Fill(rr, A(p.Accent, 0.10f));
                _clock.normal.textColor = A(p.Muted, 0.8f);
                GUI.Label(new Rect(2f, y + 1f, 58f, RowH), r.Clock, _clock);
                _row.normal.textColor = KindColor(r.Kind, p);
                GUI.Label(new Rect(62f, y + 1f, textW, rh), r.Text, _row);
                if (hov && ge.type == EventType.MouseDown && ge.button == 0)
                {
                    Copy(r.Clock + "  " + r.Text);
                    ge.Use();
                }
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

        Drag(e, new Rect(_win.x + 166f, _win.y, Mathf.Max(0f, w - 166f - 66f), HeadH));
    }

    private void Tab(Rect r, int idx, string label, OnyxPalette p)
    {
        bool active = _tab == idx;
        if (active) OnyxStyle.FillRounded(r, A(p.Accent, 0.16f), 6);
        else if (Hover(r)) OnyxStyle.FillRounded(r, A(Color.white, 0.05f), 6);
        _tabS.normal.textColor = active ? p.Accent : A(p.Muted, 0.9f);
        GUI.Label(r, label, _tabS);
        if (GUI.Button(r, GUIContent.none, GUIStyle.none) && _tab != idx)
        {
            _tab = idx;
            _scroll = 0f;
            _stick = true;
            _measuredAt = -1f;
            if (idx == 1) _chatAt = -99f;
        }
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

    private static void CopyAll(List<Row> list)
    {
        if (list.Count == 0) return;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
            sb.Append(list[i].Clock).Append("  ").Append(list[i].Text).Append('\n');
        Copy(sb.ToString());
    }

    private static void Copy(string s)
    {
        try { GUIUtility.systemCopyBuffer = s; } catch { }
        OnyxToast.Push(OnyxText.T("Скопировано", "Copied"), null, 1.6f, OnyxNotifyKind.Success);
    }

    private static void Icon(Rect r, OnyxIcon icon, Color c) => OnyxIcons.Draw(icon, new Rect(r.x + r.width / 2f - 7f, r.y + r.height / 2f - 7f, 14f, 14f), c);

    private static bool Hover(Rect r) => Event.current != null && r.Contains(Event.current.mousePosition);

    private static Color KindColor(OnyxNotifyKind k, OnyxPalette p) => k switch
    {
        OnyxNotifyKind.Success => new Color(0.32f, 0.85f, 0.46f),
        OnyxNotifyKind.Danger => new Color(0.93f, 0.36f, 0.36f),
        OnyxNotifyKind.Warning => new Color(0.96f, 0.76f, 0.20f),
        _ => Color.Lerp(p.Text, p.Accent, 0.55f),
    };

    private void Build()
    {
        if (_built) return;
        _built = true;
        _head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleLeft };
        _clock = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
        _row = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true, clipping = TextClipping.Clip, alignment = TextAnchor.UpperLeft };
        _tabS = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
