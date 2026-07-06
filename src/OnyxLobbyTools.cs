using System;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using UnityEngine;

namespace Onyx;

internal static class OnyxLobbyTools
{
    internal static string CreateLobby()
    {
        if (!IsHost()) return "Только хост может создать лобби.";
        if (LobbyBehaviour.Instance != null) return "Лобби уже есть.";

        try
        {
            GameStartManager manager = TryGetGameStartManager();
            if (manager == null || manager.LobbyPrefab == null) return "Префаб лобби не найден.";

            LobbyBehaviour lobby = UnityEngine.Object.Instantiate(manager.LobbyPrefab);
            if (lobby == null) return "Не удалось создать лобби.";

            InnerNetObject netObject = ((Il2CppObjectBase)lobby).Cast<InnerNetObject>();
            ((InnerNetClient)AmongUsClient.Instance).Spawn(netObject, -2, (SpawnFlags)0);
            return "Лобби создано заново.";
        }
        catch (Exception error)
        {
            OnyxPlugin.Logger?.LogWarning((object)$"Create lobby failed: {error}");
            return "Создание лобби не удалось.";
        }
    }

    internal static string DestroyLobby()
    {
        if (!IsHost()) return "Только хост может разрушить лобби.";

        LobbyBehaviour lobby = LobbyBehaviour.Instance;
        if (lobby == null) return "Объекта лобби сейчас нет.";

        try
        {
            InnerNetObject netObject = ((Il2CppObjectBase)lobby).Cast<InnerNetObject>();
            netObject.Despawn();
            return "Лобби разрушено.";
        }
        catch (Exception error)
        {
            OnyxPlugin.Logger?.LogWarning((object)$"Destroy lobby failed: {error}");
            return "Разрушение лобби не удалось.";
        }
    }

    private static bool IsHost()
    {
        try { return AmongUsClient.Instance != null && ((InnerNetClient)AmongUsClient.Instance).AmHost; }
        catch { return false; }
    }

    private static GameStartManager TryGetGameStartManager()
    {
        try
        {
            if (DestroyableSingleton<GameStartManager>.InstanceExists) return DestroyableSingleton<GameStartManager>.Instance;
        }
        catch { }
        try { return UnityEngine.Object.FindObjectOfType<GameStartManager>(); }
        catch { return null; }
    }
}
