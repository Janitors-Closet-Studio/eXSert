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
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PuzzleInteraction : UnlockableInteraction
{

    private GameObject playerReference;
    private bool inProgress;

    [Header("Console Settings")]
    [Tooltip("0 = first console, 1 = second console")]
    [SerializeField] private int consoleIndex = 0;

    [Header("Debug")]
    [SerializeField] private bool verboseDebug = true;

    public event Action ButtonPressed;
    public event Action<PuzzleInteraction> ButtonPressedWithSender;

    public int ConsoleIndex => consoleIndex;

    private string DebugPrefix => $"[PuzzleInteraction:{name}]";

    private void LogVerbose(string message)
    {
        if (verboseDebug)
            Debug.Log($"{DebugPrefix} {message}");
    }

    private void Start()
    {
        LogVerbose($"Start | activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled} consoleIndex={consoleIndex}");
        FindPlayerReference();
    }

    protected override void ExecuteInteraction()
    {

        int senderSubscriberCount = ButtonPressedWithSender == null ? 0 : ButtonPressedWithSender.GetInvocationList().Length;
        int basicSubscriberCount = ButtonPressed == null ? 0 : ButtonPressed.GetInvocationList().Length;
        Debug.Log($"[PuzzleInteraction:{name}] ExecuteInteraction called | senderSubscribers={senderSubscriberCount} basicSubscribers={basicSubscriberCount}");

        if (ButtonPressed != null)
        {
            Debug.Log($"[PuzzleInteraction:{name}] Invoking ButtonPressed with {basicSubscriberCount} subscribers");
            ButtonPressed.Invoke();
        }
        if (ButtonPressedWithSender != null)
        {
            Debug.Log($"[PuzzleInteraction:{name}] Invoking ButtonPressedWithSender with {senderSubscriberCount} subscribers");
            ButtonPressedWithSender.Invoke(this);
        }
        Debug.Log($"[PuzzleInteraction:{name}] Events invoked.");

        PlayerAnimationController playerAnimator = GetPlayerAnimator();
        if (playerAnimator != null)
        {
            playerAnimator.PlayIdle();
        }
    }

    public void TriggerFromInspector()
    {
        LogVerbose("TriggerFromInspector pressed.");
        Interact();
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

#if UNITY_EDITOR
[CustomEditor(typeof(PuzzleInteraction))]
public class PuzzleInteractionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        PuzzleInteraction interaction = (PuzzleInteraction)target;
        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Trigger Puzzle Event"))
        {
            interaction.TriggerFromInspector();
        }
        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to trigger the puzzle event from Inspector.", MessageType.Info);
        }
    }
}
#endif
