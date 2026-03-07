using UnityEngine;
using System.Collections;
using System.Collections.Generic; 

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
    [Tooltip("Scene 1 should be the scene the player is coming from This is used to ensure only one door exists between two scenes and to manage door state across scenes.")]
    public SceneAsset scene1;
    [Tooltip("Scene 2 should be the scene the player is going to This is used to ensure only one door exists between two scenes and to manage door state across scenes.")]
    public SceneAsset scene2;

    private ScenePair myPair => new ScenePair(scene1, scene2);

    [Header("One Way Door Settings")]
    [Tooltip("For one-way doors, track if the door has been opened once to prevent reopening")]
    private bool oneWayDoorLocked = false;
    [Tooltip("Track if the player is currently inside the door area for one-way doors")]
    [SerializeField, ReadOnly] private bool isPlayerInside = false;

    private void Awake()
    {
        // Register this door in the static dictionary
        if (scene1 != null && scene2 != null)
        {
            var pair = myPair;
            if (doorsByScenePair.TryGetValue(pair, out var existingDoor) && existingDoor != null && existingDoor != this)
            {
                // Unload the previous door
                Debug.Log($"Duplicate OneWayDoor for scenes {pair.sceneA} and {pair.sceneB} found. Destroying previous instance.");
                Destroy(existingDoor.gameObject);
            }
            doorsByScenePair[pair] = this;
        }
    }



    public override IEnumerator NotAllowReentryCoroutine()
    {
        float delayAfterExit = 1.0f; // seconds to wait before closing
        bool waitingToClose = false;
        float exitTimer = 0f;
        Debug.Log(isPlayerInside ? "Player is inside the door area." : "Player is outside the door area.");
        while(isOpened)
        {
            if (isPlayerInside && !oneWayDoorLocked)
            {
                if (!waitingToClose)
                {
                    waitingToClose = true;
                    exitTimer = 0f;
                }
                exitTimer += Time.deltaTime;
                if (exitTimer >= delayAfterExit)
                {
                    CloseDoor();
                    oneWayDoorLocked = true;
                    yield break;
                }
            }
            else
            {
                waitingToClose = false;
            }
            yield return null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered the door area.");
            isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player exited the door area.");
            isPlayerInside = false;
        }
    }
}
