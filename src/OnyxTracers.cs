using UnityEngine;
using Object = UnityEngine.Object;

namespace Onyx;

public sealed class OnyxTracers : MonoBehaviour
{
    private GUIStyle _cd;
    private GUIStyle _cdShadow;

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
        if (!tracers && !timers) return;
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
