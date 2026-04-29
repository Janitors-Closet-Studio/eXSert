/*
    Written by Brandon Wahl

    This script manages the internal inventory of the player, keeping track of collected interactable items.
    This will be called when the player collects an item.
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using Singletons;
public class InternalPlayerInventory : Singleton<InternalPlayerInventory>
{
    internal List<string> collectedInteractables = new List<string>();
    private const string DefaultCollectibleId = "null";

    protected override void Awake()
    {
        AddCollectible(DefaultCollectibleId); // Adding a default collected item keeps legacy checks stable.

        base.Awake();
    }



    public void AddCollectible(string collectibleId)
    {
        string normalizedId = NormalizeCollectibleId(collectibleId);
        if (string.IsNullOrEmpty(normalizedId))
            return;

        if (!collectedInteractables.Contains(normalizedId))
        {
            collectedInteractables.Add(normalizedId);
            Debug.Log($"[InternalPlayerInventory] Added collectible: {normalizedId}. Total collected: {collectedInteractables.Count}");
        }
        else
        {
            Debug.LogWarning($"[InternalPlayerInventory] Collectible {normalizedId} already in inventory.");
        }
    }

    /// <summary>
    /// Checks if the inventory contains a specific item.
    /// Automatically normalizes the itemID (trim and lowercase).
    /// </summary>
    public bool HasItem(string itemID)
    {
        if (string.IsNullOrEmpty(itemID)) return true;
        string normalizedID = NormalizeCollectibleId(itemID);
        return collectedInteractables.Contains(normalizedID);
    }

    /// <summary>
    /// Returns a copy of the collected items list for debugging.
    /// </summary>
    public List<string> GetCollectedItems()
    {
        return new List<string>(collectedInteractables);
    }

    public void ResetCollectedItems()
    {
        collectedInteractables.Clear();
        AddCollectible(DefaultCollectibleId);
    }

    public int RemoveTransientKeycardItems()
    {
        int removedCount = RemoveTransientKeycardEntries(collectedInteractables);
        if (removedCount > 0)
            Debug.Log($"[InternalPlayerInventory] Removed {removedCount} transient keycard item(s) from runtime inventory.");

        if (!collectedInteractables.Contains(DefaultCollectibleId))
            AddCollectible(DefaultCollectibleId);

        return removedCount;
    }

    public static int RemoveTransientKeycardEntries(List<string> collectibleIds)
    {
        if (collectibleIds == null || collectibleIds.Count == 0)
            return 0;

        return collectibleIds.RemoveAll(IsTransientKeycardId);
    }

    private static bool IsTransientKeycardId(string collectibleId)
    {
        string normalizedId = NormalizeCollectibleId(collectibleId);
        if (string.IsNullOrEmpty(normalizedId) || string.Equals(normalizedId, DefaultCollectibleId, StringComparison.Ordinal))
            return false;

        string compactId = normalizedId.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return compactId.Contains("keycard", StringComparison.Ordinal);
    }

    private static string NormalizeCollectibleId(string collectibleId)
    {
        return string.IsNullOrWhiteSpace(collectibleId)
            ? string.Empty
            : collectibleId.Trim().ToLowerInvariant();
    }
}
