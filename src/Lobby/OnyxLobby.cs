using UnityEngine;

namespace Onyx;

public sealed class OnyxLobby : MonoBehaviour
{
    private GUIStyle _mark;

    public void OnGUI()
    {
        if (OnyxConfig.LobbyBrand == null || !OnyxConfig.LobbyBrand.Value) return;
        if (LobbyBehaviour.Instance == null) return;

        EnsureStyles();
        OnyxPalette p = OnyxStyle.Current;
        Color accent = Patches.OnyxLobbyTheme.LobbyAccent(p.Accent);

        var badge = new Rect(22f, 16f, 158f, 48f);
        OnyxStyle.FillRounded(new Rect(badge.x - 2f, badge.y + 3f, badge.width + 4f, badge.height + 5f), new Color(0f, 0f, 0f, 0.32f), 15);
        OnyxStyle.FillRounded(badge, new Color(p.Window.r, p.Window.g, p.Window.b, 0.82f), 13);
        OnyxStyle.FillRounded(badge, A(accent, 0.07f), 13);
        OnyxStyle.FillRounded(new Rect(badge.x, badge.y, badge.width, badge.height * 0.5f), A(Color.white, 0.04f), 13);
        OnyxStyle.StrokeRounded(badge, A(accent, 0.5f), 13, 1);
        OnyxStyle.Fill(new Rect(badge.x + 12f, badge.y + 1f, badge.width - 24f, 1f), A(Color.white, 0.10f));
        OnyxStyle.FillRounded(new Rect(badge.x + 9f, badge.y + 11f, 3f, badge.height - 22f), accent, 2);

        DrawCrystal(new Rect(badge.x + 20f, badge.center.y - 15f, 30f, 30f), accent);

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Label(new Rect(badge.x + 60f, badge.y + 1f, badge.width - 62f, badge.height), "ONYX", _mark);
        GUI.color = p.Text;
        GUI.Label(new Rect(badge.x + 59f, badge.y, badge.width - 62f, badge.height), "ONYX", _mark);
        GUI.color = Color.white;
    }

    private static void DrawCrystal(Rect box, Color accent)
    {
        Vector2 c = box.center;
        int r = Mathf.RoundToInt(box.width / 2f);
        OnyxStyle.FillRounded(new Rect(c.x - box.width / 2f, c.y - box.height / 2f, box.width, box.height), A(accent, 0.16f), r);

        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, c);
        float s = box.width * 0.62f;
        var sq = new Rect(c.x - s / 2f, c.y - s / 2f, s, s);
        OnyxStyle.FillRounded(sq, accent, 4);
        OnyxStyle.FillRounded(new Rect(sq.x, sq.y, sq.width, sq.height * 0.5f), A(Color.white, 0.20f), 4);
        OnyxStyle.Fill(new Rect(sq.x + 2f, c.y - 0.6f, sq.width - 4f, 1.2f), A(Color.black, 0.22f));
        OnyxStyle.Fill(new Rect(c.x - 0.6f, sq.y + 2f, 1.2f, sq.height - 4f), A(Color.black, 0.22f));
        GUI.matrix = m;

        OnyxStyle.FillRounded(new Rect(box.x + 7f, box.y + 6f, 3.5f, 3.5f), A(Color.white, 0.85f), 2);
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private void EnsureStyles()
    {
        if (_mark != null) return;
        _mark = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 26
        };
        _mark.normal.textColor = Color.white;
    }
}
