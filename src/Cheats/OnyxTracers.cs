using UnityEngine;
using Object = UnityEngine.Object;

namespace Onyx;

public sealed class OnyxTracers : MonoBehaviour
{
    private GUIStyle _cd;
    private GUIStyle _cdShadow;
    private GUIStyle _tag;

    public void Update()
    {
        if (OnyxConfig.KillTimers.Value && ShipStatus.Instance != null && MeetingHud.Instance == null)
            Patches.KillTimers.Tick(Time.deltaTime);
    }

    public void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        bool tracers = OnyxConfig.Tracers.Value;
        bool timers = OnyxConfig.KillTimers.Value;
        bool esp = OnyxConfig.EspBoxes.Value;
        if (!tracers && !timers && !esp) return;
        if (MeetingHud.Instance != null) return;
        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        Camera cam = Camera.main;
        if (me == null || cam == null) return;

        Vector2 from = ToScreen(cam, me.GetTruePosition());

        if (tracers)
        {
            var it = PlayerControl.AllPlayerControls.GetEnumerator();
            while (it.MoveNext())
            {
                PlayerControl p = it.Current;
                if (p == null || p == me || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
                Line(from, ToScreen(cam, p.GetTruePosition()), BodyColor(p), 2.5f);
            }

            if (OnyxConfig.TracerBodies.Value && ShipStatus.Instance != null)
            {
                var bodies = Object.FindObjectsOfType<DeadBody>();
                for (int i = 0; i < bodies.Length; i++)
                {
                    DeadBody d = bodies[i];
                    if (d != null) Line(from, ToScreen(cam, d.transform.position), new Color(1f, 0.84f, 0.2f, 0.9f), 2.5f);
                }
            }
        }

        if (timers && ShipStatus.Instance != null)
        {
            if (_cd == null) BuildTimerStyles();
            var it = PlayerControl.AllPlayerControls.GetEnumerator();
            while (it.MoveNext())
            {
                PlayerControl p = it.Current;
                if (p == null || p == me || !Patches.KillTimers.Killer(p)) continue;
                if (Patches.KillTimers.TryGet(p.PlayerId, out float sec))
                    DrawTimer(ToScreen(cam, p.GetTruePosition()), sec);
            }
        }

        if (esp)
        {
            bool names = OnyxConfig.EspNames.Value;
            bool seeDead = OnyxConfig.EspDead.Value;
            Vector2 mePos = me.GetTruePosition();
            var it = PlayerControl.AllPlayerControls.GetEnumerator();
            while (it.MoveNext())
            {
                PlayerControl p = it.Current;
                if (p == null || p == me || p.Data == null || p.Data.Disconnected) continue;
                bool dead = p.Data.IsDead;
                if (dead && !seeDead) continue;

                Vector2 w = p.GetTruePosition();
                Vector2 sp = ToScreen(cam, w + Vector2.up * 0.45f);
                float ppu = (ToScreen(cam, w) - ToScreen(cam, w + Vector2.up)).magnitude;
                if (ppu < 4f) ppu = 42f;
                float hw = ppu * 0.42f, hh = ppu * 0.62f;

                Color col = BodyColor(p);
                if (dead) col.a *= 0.5f;
                Box(sp, hw, hh, col);

                if (names)
                {
                    float dist = Vector2.Distance(w, mePos);
                    string nm = p.Data.PlayerName;
                    if (string.IsNullOrEmpty(nm)) nm = "?";
                    Tag(new Vector2(sp.x, sp.y - hh - 12f), nm + "  " + Mathf.RoundToInt(dist) + "m", col);
                }
            }
        }
    }

    private static void Box(Vector2 c, float hw, float hh, Color col)
    {
        float t = 2f;
        float x = c.x - hw, y = c.y - hh, w = hw * 2f, h = hh * 2f;
        float br = Mathf.Min(hw, hh) * 0.6f;
        OnyxStyle.Fill(new Rect(x, y, br, t), col);
        OnyxStyle.Fill(new Rect(x + w - br, y, br, t), col);
        OnyxStyle.Fill(new Rect(x, y + h - t, br, t), col);
        OnyxStyle.Fill(new Rect(x + w - br, y + h - t, br, t), col);
        OnyxStyle.Fill(new Rect(x, y, t, br), col);
        OnyxStyle.Fill(new Rect(x, y + h - br, t, br), col);
        OnyxStyle.Fill(new Rect(x + w - t, y, t, br), col);
        OnyxStyle.Fill(new Rect(x + w - t, y + h - br, t, br), col);
    }

    private void Tag(Vector2 sp, string txt, Color col)
    {
        if (_tag == null)
            _tag = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var r = new Rect(sp.x - 90f, sp.y - 8f, 180f, 16f);
        _tag.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), txt, _tag);
        _tag.normal.textColor = col;
        GUI.Label(r, txt, _tag);
    }

    private void DrawTimer(Vector2 sp, float sec)
    {
        bool ready = sec <= 0.05f;
        string txt = ready ? "ГОТОВ" : sec.ToString("0.0");
        var r = new Rect(sp.x - 42f, sp.y - 66f, 84f, 20f);

        _cdShadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), txt, _cdShadow);
        _cd.normal.textColor = ready ? new Color(0.92f, 0.32f, 0.36f) : new Color(1f, 0.86f, 0.42f);
        GUI.Label(r, txt, _cd);
    }

    private void BuildTimerStyles()
    {
        _cd = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        _cdShadow = new GUIStyle(_cd);
    }

    private static Vector2 ToScreen(Camera cam, Vector2 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
        return new Vector2(sp.x, Screen.height - sp.y);
    }

    private static Color BodyColor(PlayerControl p)
    {
        try
        {
            int id = p.CurrentOutfit.ColorId;
            if (id >= 0 && id < Palette.PlayerColors.Length)
            {
                Color c = Palette.PlayerColors[id];
                c.a = 0.9f;
                return c;
            }
        }
        catch { }
        return new Color(1f, 1f, 1f, 0.85f);
    }

    private static void Line(Vector2 a, Vector2 b, Color col, float w)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 2f) return;

        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, a);
        OnyxStyle.Fill(new Rect(a.x, a.y - w * 0.5f, len, w), col);
        GUI.matrix = m;
    }
}
