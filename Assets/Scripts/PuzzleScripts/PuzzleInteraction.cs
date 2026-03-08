/*
    Written by Brandon Wahl

    Specialized unlockable interaction for puzzles.
    Place this script where you want a puzzle to be interacted with and activated by the player.
    Don't forget to assign the puzzle script that implements IPuzzleInterface in the inspector!
    Remember this should be placed where you want the player to START the puzzle from; not necessarily where the puzzle itself is located.

    Editted by Will T
        - Added ButtonPressed event to allow for more flexible puzzle interactions
*/

using System;
using UnityEngine;

public class PuzzleInteraction : UnlockableInteraction
{

    private GameObject playerReference;
    private bool inProgress;

    [Header("Console Settings")]
    [Tooltip("0 = first console, 1 = second console")]
    [SerializeField] private int consoleIndex = 0;

    public event Action ButtonPressed;
    public event Action<PuzzleInteraction> ButtonPressedWithSender;

    public int ConsoleIndex => consoleIndex;

    private void Start()
    {
        FindPlayerReference();
    }

    protected override void ExecuteInteraction()
    {
        ButtonPressed?.Invoke();
        ButtonPressedWithSender?.Invoke(this);

        PlayerAnimationController playerAnimator = GetPlayerAnimator();
        if (playerAnimator != null)
        {
            playerAnimator.PlayIdle();
        }
    }

    private void FindPlayerReference()
    {
        playerReference = GameObject.FindGameObjectWithTag("Player");
        if (playerReference == null)
        {
            Debug.LogWarning("[PuzzleInteraction] Player reference not found. Make sure the player has the 'Player' tag assigned.");
        }
    }

    private PlayerAnimationController GetPlayerAnimator()
    {
        if (playerReference == null)
        {
            FindPlayerReference();
        }

        if (playerReference != null)
        {
            var animator = playerReference.GetComponentInChildren<PlayerAnimationController>();
            if (animator == null)
            {
                Debug.LogWarning("[PuzzleInteraction] Player Animator not found. Make sure the player has an Animator component in its children.");
            }
            return animator;
        }
        return null;
    }
}
