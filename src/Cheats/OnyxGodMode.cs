using HarmonyLib;
using UnityEngine;

namespace Onyx;

public sealed class OnyxGodMode : MonoBehaviour
{
    private const int VentId = 50;
    private bool _last;

    internal static bool On => OnyxConfig.GodMode != null && OnyxConfig.GodMode.Value;

    public void Update()
    {
        bool on = On;
        if (on == _last) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;
        if (on && me.inVent) return;

        _last = on;
        Send(on ? 2 : 3);
    }

    internal static void Reenter()
    {
        if (!On) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead) return;
        Send(2);
    }

    private static void Send(int op)
    {
        try { VentilationSystem.Update((VentilationSystem.Operation)op, VentId); }
        catch { }
    }

    [HarmonyPatch(typeof(VentilationSystem), "Update")]
    private static class Block
    {
        private static bool Prefix(VentilationSystem.Operation __0, int __1)
        {
            if (!On || __1 == VentId) return true;
            int op = (int)__0;
            return !(op == 2 || op == 3 || op == 4);
        }
    }

    [HarmonyPatch(typeof(ShipStatus), "OnEnable")]
    private static class OnShip
    {
        private static void Postfix() => Reenter();
    }

    [HarmonyPatch(typeof(MeetingHud), "Close")]
    private static class OnClose
    {
        private static void Postfix() => Reenter();
    }
}
