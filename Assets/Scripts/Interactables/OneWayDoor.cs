using UnityEngine;
using System.Collections;
using System.Collections.Generic; 
using UnityEngine.Serialization;

public class OneWayDoor : DoorHandler
{

    [System.Serializable]
    public struct ScenePair : System.IEquatable<ScenePair>
    {
        public SceneAsset sceneA;
        public SceneAsset sceneB;

        public ScenePair(SceneAsset a, SceneAsset b)
        {
            // Always store in sorted order for equality
            if (string.Compare(a?.SceneName, b?.SceneName, System.StringComparison.Ordinal) <= 0)
            {
                sceneA = a;
                sceneB = b;
            }
            else
            {
                sceneA = b;
                sceneB = a;
            }
        }

        // Equality members for using ScenePair as a dictionary key
        public bool Equals(ScenePair other)
        {
            return (sceneA == other.sceneA && sceneB == other.sceneB);
        }

        // Override Equals and GetHashCode for dictionary usage
        public override bool Equals(object obj)
        {
            return obj is ScenePair other && Equals(other);
        }

        // Generate a hash code that is order-insensitive
        public override int GetHashCode()
        {
            int hashA = sceneA ? sceneA.SceneName.GetHashCode() : 0;
            int hashB = sceneB ? sceneB.SceneName.GetHashCode() : 0;
            // Order-insensitive hash
            return hashA ^ hashB;
        }
    }

    // Static dictionary to track doors by scene pair
    public static Dictionary<ScenePair, OneWayDoor> doorsByScenePair = new Dictionary<ScenePair, OneWayDoor>();

    [Header("One Way Door Scene Pair")]
    [FormerlySerializedAs("scene1")]
    [Tooltip("The scene the player is currently in when using this doorway.")]
    public SceneAsset playerCurrentScene;

    [FormerlySerializedAs("scene2")]
    [Tooltip("The scene the player is trying to move into through this doorway.")]
    public SceneAsset playerDestinationScene;

    private ScenePair myPair => new ScenePair(playerCurrentScene, playerDestinationScene);

    [Header("One Way Door Settings")]
    [Tooltip("For one-way doors, track if the door has been opened once to prevent reopening")]
    private bool oneWayDoorLocked = false;
    [Tooltip("Track if the player is currently inside the door area for one-way doors")]
    [SerializeField, ReadOnly] private bool isPlayerInside = false;

    [Tooltip("Optional secondary trigger that closes and locks the door once the player has fully passed through.")]
    [SerializeField] private OneWayDoorCloseTrigger closeAndLockTrigger;

    [Tooltip("Optional DoorInteractions controller to use when the close trigger should close the same linked door handlers as the interaction.")]
    [SerializeField] private DoorInteractions controllingDoorInteraction;

    [SerializeField, Min(0f)]
    [Tooltip("Optional delay after crossing the close trigger before the door closes.")]
    private float closeAfterPassingTriggerDelay = 0f;

    private Coroutine closeAfterPassRoutine;

    private void Awake()
    {
        if (closeAndLockTrigger != null)
            closeAndLockTrigger.SetOwner(this);

        // Register this door in the static dictionary
        if (playerCurrentScene != null && playerDestinationScene != null)
        {
            var pair = myPair;
            if (doorsByScenePair.TryGetValue(pair, out var existingDoor) && existingDoor != null && existingDoor != this)
            {
                Debug.Log(
                    $"Duplicate OneWayDoor for scenes {pair.sceneA} and {pair.sceneB} found. Keeping existing instance '{existingDoor.name}' and destroying duplicate '{name}'."
                );
                Destroy(gameObject);
                return;
            }

            doorsByScenePair[pair] = this;
        }

    }

    private void OnDestroy()
    {
        if (closeAfterPassRoutine != null)
            StopCoroutine(closeAfterPassRoutine);

        if (playerCurrentScene == null || playerDestinationScene == null)
            return;

        var pair = myPair;
        if (doorsByScenePair.TryGetValue(pair, out var existingDoor) && existingDoor == this)
            doorsByScenePair.Remove(pair);
    }



    public override IEnumerator NotAllowReentryCoroutine()
    {
        yield break;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            Debug.Log("Player entered the door area.");
            isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            Debug.Log("Player exited the door area.");
            isPlayerInside = false;
        }
    }

    public void NotifyPassedCloseTrigger(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (!isOpened || oneWayDoorLocked)
            return;

        if (closeAfterPassRoutine != null)
            StopCoroutine(closeAfterPassRoutine);

        closeAfterPassRoutine = StartCoroutine(CloseAfterPassingTriggerCoroutine());
    }

    private IEnumerator CloseAfterPassingTriggerCoroutine()
    {
        float delay = Mathf.Max(0f, closeAfterPassingTriggerDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (isOpened && !oneWayDoorLocked)
        {
            CloseControlledDoorHandlersOrSelf();
            oneWayDoorLocked = true;
        }

        closeAfterPassRoutine = null;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        return other != null && other.transform.root.CompareTag("Player");
    }

    private void CloseControlledDoorHandlersOrSelf()
    {
        DoorInteractions doorInteraction = ResolveControllingDoorInteraction();
        if (doorInteraction != null)
        {
            doorInteraction.CloseAssignedDoors();
            return;
        }

        CloseDoor();
    }

    private DoorInteractions ResolveControllingDoorInteraction()
    {
        if (controllingDoorInteraction != null)
            return controllingDoorInteraction;

#if UNITY_2022_3_OR_NEWER
        DoorInteractions[] interactions = FindObjectsByType<DoorInteractions>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        DoorInteractions[] interactions = FindObjectsOfType<DoorInteractions>(true);
#endif

        for (int i = 0; i < interactions.Length; i++)
        {
            DoorInteractions interaction = interactions[i];
            if (interaction != null && interaction.ContainsDoorHandler(this))
            {
                controllingDoorInteraction = interaction;
                return interaction;
            }
        }

        return null;
    }
}
