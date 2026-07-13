using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Onyx;

public sealed class OnyxHud : MonoBehaviour
{
    private const float LobbyLifetime = 10f * 60f;
    private const float Refresh = 0.25f;

    internal static int CurrentFps = 60;

    private int _frames;
    private float _accum;

    private static int _lobbyGameId = -1;
    private static float _lobbyStart = -1f;

    public void Update()
    {
        _frames++;
        _accum += Mathf.Max(Time.unscaledDeltaTime, 0f);
        if (_accum >= Refresh)
        {
            CurrentFps = Mathf.Clamp(Mathf.RoundToInt(_frames / Mathf.Max(_accum, 0.0001f)), 1, 999);
            _frames = 0;
            _accum = 0f;
        }

        if (OnyxConfig.CopyCodeKey != null && Input.GetKeyDown(OnyxConfig.CopyCodeKey.Value))
            CopyLobbyCode();

        if (OnyxConfig.EndMatchKey != null && Input.GetKeyDown(OnyxConfig.EndMatchKey.Value))
            TryEndMatch();

        if (!OnyxMenu.Rebinding)
        {
            if (OnyxConfig.GodModeKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.GodModeKey.Value))
            {
                OnyxConfig.GodMode.Value = !OnyxConfig.GodMode.Value;
                OnyxToast.Push("God Mode", OnyxConfig.GodMode.Value ? OnyxText.T("Вкл", "On") : OnyxText.T("Выкл", "Off"), 1.5f, OnyxNotifyKind.Info);
            }
            if (OnyxConfig.MirageKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.MirageKey.Value))
            {
                OnyxConfig.LagComp.Value = !OnyxConfig.LagComp.Value;
                OnyxToast.Push(OnyxText.T("Мираж", "Mirage"), OnyxConfig.LagComp.Value ? OnyxText.T("Вкл", "On") : OnyxText.T("Выкл", "Off"), 1.5f, OnyxNotifyKind.Info);
            }
            if (OnyxConfig.VotekickKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.VotekickKey.Value))
                OnyxVotekick.ToggleAuto();
            if (OnyxConfig.SabotageKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.SabotageKey.Value))
                OnyxSabotage.All();
            if (OnyxConfig.DoorsKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.DoorsKey.Value))
                OnyxDoors.CloseAll();
            if (OnyxConfig.InvisibleKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.InvisibleKey.Value))
            {
                OnyxConfig.Invisible.Value = !OnyxConfig.Invisible.Value;
                OnyxToast.Push(OnyxText.T("Невидимость", "Invisibility"), OnyxConfig.Invisible.Value ? OnyxText.T("Вкл", "On") : OnyxText.T("Выкл", "Off"), 1.5f, OnyxNotifyKind.Info);
            }
        }

        OnyxXmas.Tick();

        if (!OnyxMenu.Rebinding && MeetingHud.Instance != null)
        {
            if (OnyxConfig.CloseVotingKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.CloseVotingKey.Value))
                OnyxToast.Push(OnyxText.T("Голосование", "Voting"), OnyxMeetingTools.CloseVoting(), 2f, OnyxNotifyKind.Info);
            if (OnyxConfig.CloseMeetingKey.Value != KeyCode.None && Input.GetKeyDown(OnyxConfig.CloseMeetingKey.Value))
                OnyxToast.Push(OnyxText.T("Собрание", "Meeting"), OnyxMeetingTools.CloseMeeting(), 2f, OnyxNotifyKind.Info);
        }
    }

    public void LateUpdate()
    {
        int fps = OnyxConfig.FpsLock30.Value ? 30 : (OnyxConfig.FpsCap.Value >= 300 ? -1 : OnyxConfig.FpsCap.Value);
        if (Application.targetFrameRate != fps || QualitySettings.vSyncCount != 0 || QualitySettings.maxQueuedFrames != 2)
        {
            QualitySettings.vSyncCount = 0;
            QualitySettings.maxQueuedFrames = 2;
            Application.targetFrameRate = fps;
        }
    }

    private static void TryEndMatch()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (ShipStatus.Instance == null || LobbyBehaviour.Instance != null || GameManager.Instance == null) return;
        try { GameManager.Instance.RpcEndGame((GameOverReason)1, false); } catch { }
    }

    internal static void RenderTracker(PingTracker tracker)
    {
        if (tracker == null || tracker.text == null) return;

        if (OnyxConfig.LobbyBar != null && OnyxConfig.LobbyBar.Value && LobbyBehaviour.Instance != null)
        {
            tracker.text.text = string.Empty;
            return;
        }

        int ping = 0;
        try { if (AmongUsClient.Instance != null) ping = ((InnerNetClient)AmongUsClient.Instance).Ping; }
        catch { ping = 0; }

        string text = $"PING: {ping} ms";
        string details = BuildDetails();
        if (details.Length > 0)
            text += "\n<size=62%><color=#D9E1EC>" + details + "</color></size>";

        TMP_Text t = tracker.text;
        t.richText = true;
        t.enableWordWrapping = false;
        t.alignment = TextAlignmentOptions.Center;
        t.lineSpacing = -6f;
        t.overflowMode = TextOverflowModes.Overflow;
        t.text = text;
    }

    private static string BuildDetails()
    {
        int remaining = 0;
        bool showFps = OnyxConfig.ShowFps != null && OnyxConfig.ShowFps.Value;
        bool hasTimer = OnyxConfig.ShowLobbyTimer != null && OnyxConfig.ShowLobbyTimer.Value && TryLobbyTimer(out remaining);

        string result = string.Empty;
        if (showFps)
        {
            int fps = CurrentFps;
            string c = fps >= 55 ? "7FEC9A" : fps >= 30 ? "EDE87A" : fps >= 20 ? "F5A84A" : "F07070";
            result = $"<color=#{c}>●</color> <b><color=#{c}>{fps}</color></b> <color=#607888>fps</color>";
        }

        if (hasTimer)
        {
            string value = $"{remaining / 60}:{remaining % 60:00}";
            string timer;
            if (remaining < 60)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.6f);
                pulse = pulse * pulse * (3f - 2f * pulse);
                Color col = Color.Lerp(new Color(0.74f, 0.16f, 0.19f), new Color(1f, 0.48f, 0.48f), pulse);
                string pc = Hex(col);
                timer = $"<color=#{pc}>●</color> <color=#607888>Лобби:</color> <b><color=#{pc}>{value}</color></b>";
            }
            else
            {
                timer = $"<color=#D4E4F0>●</color> <color=#607888>Лобби:</color> <color=#D4E4F0>{value}</color>";
            }

            result = result.Length > 0 ? result + "   <color=#2E3E4A>│</color>   " + timer : timer;
        }

        return result;
    }

    private static bool TryLobbyTimer(out int remaining)
    {
        remaining = 0;
        if (LobbyBehaviour.Instance == null || AmongUsClient.Instance == null)
        {
            _lobbyStart = -1f;
            _lobbyGameId = -1;
            return false;
        }

        int gid = ((InnerNetClient)AmongUsClient.Instance).GameId;
        if (_lobbyStart < 0f || gid != _lobbyGameId)
        {
            _lobbyGameId = gid;
            float elapsed = LobbyBehaviour.Instance.optionsTimer;
            float seed = elapsed > 0f && elapsed < LobbyLifetime ? elapsed : 0f;
            _lobbyStart = Time.realtimeSinceStartup - seed;
        }

        remaining = Mathf.Max(0, Mathf.CeilToInt(LobbyLifetime - (Time.realtimeSinceStartup - _lobbyStart)));
        return true;
    }

    private static void CopyLobbyCode()
    {
        if (AmongUsClient.Instance == null) return;
        int id = ((InnerNetClient)AmongUsClient.Instance).GameId;
        string code = GameCode.IntToGameName(id);
        if (string.IsNullOrEmpty(code))
        {
            OnyxToast.Push("Код лобби недоступен");
            return;
        }

        GUIUtility.systemCopyBuffer = code;
        OnyxToast.Push($"Код скопирован: <b>{code}</b>");
    }

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }
}

[HarmonyPatch(typeof(PingTracker), "Update")]
internal static class OnyxPingTrackerPatch
{
    public static bool Prefix(PingTracker __instance)
    {
        try
        {
            OnyxHud.RenderTracker(__instance);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
