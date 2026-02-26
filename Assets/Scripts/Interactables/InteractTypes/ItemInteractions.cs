/*
    Written by Brandon Wahl

    Place this script where you want an item to be interacted with and collected into the player's inventory.
*/

public class ItemInteractions : CollectableInteraction
{
    protected override void ExecuteInteraction()
    {
        InternalPlayerInventory.Instance.AddCollectible(this.interactId);
    }

}
