using UnityEngine;
using System;

/// <summary>
/// Static class for managing the player.
/// </summary>
public static class Player
{
    public static bool IsActive { get; private set; } = false;
    internal static void SetActive(bool active) => IsActive = active;

    public static event Action RespawnPlayer;
    public static void TriggerRespawn() => RespawnPlayer?.Invoke();
}
