using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;

namespace Onyx;

internal static class OnyxForceRoles
{
    internal static readonly (string Ru, string En, RoleTypes Role)[] Roles =
    {
        ("Без форса", "No force", (RoleTypes)255),
        ("Мирный", "Crewmate", RoleTypes.Crewmate),
        ("Импостер", "Impostor", RoleTypes.Impostor),
        ("Учёный", "Scientist", RoleTypes.Scientist),
        ("Инженер", "Engineer", RoleTypes.Engineer),
        ("Ангел", "Guardian Angel", RoleTypes.GuardianAngel),
        ("Оборотень", "Shapeshifter", RoleTypes.Shapeshifter),
        ("Фантом", "Phantom", RoleTypes.Phantom),
        ("Следопыт", "Tracker", RoleTypes.Tracker),
        ("Паникёр", "Noisemaker", RoleTypes.Noisemaker),
        ("Детектив", "Detective", RoleTypes.Detective),
        ("Гадюка", "Viper", RoleTypes.Viper),
    };

    private static readonly Dictionary<byte, RoleTypes> _forced = new Dictionary<byte, RoleTypes>();

    internal static int Count => _forced.Count;

    internal static bool Host()
    {
        try { return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; }
        catch { return false; }
    }

    internal static string Name(int idx)
    {
        if (idx < 0 || idx >= Roles.Length) idx = 0;
        return OnyxText.T(Roles[idx].Ru, Roles[idx].En);
    }

    internal static int IndexOf(byte pid)
    {
        if (_forced.TryGetValue(pid, out RoleTypes r))
            for (int i = 1; i < Roles.Length; i++)
                if (Roles[i].Role == r) return i;
        return 0;
    }

    internal static void Set(byte pid, RoleTypes role) => _forced[pid] = role;

    internal static void Cycle(byte pid)
    {
        int next = (IndexOf(pid) + 1) % Roles.Length;
        if (next == 0) _forced.Remove(pid);
        else _forced[pid] = Roles[next].Role;
    }

    internal static void ForceNow(byte pid)
    {
        if (!Host() || LobbyBehaviour.Instance == null) return;
        PlayerControl pc = ById(pid);
        if (pc == null || pc.Data == null) return;
        RoleTypes role = _forced.TryGetValue(pid, out RoleTypes r) ? r : RoleTypes.Crewmate;
        try { pc.RpcSetRole(role, false); } catch { try { pc.RpcSetRole(role); } catch { } }
    }

    private static PlayerControl ById(byte pid)
    {
        try { foreach (PlayerControl p in PlayerControl.AllPlayerControls) if (p != null && p.PlayerId == pid) return p; } catch { }
        return null;
    }

    internal static void Clear() => _forced.Clear();

    internal static void RegisterDefault(byte pid, RoleTypes role)
    {
        if (!_forced.ContainsKey(pid)) _forced[pid] = role;
    }

    internal static void ReapplyAtStart()
    {
        if (_forced.Count == 0 || !Host()) return;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null) continue;
                if (!_forced.TryGetValue(pc.PlayerId, out RoleTypes role)) continue;
                try { pc.RpcSetRole(role, false); } catch { try { pc.RpcSetRole(role); } catch { } }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(IntroCutscene), "CoBegin")]
internal static class OnyxForceRolesStartPatch
{
    public static void Prefix() => OnyxForceRoles.ReapplyAtStart();
}

[HarmonyPatch(typeof(LobbyBehaviour), "Start")]
internal static class OnyxForceRolesResetPatch
{
    public static void Postfix() => OnyxForceRoles.Clear();
}
