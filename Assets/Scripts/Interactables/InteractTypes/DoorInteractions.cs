/*
    Written by Brandon Wahl

    Specialized unlockable interaction for doors.
    Place this script on any GameObject that will allow a certain door to open.
    It could be on a console, a button, or even the door itself.
    Make sure to assign the DoorHandler component of the door you want to interact with in the inspector.
*/
using System.Collections.Generic;
using UnityEngine;

public class DoorInteractions : UnlockableInteraction
{
    [Tooltip("Place the gameObject with the DoorHandler component here, it may be on a different object or the same object as this script.")]
    [SerializeField] private List<DoorHandler> doorHandlers;

    

    protected override void ExecuteInteraction()
    {
        foreach (DoorHandler doorHandler in doorHandlers)
        {
            if (doorHandler != null)
            {
                if (doorHandler.doorLockState == DoorHandler.DoorLockState.Locked)
                {
                    doorHandler.doorLockState = DoorHandler.DoorLockState.Unlocked;
                }
                

                doorHandler.Interact();
                doorHandler.FadeColorIntoEachother(doorHandler.lockedLightColor, doorHandler.unlockedClosedLightColor, doorHandler.lightFadeSpeed);

            }

        }
    }
}
