using System.Collections.Generic;
using BepInEx.Configuration;

namespace Onyx;

internal sealed class QuickItem
{
    internal readonly string Id, Ru, En;
    internal readonly ConfigEntry<bool> Cfg;
    internal QuickItem(string id, string ru, string en, ConfigEntry<bool> cfg) { Id = id; Ru = ru; En = en; Cfg = cfg; }
    internal string Label => OnyxText.T(Ru, En);
}

internal static class OnyxQuick
{
    private const int MaxFavs = 10;
    private static QuickItem[] _items;

    internal static QuickItem[] Items => _items ??= Build();

    internal static QuickItem ById(string id)
    {
        foreach (QuickItem it in Items) if (it.Id == id) return it;
        return null;
    }

    internal static bool IsFav(string id) => FavIds().Contains(id);

    internal static List<string> FavIds()
    {
        var res = new List<string>();
        string s = OnyxConfig.RadialFavorites != null ? OnyxConfig.RadialFavorites.Value : "";
        if (string.IsNullOrEmpty(s)) return res;
        foreach (string part in s.Split(','))
        {
            string p = part.Trim();
            if (p.Length > 0 && ById(p) != null && !res.Contains(p)) res.Add(p);
        }
        return res;
    }

    internal static void ToggleFav(string id)
    {
        List<string> list = FavIds();
        if (list.Contains(id)) list.Remove(id);
        else if (list.Count < MaxFavs) list.Add(id);
        OnyxConfig.RadialFavorites.Value = string.Join(",", list);
    }

    private static QuickItem[] Build() => new[]
    {
        new QuickItem("god", "Бессмертие", "God Mode", OnyxConfig.GodMode),
        new QuickItem("invisible", "Невидимость", "Invisibility", OnyxConfig.Invisible),
        new QuickItem("fakescan", "Фейк-скан", "Fake scan", OnyxConfig.FakeScan),
        new QuickItem("fakecams", "Камеры заняты", "Cameras in use", OnyxConfig.FakeCams),
        new QuickItem("mirage", "Мираж", "Mirage", OnyxConfig.LagComp),
        new QuickItem("speed", "Своя скорость", "Custom speed", OnyxConfig.SpeedMod),
        new QuickItem("invert", "Инверт управления", "Invert controls", OnyxConfig.InvertControls),
        new QuickItem("noclip", "Ноклип", "No-clip", OnyxConfig.VisualNoClip),
        new QuickItem("freecam", "Свободная камера", "Free camera", OnyxConfig.VisualFreeCamera),
        new QuickItem("zoom", "Зум камеры", "Camera zoom", OnyxConfig.VisualCameraZoom),
        new QuickItem("esp", "ESP-боксы", "ESP boxes", OnyxConfig.EspBoxes),
        new QuickItem("radar", "Радар", "Radar", OnyxConfig.Radar),
        new QuickItem("tracers", "Трассеры", "Tracers", OnyxConfig.Tracers),
        new QuickItem("tracebody", "Трассеры к телам", "Body tracers", OnyxConfig.TracerBodies),
        new QuickItem("killcd", "Кулдаун киллов", "Kill cooldown ESP", OnyxConfig.KillTimers),
        new QuickItem("seevents", "Видеть венты", "See vents", OnyxConfig.SeeVents),
        new QuickItem("seeghosts", "Видеть призраков", "See ghosts", OnyxConfig.SeeGhosts),
        new QuickItem("wallhack", "Wallhack", "Wallhack", OnyxConfig.Wallhack),
        new QuickItem("roles", "Показывать роли", "Reveal roles", OnyxConfig.RevealRoles),
        new QuickItem("unmask", "Раскрыть Оборотня", "Unmask shapeshifter", OnyxConfig.UnmaskShapeshifter),
        new QuickItem("votes", "Голоса на собрании", "Meeting votes", OnyxConfig.RevealVotes),
        new QuickItem("anonvotes", "Раскрыть анонимные", "De-anon votes", OnyxConfig.RevealAnonVotes),
        new QuickItem("infonames", "Инфо над игроками", "Info over players", OnyxConfig.VisualPlayerInfoNames),
        new QuickItem("skipshhh", "Скип Shhh", "Skip Shhh", OnyxConfig.SkipShhh),
        new QuickItem("skipintro", "Скип выдачи ролей", "Skip role intro", OnyxConfig.SkipRoleIntro),
        new QuickItem("skipkill", "Скип килл-анимации", "Skip kill anim", OnyxConfig.SkipKillAnim),
        new QuickItem("ghoststart", "Призрак после старта", "Ghost after start", OnyxConfig.GhostAfterStart),
        new QuickItem("mouseteleport", "Телепорт по ПКМ", "Teleport on RMB", OnyxConfig.MouseTeleport),
        new QuickItem("mouseselect", "Выбор мышью", "Mouse select", OnyxConfig.MouseSelect),
        new QuickItem("cosmetics", "Разблок косметики", "Unlock cosmetics", OnyxConfig.FreeCosmetics),
        new QuickItem("hidecos", "Скрыть косметику в матче", "Hide cosmetics in match", OnyxConfig.HideCosmeticsInMatch),
        new QuickItem("modstamp", "Скрыть MOD-штамп", "Hide MOD stamp", OnyxConfig.HideModStamp),
        new QuickItem("namecolor", "Цветной ник", "Colored name", OnyxConfig.NameColor),
        new QuickItem("autohost", "Автохост", "Auto-host", OnyxConfig.AutoHostEnabled),
        new QuickItem("dummies", "Манекены", "Dummies", OnyxConfig.DummyEnabled),
        new QuickItem("doors", "Двери открыты", "Doors kept open", OnyxConfig.DoorKeepOpen),
        new QuickItem("sablights", "Держать свет выкл", "Keep lights off", OnyxConfig.SabAutoLights),
        new QuickItem("sabspam", "Спам саботажа", "Spam sabotage", OnyxConfig.SabSpamReactor),
        new QuickItem("nowin", "Без условий победы", "No win conditions", OnyxConfig.NoWinConditions),
        new QuickItem("spoofplatform", "Спуф платформы", "Spoof platform", OnyxConfig.SpoofPlatformEnabled),
        new QuickItem("spooflevel", "Спуф уровня", "Spoof level", OnyxConfig.SpoofLevelEnabled),
        new QuickItem("telemetry", "Блок телеметрии", "Block telemetry", OnyxConfig.BlockTelemetry),
        new QuickItem("rpcguard", "Античит RPC", "RPC anticheat", OnyxConfig.RpcGuard),
        new QuickItem("vkprotect", "Блок войткиков", "Block votekicks", OnyxConfig.VoteKickProtect),
        new QuickItem("betterchat", "Улучшенный чат", "Better chat", OnyxConfig.BetterChat),
        new QuickItem("ghostchat", "Чат мёртвых", "Ghost chat", OnyxConfig.GhostChat),
        new QuickItem("chatlog", "Лог чата", "Chat log", OnyxConfig.ChatLog),
        new QuickItem("lobbybar", "Лобби-бар", "Lobby bar", OnyxConfig.LobbyBar),
    };
}
