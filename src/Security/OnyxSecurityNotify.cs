namespace Onyx;

internal static class OnyxSecurityNotify
{
    private static bool On => OnyxConfig.SecurityNotify != null && OnyxConfig.SecurityNotify.Value;

    internal static void Fire(string ru, string en, OnyxNotifyKind kind = OnyxNotifyKind.Danger)
    {
        string msg = OnyxText.T(ru, en);
        try { OnyxEventLog.Add(msg, kind); } catch { }
        if (!On) return;
        try { OnyxToast.Push(OnyxText.T("Защита", "Guard"), msg, 3.5f, kind); } catch { }
    }
}
