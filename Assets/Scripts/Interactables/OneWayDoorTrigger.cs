using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class OneWayDoorTrigger : MonoBehaviour
{
    [Header("Door References")]
    [SerializeField] private DoorHandler targetDoor;
    [SerializeField] private DoorInteractions targetInteraction;

    [Header("Direction")]
    [Tooltip("Forward points toward the side the player is allowed to end up on, for example Crew Quarters.")]
    [SerializeField] private Transform directionReference;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";

    [Header("Behavior")]
    [SerializeField] private bool closeDoorWhenConsumed = true;
    [SerializeField] private bool disableInteractionWhenConsumed = true;
    [SerializeField] private bool consumeIfPlayerStartsOnDestinationSide = true;

    private readonly Dictionary<Transform, PlayerTriggerState> playerStates = new();

    private bool isArmed;
    private bool isConsumed;

    private sealed class PlayerTriggerState
    {
        public int OverlapCount;
        public float EntrySide;
    }

    private void Reset()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        if (directionReference == null)
            directionReference = transform;
    }

    private void Awake()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        if (directionReference == null)
            directionReference = transform;
    }

    private void Start()
    {
        if (consumeIfPlayerStartsOnDestinationSide)
            StartCoroutine(ConsumeIfPlayerStartsBeyondTrigger());
    }

    public void ArmTrigger()
    {
        if (isConsumed)
            return;

        isArmed = true;
    }

    public void DisarmTrigger()
    {
        if (isConsumed)
            return;

        isArmed = false;
        playerStates.Clear();
    }

    public void ConsumeImmediately()
    {
        ConsumeTrigger();
    }

    private IEnumerator ConsumeIfPlayerStartsBeyondTrigger()
    {
        const int maxFramesToCheck = 180;

        for (int frame = 0; frame < maxFramesToCheck; frame++)
        {
            Transform playerRoot = FindPlayerRoot();
            if (playerRoot != null)
            {
                if (IsOnDestinationSide(playerRoot.position))
                    ConsumeTrigger();

                yield break;
            }

            yield return null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsTrackedPlayer(other) || !isArmed || isConsumed)
            return;

        Transform playerRoot = other.transform.root;
        if (!playerStates.TryGetValue(playerRoot, out PlayerTriggerState playerState))
        {
            playerState = new PlayerTriggerState
            {
                EntrySide = GetSignedSide(playerRoot.position),
                OverlapCount = 0,
            };
            playerStates.Add(playerRoot, playerState);
        }

        playerState.OverlapCount++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsTrackedPlayer(other))
            return;

        Transform playerRoot = other.transform.root;
        if (!playerStates.TryGetValue(playerRoot, out PlayerTriggerState playerState))
            return;

        playerState.OverlapCount = Mathf.Max(0, playerState.OverlapCount - 1);
        if (playerState.OverlapCount > 0)
            return;

        float exitSide = GetSignedSide(playerRoot.position);
        bool crossedForward = playerState.EntrySide < 0f && exitSide > 0f;
        playerStates.Remove(playerRoot);

        if (isArmed && !isConsumed && crossedForward)
            ConsumeTrigger();
    }

    private bool IsTrackedPlayer(Collider other)
    {
        return other != null && other.transform.root.CompareTag(playerTag);
    }

    private Transform FindPlayerRoot()
    {
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (taggedPlayer != null)
            return taggedPlayer.transform.root;

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        return playerMovement != null ? playerMovement.transform.root : null;
    }

    private bool IsOnDestinationSide(Vector3 worldPosition)
    {
        return GetSignedSide(worldPosition) > 0f;
    }

    private float GetSignedSide(Vector3 worldPosition)
    {
        Transform reference = directionReference != null ? directionReference : transform;
        return Vector3.Dot(reference.forward, worldPosition - reference.position);
    }

    private void ConsumeTrigger()
    {
        if (isConsumed)
            return;

        isConsumed = true;
        isArmed = false;
        playerStates.Clear();

        if (closeDoorWhenConsumed && targetDoor != null && targetDoor.currentDoorState != DoorHandler.DoorState.Closed)
            targetDoor.CloseDoor();

        if (disableInteractionWhenConsumed && targetInteraction != null)
            targetInteraction.DisableInteraction();
    }
}