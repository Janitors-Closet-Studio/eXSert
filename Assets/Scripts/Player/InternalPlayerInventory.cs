/*
    Written by Brandon Wahl

    This script manages the internal inventory of the player, keeping track of collected interactable items.
    This will be called when the player collects an item.
*/

using System.Collections.Generic;
using UnityEngine;
using Singletons;
public class InternalPlayerInventory : Singleton<InternalPlayerInventory>
{
    internal List<string> collectedInteractables = new List<string>();

    protected override void Awake()
    {
        AddCollectible("null"); // Adding "null" as a default collected item

        base.Awake();
    }

    public void AddCollectible(string collectibleId)
    {
        if (!collectedInteractables.Contains(collectibleId))
            collectedInteractables.Add(collectibleId);


    }

    /// <summary>
    /// Checks if the inventory contains a specific item.
    /// Automatically normalizes the itemID (trim and lowercase).
    /// </summary>
    public bool HasItem(string itemID)
    {
        if (string.IsNullOrEmpty(itemID)) return true;
        string normalizedID = itemID.Trim().ToLowerInvariant();
        return collectedInteractables.Contains(normalizedID);
    }
}
