using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onyx.Patches;

internal static class OnyxLobbyTheme
{
    private enum Role { Neutral, Accent, Start, Success, Danger, DarkBlue }

    private static readonly Dictionary<int, Color> spriteOrig = new Dictionary<int, Color>();
    private static readonly Dictionary<int, SpriteRenderer> spriteRef = new Dictionary<int, SpriteRenderer>();
    private static readonly Dictionary<int, Color> textOrig = new Dictionary<int, Color>();
    private static readonly Dictionary<int, TMP_Text> textRef = new Dictionary<int, TMP_Text>();
    private static bool wasOn;
    private static float nextPaint;

    internal static void Apply(GameStartManager m, bool immediate)
    {
        if (m == null) return;

        if (!OnyxConfig.LobbyTheme.Value)
        {
            if (wasOn) RestoreAll();
            wasOn = false;
            return;
        }

        if (!immediate && Time.unscaledTime < nextPaint) return;
        nextPaint = Time.unscaledTime + 0.5f;
        wasOn = true;

        OnyxPalette p = OnyxStyle.Current;
        Color header = Color.Lerp(p.Text, p.Accent, 0.5f);
        Color surface = Color.Lerp(Color.Lerp(p.Panel, p.ButtonHover, 0.55f), p.Accent, 0.12f);

        PaintText(m.PlayerCounter, header);
        PaintText(m.GameRoomNameCode, p.Accent);
        PaintText(m.GameStartText, p.Text);
        PaintText(m.RulesPresetText, p.Accent);
        PaintText(m.privatePublicPanelText, p.Text);

        if (m.LobbyInfoPane != null)
        {
            PaintSprite(m.LobbyInfoPane.InfoPaneBackground, surface);
            foreach (PassiveButton b in ((Component)m.LobbyInfoPane).GetComponentsInChildren<PassiveButton>(true))
                PaintButton(b, p, Role.DarkBlue);
        }

        PaintButton(m.StartButton, p, Role.Start);
        PaintButton(m.EditButton, p, Role.DarkBlue);
        PaintButton(m.HostPublicButton, p, Role.Success);
        PaintButton(m.HostPrivateButton, p, Role.Danger);
        PaintButton(m.HostViewButton, p, Role.DarkBlue);
        PaintButton(m.ClientViewButton, p, Role.DarkBlue);

        BlendSprite(m.MapImage, p.Accent, 0.18f);
    }

    private static void PaintButton(PassiveButton b, OnyxPalette p, Role role)
    {
        if (b == null) return;

        Color fill, text;
        switch (role)
        {
            case Role.Start: fill = Color.Lerp(p.ButtonHover, p.Accent, 0.45f); text = Color.white; break;
            case Role.Accent: fill = Color.Lerp(p.Button, p.Accent, 0.42f); text = p.Text; break;
            case Role.Success: fill = new Color(0.10f, 0.40f, 0.22f, 1f); text = new Color(0.94f, 1f, 0.95f, 1f); break;
            case Role.Danger: fill = new Color(0.42f, 0.06f, 0.10f, 1f); text = new Color(1f, 0.96f, 0.96f, 1f); break;
            case Role.DarkBlue: fill = Color.Lerp(new Color(0.02f, 0.027f, 0.045f, 1f), p.Accent, 0.45f); text = Color.white; break;
            default: fill = Color.Lerp(p.Panel, p.Button, 0.6f); text = p.Text; break;
        }

        b.activeTextColor = text;
        b.inactiveTextColor = text;
        b.selectedTextColor = text;
        b.selectedInactiveTextColor = text;
        b.disabledTextColor = new Color(p.Muted.r, p.Muted.g, p.Muted.b, 0.55f);

        foreach (SpriteRenderer r in ((Component)b).GetComponentsInChildren<SpriteRenderer>(true))
            if (r != null && !SkipSprite(r)) PaintSprite(r, fill);

        foreach (Image img in ((Component)b).GetComponentsInChildren<Image>(true))
            if (img != null) ((Graphic)img).color = fill;

        foreach (ButtonRolloverHandler ro in ((Component)b).GetComponentsInChildren<ButtonRolloverHandler>(true))
            if (ro != null) Object.Destroy(ro);

        Button ui = ((Component)b).GetComponent<Button>();
        if (ui != null) ui.transition = Selectable.Transition.None;

        PaintText(b.buttonText, text);
    }

    private static bool SkipSprite(SpriteRenderer r)
    {
        Component c = r;
        return c.GetComponentInParent<PlayerControl>() != null || c.GetComponentInParent<PoolablePlayer>() != null;
    }

    private static void PaintSprite(SpriteRenderer r, Color col)
    {
        if (r == null) return;
        int id = r.GetInstanceID();
        if (!spriteOrig.ContainsKey(id)) { spriteOrig[id] = r.color; spriteRef[id] = r; }
        r.color = new Color(col.r, col.g, col.b, spriteOrig[id].a);
    }

    private static void BlendSprite(SpriteRenderer r, Color tint, float amount)
    {
        if (r == null) return;
        int id = r.GetInstanceID();
        if (!spriteOrig.ContainsKey(id)) { spriteOrig[id] = r.color; spriteRef[id] = r; }
        Color mix = Color.Lerp(spriteOrig[id], tint, amount);
        mix.a = spriteOrig[id].a;
        r.color = mix;
    }

    private static void PaintText(TMP_Text t, Color col)
    {
        if (t == null) return;
        int id = t.GetInstanceID();
        if (!textOrig.ContainsKey(id)) { textOrig[id] = t.color; textRef[id] = t; }
        t.color = new Color(col.r, col.g, col.b, textOrig[id].a);
    }

    private static void RestoreAll()
    {
        foreach (KeyValuePair<int, SpriteRenderer> pair in spriteRef)
            if (pair.Value != null && spriteOrig.TryGetValue(pair.Key, out Color c)) pair.Value.color = c;
        foreach (KeyValuePair<int, TMP_Text> pair in textRef)
            if (pair.Value != null && textOrig.TryGetValue(pair.Key, out Color c)) pair.Value.color = c;

        spriteOrig.Clear();
        spriteRef.Clear();
        textOrig.Clear();
        textRef.Clear();
    }
}

[HarmonyPatch(typeof(GameStartManager), "Start")]
internal static class LobbyThemeStartPatch
{
    public static void Postfix(GameStartManager __instance)
    {
        try { OnyxLobbyTheme.Apply(__instance, true); } catch { }
    }
}

[HarmonyPatch(typeof(GameStartManager), "Update")]
internal static class LobbyThemeUpdatePatch
{
    public static void Postfix(GameStartManager __instance)
    {
        try { OnyxLobbyTheme.Apply(__instance, false); } catch { }
    }
}
