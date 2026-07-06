using BepInEx.Configuration;
using UnityEngine;

namespace Onyx;

internal static class OnyxConfig
{
    internal static ConfigEntry<KeyCode> MenuKey;
    internal static ConfigEntry<KeyCode> CopyCodeKey;
    internal static ConfigEntry<KeyCode> EndMatchKey;
    internal static ConfigEntry<KeyCode> CloseVotingKey;
    internal static ConfigEntry<KeyCode> CloseMeetingKey;
    internal static ConfigEntry<bool> FpsLock30;
    internal static ConfigEntry<int> FpsCap;

    internal static ConfigEntry<bool> ShowFps;
    internal static ConfigEntry<bool> ShowLobbyTimer;
    internal static ConfigEntry<bool> Toasts;
    internal static ConfigEntry<bool> LobbyBrand;
    internal static ConfigEntry<bool> LobbyBar;

    internal static ConfigEntry<float> HudScale;

    internal static ConfigEntry<bool> FreeCosmetics;
    internal static ConfigEntry<bool> HideCosmeticsInMatch;
    internal static ConfigEntry<bool> AllowDuplicateColors;
    internal static ConfigEntry<string>[] FavoriteOutfits;

    internal static ConfigEntry<bool> RemoveGuestLimits;
    internal static ConfigEntry<bool> RemoveMinorLimits;
    internal static ConfigEntry<bool> ClearDisconnectPenalty;

    internal static ConfigEntry<bool> BlockTelemetry;

    internal static ConfigEntry<bool> AlwaysUnlockStartButton;
    internal static ConfigEntry<bool> QuickStartOnEnter;
    internal static ConfigEntry<bool> InstantStartOnEnter;
    internal static ConfigEntry<bool> AutoReturnLobbyAfterMatch;
    internal static ConfigEntry<bool> UnlockMatchKickBan;
    internal static ConfigEntry<bool> RichLobbyRows;
    internal static ConfigEntry<bool> JoinDetect;

    internal static ConfigEntry<bool> AccessBanEnabled;
    internal static ConfigEntry<bool> AccessWhitelistOnly;
    internal static ConfigEntry<string> BanList;
    internal static ConfigEntry<string> WhiteList;
    internal static ConfigEntry<bool> MinLevelEnabled;
    internal static ConfigEntry<int> MinLevel;
    internal static ConfigEntry<string> MinLevelAction;
    internal static ConfigEntry<bool> MaxLevelEnabled;
    internal static ConfigEntry<int> MaxLevel;
    internal static ConfigEntry<string> MaxLevelAction;
    internal static ConfigEntry<bool> VoteKickProtect;
    internal static ConfigEntry<string> VoteKickAction;
    internal static ConfigEntry<bool> KickFortegreen;
    internal static ConfigEntry<bool> AccessNickBanEnabled;
    internal static ConfigEntry<string> NickBanList;
    internal static ConfigEntry<bool> NameHistory;
    internal static ConfigEntry<bool> ColorReservationsEnabled;

    internal static ConfigEntry<bool> VisualFreeCamera;
    internal static ConfigEntry<int> VisualFreeCameraSpeed;
    internal static ConfigEntry<bool> VisualCameraZoom;
    internal static ConfigEntry<int> VisualCameraMaxZoom;
    internal static ConfigEntry<bool> VisualNoClip;
    internal static ConfigEntry<bool> HideModStamp;
    internal static ConfigEntry<bool> SkipShhh;
    internal static ConfigEntry<bool> SkipRoleIntro;
    internal static ConfigEntry<bool> SkipKillAnim;
    internal static ConfigEntry<bool> ChatCmdXmas;
    internal static ConfigEntry<bool> RevealRoles;
    internal static ConfigEntry<bool> VisualPlayerInfoNames;
    internal static ConfigEntry<bool> VisualAlwaysShowChat;
    internal static ConfigEntry<bool> Tracers;
    internal static ConfigEntry<bool> TracerBodies;
    internal static ConfigEntry<bool> KillTimers;
    internal static ConfigEntry<bool> RevealVotes;
    internal static ConfigEntry<bool> MouseTeleport;
    internal static ConfigEntry<bool> MouseSelect;
    internal static ConfigEntry<bool> GhostAfterStart;
    internal static ConfigEntry<int> FakeMapId;

    internal static ConfigEntry<bool> NameColor;
    internal static ConfigEntry<int> NameColorStyle;
    internal static ConfigEntry<bool> NameColorAnimated;
    internal static ConfigEntry<bool> ChatLog;
    internal static ConfigEntry<bool> BanWords;

    internal static ConfigEntry<bool> LobbyCloneMode;
    internal static ConfigEntry<bool> LobbyCloneShadow;
    internal static ConfigEntry<bool> LobbyCloneGuard;
    internal static ConfigEntry<float> LobbyCloneGuardRadius;
    internal static ConfigEntry<bool> LobbyCloneDrift;
    internal static ConfigEntry<int> LobbyCloneMax;
    internal static ConfigEntry<int> LobbyCloneSpawnCount;
    internal static ConfigEntry<int> LobbyCloneColorId;
    internal static ConfigEntry<float> LobbyCloneScale;
    internal static ConfigEntry<int> CloneFormation;
    internal static ConfigEntry<int> LobbyCloneFormationCopies;

    internal static ConfigEntry<bool> BetterChat;
    internal static ConfigEntry<int> ChatHistorySize;
    internal static ConfigEntry<bool> DarkChatTheme;
    internal static ConfigEntry<bool> ChatBubbleSenderInfo;
    internal static ConfigEntry<bool> UnlimitedChatLength;
    internal static ConfigEntry<bool> SkipChatCooldown;
    internal static ConfigEntry<bool> GhostChat;

    internal static ConfigEntry<bool> AutoHostEnabled;
    internal static ConfigEntry<int> AutoHostMinPlayers;
    internal static ConfigEntry<int> AutoHostStartDelaySeconds;
    internal static ConfigEntry<int> AutoHostBackoffSeconds;
    internal static ConfigEntry<bool> AutoHostInstantStart;
    internal static ConfigEntry<int> AutoHostWarmupSeconds;
    internal static ConfigEntry<int> AutoHostLoadGraceSeconds;
    internal static ConfigEntry<int> AutoHostFastStartPlayers;
    internal static ConfigEntry<int> AutoHostFastStartDelaySeconds;
    internal static ConfigEntry<int> AutoHostForceAfterMinutes;
    internal static ConfigEntry<int> AutoHostForceMinPlayers;
    internal static ConfigEntry<bool> AutoHostCancelBelowMin;
    internal static ConfigEntry<bool> AutoHostWaitLoadedPlayers;
    internal static ConfigEntry<bool> AutoHostReturnAfterMatch;
    internal static ConfigEntry<bool> AutoHostForceLastMinute;
    internal static ConfigEntry<bool> AutoHostNotifications;

    internal static ConfigEntry<string> MusicAccent;
    internal static ConfigEntry<int> MusicEqPreset;
    internal static ConfigEntry<KeyCode> MusicPrevKey;
    internal static ConfigEntry<KeyCode> MusicNextKey;
    internal static ConfigEntry<KeyCode> MusicPlayPauseKey;
    internal static ConfigEntry<KeyCode> MusicStopKey;
    internal static ConfigEntry<KeyCode> MusicVolumeUpKey;
    internal static ConfigEntry<KeyCode> MusicVolumeDownKey;
    internal static ConfigEntry<KeyCode> MusicToggleKey;

    internal static ConfigEntry<bool> LobbyTheme;
    internal static ConfigEntry<bool> LobbyAnims;

    internal static ConfigEntry<bool> NoWinConditions;
    internal static ConfigEntry<bool> HideAndSeekTwoSeekers;
    internal static ConfigEntry<bool> FourImpostors;
    internal static ConfigEntry<bool> LooseHostOptions;
    internal static ConfigEntry<bool> ForceMinValues;
    internal static ConfigEntry<bool> CopyCodeOnDisconnect;

    internal static ConfigEntry<string> Language;

    internal static ConfigEntry<string> BodyMode;
    internal static ConfigEntry<bool> MuteLobbyMusic;
    internal static ConfigEntry<bool> SpoofPlatformEnabled;
    internal static ConfigEntry<int> SpoofPlatformIndex;
    internal static ConfigEntry<bool> SpoofLevelEnabled;
    internal static ConfigEntry<int> SpoofLevelValue;

    internal static void Bind(ConfigFile config)
    {
        MenuKey = config.Bind("Keys", "MenuKey", KeyCode.Insert, "Open/close the menu.");
        CopyCodeKey = config.Bind("Keys", "CopyCodeKey", KeyCode.F6, "Copy the lobby code.");
        EndMatchKey = config.Bind("Keys", "EndMatchKey", KeyCode.End, "End the match (host).");
        CloseVotingKey = config.Bind("Keys", "CloseVotingKey", KeyCode.None, "Force-close voting, tally cast votes (host).");
        CloseMeetingKey = config.Bind("Keys", "CloseMeetingKey", KeyCode.None, "Force-close the meeting with no ejection (host).");
        FpsLock30 = config.Bind("QoL", "FpsLock30", false, "Lock FPS to 30.");
        FpsCap = config.Bind("QoL", "FpsCap", 60, "FPS cap when not locked to 30 (300 = unlimited).");
        FpsCap.Value = Mathf.Clamp(FpsCap.Value, 30, 300);

        ShowFps = config.Bind("QoL", "ShowFps", true, "FPS counter.");
        ShowLobbyTimer = config.Bind("QoL", "ShowLobbyTimer", true, "Lobby lifetime timer.");
        Toasts = config.Bind("QoL", "Toasts", true, "Pop-up notifications.");
        LobbyBrand = config.Bind("Lobby", "Brand", true, "Onyx watermark in the lobby.");
        LobbyBar = config.Bind("Lobby", "LobbyBar", true, "Custom lobby bar (host avatar, START button, FPS/ping/time chips).");

        HudScale = config.Bind("QoL", "HudScale", 1f, "HUD scale.");

        FreeCosmetics = config.Bind("Cosmetics", "UnlockCosmetics", true, "Mark cosmetics and bundles as owned on this client.");
        HideCosmeticsInMatch = config.Bind("Cosmetics", "HideCosmeticsInMatch", false, "Hide cosmetics on this client during a match.");
        AllowDuplicateColors = config.Bind("Cosmetics", "AllowDuplicateColors", false, "Host: let several players share one color.");
        FavoriteOutfits = new ConfigEntry<string>[4];
        for (int i = 0; i < FavoriteOutfits.Length; i++)
            FavoriteOutfits[i] = config.Bind("Cosmetics", $"FavoriteOutfit{i + 1}", "", $"Saved outfit, slot {i + 1}.");

        RemoveGuestLimits = config.Bind("Account", "RemoveGuestLimits", false, "Give guests extra client-side account features.");
        RemoveMinorLimits = config.Bind("Account", "RemoveMinorLimits", false, "Bypass client-side age limits and waiting.");
        ClearDisconnectPenalty = config.Bind("Account", "ClearDisconnectPenalty", true, "Keep the local disconnect penalty from growing.");

        BlockTelemetry = config.Bind("Privacy", "BlockTelemetry", true, "Disable analytics, crash reports and performance reporting.");

        AlwaysUnlockStartButton = config.Bind("Lobby", "AlwaysUnlockStartButton", true, "Keep the Start button active for the host (MinPlayers=1).");
        QuickStartOnEnter = config.Bind("Lobby", "QuickStartOnEnter", true, "Enter in the lobby triggers Start when chat is not focused.");
        InstantStartOnEnter = config.Bind("Lobby", "InstantStartOnEnter", true, "Skip the countdown when starting with Enter.");
        AutoReturnLobbyAfterMatch = config.Bind("Lobby", "AutoReturnLobbyAfterMatch", true, "Auto-return to the lobby from post-match screens.");
        UnlockMatchKickBan = config.Bind("Advanced", "UnlockMatchKickBan", true, "Host: kick/ban buttons available during a match.");
        RichLobbyRows = config.Bind("Lobby", "RichLobbyRows", true, "Rich lobby browser: up to 24 rows, host/code/platform/age.");
        JoinDetect = config.Bind("Players", "JoinDetect", true, "Join detect: platform, level and raw platform name.");

        AccessBanEnabled = config.Bind("Guard", "AccessBanEnabled", true, "Kick/ban players from the ban list on join.");
        AccessWhitelistOnly = config.Bind("Guard", "AccessWhitelistOnly", false, "Kick everyone not in the whitelist.");
        BanList = config.Bind("Guard", "BanList", "", "Ban list (FriendCode, comma-separated).");
        WhiteList = config.Bind("Guard", "WhiteList", "", "Whitelist (FriendCode, comma-separated).");
        MinLevelEnabled = config.Bind("Guard", "MinLevelEnabled", false, "React to players below the minimum level.");
        MinLevel = config.Bind("Guard", "MinLevel", 1, "Minimum level.");
        MinLevel.Value = Mathf.Clamp(MinLevel.Value, 1, 500);
        MinLevelAction = config.Bind("Guard", "MinLevelAction", "Kick", "Action on low level: Kick, Ban.");
        MaxLevelEnabled = config.Bind("Guard", "MaxLevelEnabled", false, "React to players above the maximum level.");
        MaxLevel = config.Bind("Guard", "MaxLevel", 500, "Maximum level.");
        MaxLevel.Value = Mathf.Clamp(MaxLevel.Value, 1, 999);
        MaxLevelAction = config.Bind("Guard", "MaxLevelAction", "Kick", "Action on high level: Kick, Ban.");
        VoteKickProtect = config.Bind("Guard", "VoteKickProtect", true, "Block vote-kicks in the lobby.");
        VoteKickAction = config.Bind("Guard", "VoteKickAction", "Kick", "Action on the voter: Null, Warn, Kick, Ban.");
        KickFortegreen = config.Bind("Guard", "KickFortegreen", true, "Host: kick players with the Fortegreen color.");
        AccessNickBanEnabled = config.Bind("Guard", "AccessNickBanEnabled", false, "Kick/ban players from the nick ban list by name.");
        NickBanList = config.Bind("Guard", "NickBanList", "", "Nick ban list (nicks separated by ;).");
        NameHistory = config.Bind("Guard", "NameHistory", true, "Remember nicks by FriendCode and notify on changes.");
        ColorReservationsEnabled = config.Bind("Guard", "ColorReservationsEnabled", false, "Host: auto-apply a reserved color to a player by FriendCode on join.");

        VisualFreeCamera = config.Bind("Visual", "FreeCamera", false, "Free WASD camera in the lobby.");
        VisualFreeCameraSpeed = config.Bind("Visual", "FreeCameraSpeed", 11, "Free camera speed.");
        VisualFreeCameraSpeed.Value = Mathf.Clamp(VisualFreeCameraSpeed.Value, 4, 30);
        VisualCameraZoom = config.Bind("Visual", "CameraZoom", false, "Camera zoom with the mouse wheel (everywhere).");
        VisualCameraMaxZoom = config.Bind("Visual", "CameraMaxZoom", 18, "Max orthographic size when zooming.");
        VisualCameraMaxZoom.Value = Mathf.Clamp(VisualCameraMaxZoom.Value, 4, 18);
        VisualNoClip = config.Bind("Visual", "NoClip", false, "No-clip: disable the local player collider (lobby and match).");
        HideModStamp = config.Bind("Visual", "HideModStamp", false, "Client-side: hide the yellow MOD badge (stealth).");
        SkipShhh = config.Bind("Visual", "SkipShhh", false, "Skip the 'Shhh!' intro screen.");
        SkipRoleIntro = config.Bind("Visual", "SkipRoleIntro", false, "Skip the role reveal cutscene.");
        SkipKillAnim = config.Bind("Visual", "SkipKillAnim", false, "Skip the kill animation overlay.");
        ChatCmdXmas = config.Bind("Chat", "ChatCmdXmas", true, "Host: let players toggle rainbow colors with /xmas in chat.");
        RevealRoles = config.Bind("Visual", "RevealRoles", false, "Show each player's role above their name (by color).");
        VisualPlayerInfoNames = config.Bind("Visual", "PlayerInfoNames", false, "Level/platform/host above names in the lobby.");
        VisualAlwaysShowChat = config.Bind("Visual", "AlwaysShowChat", true, "Keep chat always visible.");
        Tracers = config.Bind("Visual", "Tracers", false, "Lines to other players in a match.");
        TracerBodies = config.Bind("Visual", "TracerBodies", false, "Tracers to bodies too (yellow).");
        KillTimers = config.Bind("Visual", "KillTimers", false, "Kill cooldown above killer players.");
        RevealVotes = config.Bind("Visual", "RevealVotes", false, "Show meeting votes before the reveal.");
        MouseTeleport = config.Bind("Player", "MouseTeleport", false, "Teleport the local player with right-click.");
        MouseSelect = config.Bind("Player", "MouseSelect", false, "Select a player with the mouse (LMB) + wheel resize.");
        GhostAfterStart = config.Bind("Player", "GhostAfterStart", false, "Become a ghost locally after the match starts.");
        FakeMapId = config.Bind("Lobby", "FakeMapId", 5, "Map for the lobby fake-map (0-5).");
        FakeMapId.Value = Mathf.Clamp(FakeMapId.Value, 0, 5);

        NameColor = config.Bind("Player", "NameColor", false, "Colored name (local only, your own player).");
        NameColorStyle = config.Bind("Player", "NameColorStyle", 4, "Colored-name preset index.");
        NameColorStyle.Value = Mathf.Clamp(NameColorStyle.Value, 0, 15);
        NameColorAnimated = config.Bind("Player", "NameColorAnimated", false, "Animate the colored name.");
        ChatLog = config.Bind("Chat", "ChatLog", false, "Write chat to Onyx/ChatLog.txt.");
        BanWords = config.Bind("Chat", "BanWords", false, "Censor banned words (Onyx/BanWords.txt).");

        LobbyCloneMode = config.Bind("Lobby", "LobbyCloneMode", false, "Clone mode: LMB spawns at cursor, RMB removes, drag with mouse.");
        LobbyCloneShadow = config.Bind("Lobby", "LobbyCloneShadow", false, "Shadow clone trailing behind the player.");
        LobbyCloneGuard = config.Bind("Lobby", "LobbyCloneGuard", true, "Clones orbit around the player.");
        LobbyCloneGuardRadius = config.Bind("Lobby", "LobbyCloneGuardRadius", 2.5f, "Guard circle radius in world units.");
        LobbyCloneGuardRadius.Value = Mathf.Clamp(LobbyCloneGuardRadius.Value, 1f, 8f);
        LobbyCloneDrift = config.Bind("Lobby", "LobbyCloneDrift", false, "Clones wander around.");
        LobbyCloneMax = config.Bind("Lobby", "LobbyCloneMax", 50, "Max number of clones.");
        LobbyCloneMax.Value = Mathf.Clamp(LobbyCloneMax.Value, 1, 100);
        LobbyCloneSpawnCount = config.Bind("Lobby", "LobbyCloneSpawnCount", 1, "Clones spawned per left-click (1-20).");
        LobbyCloneSpawnCount.Value = Mathf.Clamp(LobbyCloneSpawnCount.Value, 1, 20);
        LobbyCloneColorId = config.Bind("Lobby", "LobbyCloneColorId", -1, "Clone color override (-1 = source, 0-17 = color index).");
        LobbyCloneColorId.Value = Mathf.Clamp(LobbyCloneColorId.Value, -1, 17);
        LobbyCloneScale = config.Bind("Lobby", "LobbyCloneScale", 1f, "Clone scale.");
        CloneFormation = config.Bind("Lobby", "CloneFormation", 4, "Clone formation (0-8).");
        CloneFormation.Value = Mathf.Clamp(CloneFormation.Value, 0, 8);
        LobbyCloneFormationCopies = config.Bind("Lobby", "LobbyCloneFormationCopies", 1, "Concentric copies when building a formation (1-5).");
        LobbyCloneFormationCopies.Value = Mathf.Clamp(LobbyCloneFormationCopies.Value, 1, 5);

        BetterChat = config.Bind("Chat", "BetterChat", true, "Better chat: richer input, copy/paste, bubble animation.");
        ChatHistorySize = config.Bind("Chat", "ChatHistorySize", 20, "How many messages to keep visible.");
        ChatHistorySize.Value = Mathf.Clamp(ChatHistorySize.Value, 5, 100);
        DarkChatTheme = config.Bind("Chat", "DarkChatTheme", true, "Dark chat theme matching the mod colors.");
        ChatBubbleSenderInfo = config.Bind("Chat", "ChatBubbleSenderInfo", true, "Level/platform next to the name in chat bubbles.");
        UnlimitedChatLength = config.Bind("Advanced", "UnlimitedChatLength", false, "Unsafe: remove the local message length limit.");
        SkipChatCooldown = config.Bind("Advanced", "SkipChatCooldown", false, "Unsafe: remove the delay between messages.");
        GhostChat = config.Bind("Chat", "GhostChat", false, "See dead chat while still alive.");

        AutoHostEnabled = config.Bind("AutoHost", "Enabled", false, "Host: auto-start matches in the lobby.");
        AutoHostMinPlayers = config.Bind("AutoHost", "MinPlayers", 4, "Minimum players before the start countdown.");
        AutoHostStartDelaySeconds = config.Bind("AutoHost", "StartDelaySeconds", 15, "Delay after reaching the minimum players.");
        AutoHostBackoffSeconds = config.Bind("AutoHost", "BackoffSeconds", 8, "Pause after a failed start attempt.");
        AutoHostInstantStart = config.Bind("AutoHost", "InstantStart", true, "Instant start instead of the vanilla countdown.");
        AutoHostWarmupSeconds = config.Bind("AutoHost", "WarmupSeconds", 5, "Warm-up after opening the lobby before countdowns.");
        AutoHostLoadGraceSeconds = config.Bind("AutoHost", "LoadGraceSeconds", 20, "Wait for players to load (0 = forever).");
        AutoHostFastStartPlayers = config.Bind("AutoHost", "FastStartPlayers", 13, "Player count for the short delay (0 = off).");
        AutoHostFastStartDelaySeconds = config.Bind("AutoHost", "FastStartDelaySeconds", 5, "Delay for the fast start.");
        AutoHostForceAfterMinutes = config.Bind("AutoHost", "ForceAfterMinutes", 0, "Start after N lobby minutes even below the minimum (0 = off).");
        AutoHostForceMinPlayers = config.Bind("AutoHost", "ForceMinPlayers", 2, "Minimum for forced starts.");
        AutoHostCancelBelowMin = config.Bind("AutoHost", "CancelBelowMin", true, "Cancel the countdown if players drop below the minimum.");
        AutoHostWaitLoadedPlayers = config.Bind("AutoHost", "WaitLoadedPlayers", true, "Wait for players to fully load in the lobby.");
        AutoHostReturnAfterMatch = config.Bind("AutoHost", "ReturnAfterMatch", true, "Return to the lobby from post-match screens.");
        AutoHostForceLastMinute = config.Bind("AutoHost", "ForceLastMinute", true, "Start in the lobby's last minute (if >=2 players).");
        AutoHostNotifications = config.Bind("AutoHost", "Notifications", true, "Show auto-host status.");

        MusicAccent = config.Bind("Music", "Accent", "Red", "Player accent theme.");
        MusicEqPreset = config.Bind("Music", "EqPreset", 16, "Equalizer preset index.");
        MusicEqPreset.Value = Mathf.Clamp(MusicEqPreset.Value, 0, 30);
        MusicPrevKey = config.Bind("Music", "PrevKey", KeyCode.LeftBracket, "Previous track.");
        MusicNextKey = config.Bind("Music", "NextKey", KeyCode.RightBracket, "Next track.");
        MusicPlayPauseKey = config.Bind("Music", "PlayPauseKey", KeyCode.None, "Play/pause.");
        MusicStopKey = config.Bind("Music", "StopKey", KeyCode.None, "Stop.");
        MusicVolumeUpKey = config.Bind("Music", "VolumeUpKey", KeyCode.None, "Volume up.");
        MusicVolumeDownKey = config.Bind("Music", "VolumeDownKey", KeyCode.None, "Volume down.");
        MusicToggleKey = config.Bind("Music", "ToggleKey", KeyCode.M, "Open/close the player.");

        LobbyTheme = config.Bind("Lobby", "LobbyTheme", true, "Dark lobby shell theme.");
        LobbyAnims = config.Bind("Lobby", "LobbyAnims", true, "Animations: right panel slide and Start button.");

        NoWinConditions = config.Bind("Lobby", "NoWinConditions", false, "Disable win-condition checks (the game won't end by itself).");
        HideAndSeekTwoSeekers = config.Bind("Lobby", "HideAndSeekTwoSeekers", false, "Host: 2 seekers in Hide and Seek.");
        FourImpostors = config.Bind("Lobby", "FourImpostors", false, "Host: 4 impostors in normal mode (needs >=9 players).");
        LooseHostOptions = config.Bind("Lobby", "LooseHostOptions", false, "Unlock host option limits (values beyond vanilla).");
        ForceMinValues = config.Bind("Lobby", "ForceMinValues", false, "Host option step 0.1 (when limits are unlocked).");
        CopyCodeOnDisconnect = config.Bind("Lobby", "CopyCodeOnDisconnect", true, "Copy the lobby code to the clipboard on disconnect.");

        Language = config.Bind("Interface", "Language", "en", "Menu language: ru, en.");

        BodyMode = config.Bind("Lobby", "BodyMode", "Disabled", "Client-side body style: Disabled, Horse, Seeker, Long, LongHorse.");
        MuteLobbyMusic = config.Bind("Lobby", "MuteLobbyMusic", true, "Mute only the lobby music locally.");
        SpoofPlatformEnabled = config.Bind("Spoof", "SpoofPlatformEnabled", false, "Spoof your platform in PlatformSpecificData.");
        SpoofPlatformIndex = config.Bind("Spoof", "SpoofPlatformIndex", 5, "0=Epic,1=Steam,2=Mac,3=Microsoft,4=Itch,5=iOS,6=Android,7=Switch,8=Xbox,9=PS,10=Starlight");
        SpoofPlatformIndex.Value = Mathf.Clamp(SpoofPlatformIndex.Value, 0, 10);
        SpoofLevelEnabled = config.Bind("Spoof", "SpoofLevelEnabled", false, "Override the displayed level.");
        SpoofLevelValue = config.Bind("Spoof", "SpoofLevelValue", 666, "Level to display (1-9999).");
        SpoofLevelValue.Value = Mathf.Clamp(SpoofLevelValue.Value, 1, 9999);
    }
}
