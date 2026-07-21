using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Onyx.Patches;

internal static class GhostStart
{
    private static int tries = -1;
    private static float nextAt;

    private static bool On =>
        (OnyxConfig.GhostAfterStart != null && OnyxConfig.GhostAfterStart.Value) ||
        (OnyxConfig.GameMaster != null && OnyxConfig.GameMaster.Value);

    internal static void Arm()
    {
        tries = On ? 0 : -1;
        nextAt = Time.realtimeSinceStartup + 1f;
    }

    internal static void Tick()
    {
        if (tries < 0) return;
        if (!On) { tries = -1; return; }
        if (Time.realtimeSinceStartup < nextAt) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (IsDead(me)) { tries = -1; return; }

        bool ready = ShipStatus.Instance != null && LobbyBehaviour.Instance == null
            && IntroCutscene.Instance == null && MeetingHud.Instance == null && ExileController.Instance == null
            && me != null && me.Data != null && me.Data.Role != null;
        if (!ready) { Retry(); return; }

        if (Activate(me))
        {
            OnyxToast.Push("Призрак", "Режим призрака включён.", 2.5f, OnyxNotifyKind.Info);
            tries = -1;
            return;
        }
        Retry();
    }

    private static void Retry()
    {
        if (tries >= 60) { tries = -1; return; }
        tries++;
        nextAt = Time.realtimeSinceStartup + 0.5f;
    }

    private static bool Activate(PlayerControl me)
    {
        if (me == null) return false;
        if (IsDead(me)) return true;

        if (TryDie(me, DeathReason.Exile, true) || TryDie(me, DeathReason.Exile, false)
            || TryDie(me, DeathReason.Kill, true) || TryDie(me, DeathReason.Kill, false))
            return true;

        if (InvokeNoArg(me, "Exiled") || InvokeNoArg(me, "RpcExiled") || InvokeNoArg(me, "RpcExiledV2") || InvokeNoArg(me, "SetDead"))
            return true;

        if (MurderSelf(me)) return true;
        return SetDeadFlag(me);
    }

    private static bool TryDie(PlayerControl me, DeathReason reason, bool anim)
    {
        try { me.Die(reason, anim); } catch { }
        return IsDead(me);
    }

    private static bool InvokeNoArg(PlayerControl me, string name)
    {
        try
        {
            for (Type t = me.GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m == null) continue;
                m.Invoke(me, null);
                return IsDead(me);
            }
        }
        catch { }
        return false;
    }

    private static bool MurderSelf(PlayerControl me)
    {
        try
        {
            foreach (MethodInfo m in me.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "MurderPlayer") continue;
                ParameterInfo[] ps = m.GetParameters();
                object[] args = null;
                if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(typeof(PlayerControl)))
                    args = new object[] { me };
                else if (ps.Length == 2 && ps[0].ParameterType.IsAssignableFrom(typeof(PlayerControl)) && ps[1].ParameterType == typeof(bool))
                    args = new object[] { me, true };
                else if (ps.Length == 3 && ps[0].ParameterType.IsAssignableFrom(typeof(PlayerControl)) && ps[1].ParameterType == typeof(bool) && ps[2].ParameterType == typeof(bool))
                    args = new object[] { me, true, true };

                if (args == null) continue;
                m.Invoke(me, args);
                if (IsDead(me)) return true;
            }
        }
        catch { }
        return false;
    }

    private static bool SetDeadFlag(PlayerControl me)
    {
        if (me == null || me.Data == null) return false;
        object data = me.Data;
        try
        {
            for (Type t = data.GetType(); t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty("IsDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(bool))
                {
                    p.SetValue(data, true, null);
                    if (IsDead(me)) return true;
                }

                FieldInfo f = t.GetField("IsDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool))
                {
                    f.SetValue(data, true);
                    if (IsDead(me)) return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static bool IsDead(PlayerControl me)
    {
        try { return me != null && me.Data != null && me.Data.IsDead; }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class GhostStartArmPatch
{
    public static void Postfix() => GhostStart.Arm();
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class GhostStartTickPatch
{
    public static void Postfix()
    {
        try { GhostStart.Tick(); } catch { }
    }
}
