using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;

namespace Onyx.Patches;

// Снятие лимитов хост-настроек (шаг 0.1) и 4 импостера в обычном режиме
internal static class OnyxHostOptions
{
    private const int NormalMode = 1;
    private const int FoolsMode = 3;
    private const int ImpostorTitle = 133;
    private const int SpeedTitle = 137;

    private static bool Loose => OnyxConfig.LooseHostOptions != null && OnyxConfig.LooseHostOptions.Value;
    private static bool MinStep => OnyxConfig.ForceMinValues != null && OnyxConfig.ForceMinValues.Value;
    private static bool FourImp => OnyxConfig.FourImpostors != null && OnyxConfig.FourImpostors.Value;

    internal static bool TryStep(NumberOption option, float dir)
    {
        if (!Loose || KeepVanilla(option)) return HarmonyControl.Continue;
        float inc = MinStep && !IsInt(option) ? 0.1f : option.Increment;
        option.Value += inc * dir;
        Refresh(option);
        return HarmonyControl.SkipOriginal;
    }

    internal static void RelaxRange(NumberOption option)
    {
        if (!Loose || KeepVanilla(option)) return;
        option.ValidRange = new FloatRange(-999f, 999f);
        RelaxData(option);
    }

    internal static bool TryImpostorCount(ref int count)
    {
        if (FourImp && !IsHnS())
        {
            // 4 импа + минимум 5 мирных, иначе импостеры сразу выигрывают
            if (CountPlayers() >= 9) { count = 4; return HarmonyControl.SkipOriginal; }
        }

        var mgr = GameOptionsManager.Instance;
        if (!Loose || mgr == null || mgr.CurrentGameOptions == null) return HarmonyControl.Continue;
        count = mgr.CurrentGameOptions.NumImpostors;
        return HarmonyControl.SkipOriginal;
    }

    internal static bool AllowSync()
    {
        if (!Loose) return HarmonyControl.Continue;
        return IsPublicLobby() ? HarmonyControl.Continue : HarmonyControl.SkipOriginal;
    }

    // скорость в классике и импостеров вне обычного режима не трогаем, остальное в обычном — расширяем
    private static bool KeepVanilla(NumberOption option)
    {
        var mgr = GameOptionsManager.Instance;
        if (mgr == null || mgr.CurrentGameOptions == null) return true;
        int mode = (int)mgr.CurrentGameOptions.GameMode;
        bool normal = mode == NormalMode || mode == FoolsMode;
        int title = (int)((OptionBehaviour)option).Title;
        if (normal && title == SpeedTitle) return true;
        if (normal) return false;
        return title == ImpostorTitle;
    }

    private static bool IsInt(NumberOption option)
    {
        try
        {
            var data = ((OptionBehaviour)option).Data;
            return data != null && ((Il2CppObjectBase)(object)data).TryCast<IntGameSetting>() != null;
        }
        catch { return false; }
    }

    private static void RelaxData(NumberOption option)
    {
        try
        {
            var data = ((OptionBehaviour)option).Data;
            if (data == null) return;
            FloatGameSetting f = ((Il2CppObjectBase)(object)data).TryCast<FloatGameSetting>();
            if (f != null) { f.ValidRange = new FloatRange(-999f, 999f); return; }
            IntGameSetting i = ((Il2CppObjectBase)(object)data).TryCast<IntGameSetting>();
            if (i != null) i.ValidRange = new IntRange(-999, 999);
        }
        catch { }
    }

    private static int CountPlayers()
    {
        int n = 0;
        try
        {
            var cur = PlayerControl.AllPlayerControls.GetEnumerator();
            while (cur.MoveNext())
            {
                PlayerControl p = cur.Current;
                if (p != null && p.Data != null && !p.Data.Disconnected) n++;
            }
        }
        catch { }
        return n;
    }

    private static bool IsHnS()
    {
        try
        {
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek()) return true;
            var mgr = GameOptionsManager.Instance;
            if (mgr == null || mgr.CurrentGameOptions == null) return false;
            int mode = (int)mgr.CurrentGameOptions.GameMode;
            return mode == 2 || mode == 4;
        }
        catch { return false; }
    }

    private static bool IsPublicLobby()
    {
        try
        {
            if (AmongUsClient.Instance == null) return false;
            InnerNetClient c = (InnerNetClient)AmongUsClient.Instance;
            return c.IsGamePublic && !c.IsGameStarted;
        }
        catch { return false; }
    }

    private static void Refresh(NumberOption option)
    {
        option.UpdateValue();
        ((OptionBehaviour)option).OnValueChanged.Invoke((OptionBehaviour)(object)option);
        option.AdjustButtonsActiveState();
    }
}

// В приватном лобби со снятыми лимитами не синкать настройки на сервер — иначе их обрежут
[HarmonyPatch(typeof(PlayerControl), "RpcSyncSettings")]
internal static class OptionSyncPatch
{
    public static bool Prefix()
    {
        try { return OnyxHostOptions.AllowSync(); }
        catch { return HarmonyControl.Continue; }
    }
}
