using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.CrashReportHandler;

namespace Onyx;

[BepInProcess("Among Us.exe")]
[BepInPlugin(PluginId, PluginName, PluginVersion)]
public sealed class OnyxPlugin : BasePlugin
{
    public const string PluginId = "onyx.mod";
    public const string PluginName = "Onyx";
    public const string PluginVersion = "1.1.2";

    internal static ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new Harmony(PluginId);

    public override void Load()
    {
        OnyxDependencies.Setup();
        Logger = Log;
        OnyxDependencies.FlushLog();
        OnyxConfig.Bind(Config);

        InstallHarmonyXNoiseFilter();
        _harmony.PatchAll(typeof(OnyxPlugin).Assembly);
        ApplyTelemetryPreference();
        Patches.OnyxBanWords.Init();

        AddComponent<OnyxMenu>();
        AddComponent<OnyxHud>();
        AddComponent<OnyxToast>();
        AddComponent<OnyxLobby>();
        AddComponent<OnyxAutoLobbyReturn>();
        AddComponent<OnyxJoinDetector>();
        AddComponent<OnyxJoinLogger>();
        AddComponent<OnyxHistoryTracker>();
        AddComponent<OnyxTracers>();
        AddComponent<OnyxRadar>();
        AddComponent<OnyxMouseTools>();
        AddComponent<OnyxColoredName>();
        AddComponent<OnyxLobbyClones>();
        AddComponent<Patches.OnyxLobbyAnimDriver>();
        AddComponent<Patches.OnyxSpoofDriver>();
        AddComponent<Patches.OnyxAutoHost>();
        AddComponent<OnyxDummies>();
        AddComponent<Patches.OnyxAccessGuard>();
        AddComponent<Patches.OnyxModStampDriver>();
        AddComponent<OnyxLobbyPranks>();
        AddComponent<OnyxLobbyBar>();
        AddComponent<OnyxMusicPlayer>();
        AddComponent<OnyxDiscordPresence>();
        AddComponent<OnyxVotekick>();
        AddComponent<OnyxSabotage>();
        AddComponent<OnyxGodMode>();
        AddComponent<OnyxSpeed>();
        AddComponent<OnyxChatSender>();
        AddComponent<OnyxRadial>();
        AddComponent<OnyxEventNotify>();
        AddComponent<OnyxFakeTasks>();
        AddComponent<OnyxTwins>();
        AddComponent<OnyxUpdateCheck>();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private static void InstallHarmonyXNoiseFilter()
    {
        try
        {
            var listeners = BepInEx.Logging.Logger.Listeners;
            var toWrap = new List<ILogListener>(listeners);
            var collection = (ICollection<ILogListener>)listeners;
            foreach (var listener in toWrap)
            {
                collection.Remove(listener);
                collection.Add(new HarmonyXNoiseFilter(listener));
            }
        }
        catch
        {
        }
    }

    private sealed class HarmonyXNoiseFilter : ILogListener
    {
        private readonly ILogListener _inner;

        internal HarmonyXNoiseFilter(ILogListener inner) => _inner = inner;

        public LogLevel LogLevelFilter => _inner.LogLevelFilter;

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if (eventArgs.Level == LogLevel.Warning && eventArgs.Source?.SourceName == "HarmonyX")
            {
                return;
            }

            _inner.LogEvent(sender, eventArgs);
        }

        public void Dispose() => _inner.Dispose();
    }

    private static void ApplyTelemetryPreference()
    {
        if (!OnyxConfig.BlockTelemetry.Value || Application.platform == RuntimePlatform.Android)
        {
            return;
        }

        Analytics.enabled = false;
        Analytics.deviceStatsEnabled = false;
        Analytics.initializeOnStartup = false;
        Analytics.limitUserTracking = true;
        PerformanceReporting.enabled = false;
        CrashReportHandler.enableCaptureExceptions = false;
    }
}
