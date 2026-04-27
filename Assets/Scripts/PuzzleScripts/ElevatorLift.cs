using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Managers.TimeLord;
using System.Collections.Generic;
using TMPro;
using Unity.AppUI.UI;

[Serializable]
public class LockedElevatorFloorData
{
    public bool isLocked;
    public string floorName;
    public TextMeshProUGUI buttonText;
    public string keyItemID;
    public string keyDisplayName;
}

[RequireComponent(typeof(BoxCollider))]

public class ElevatorLift : PuzzlePart, IConsoleSelectable
{
    // FixedUpdate movement reverted; coroutine will handle movement

    public static bool ElevatorMenuActive { get; private set; }

    public List<LockedElevatorFloorData> lockedFloors = new List<LockedElevatorFloorData>();

    [Header("Failsafe")]
    [SerializeField, Tooltip("Automatically recalls the lift to floor one when the player is stranded near the base of the shaft.")]
    private bool enableGroundRecallFailsafe = true;
    [SerializeField, Min(0f), Tooltip("Horizontal distance from the first-floor lift position within which the player counts as waiting for a recall.")]
    private float groundRecallRadius = 6f;
    [SerializeField, Min(0f), Tooltip("Maximum height above the first-floor lift position at which the failsafe can trigger.")]
    private float groundRecallMaxHeightOffset = 3f;
    [SerializeField, Min(0f), Tooltip("Minimum delay between automatic recall attempts.")]
    private float groundRecallCooldown = 2f;

    [SerializeField] private GameObject elevatorLift;
    [SerializeField] private CinemachineCamera elevatorCamera;
    [SerializeField] private GameObject[] elevatorUI;

    [SerializeField] private BoxCollider elevatorTriggerCollider;

    [Tooltip("Assign the desired local positions for the elevator lift to move to for each floor, in order from first to third floor")]
    [SerializeField] private Vector3[] desiredLiftPosition;
    [SerializeField] private bool lockXMovement;
    [SerializeField] private bool lockYMovement;
    [SerializeField] private bool lockZMovement;
    [SerializeField] private float liftSpeed = 1f;

    [Tooltip("Assign the corresponding floor button UI elements in the inspector, in order from first to third floor; exit button should last.")]
    [SerializeField] private GameObject[] floorButtonUI;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference backToGameplayAction;
    [SerializeField] private InputActionReference firstFloorAction;
    [SerializeField] private InputActionReference secondFloorAction;
    [SerializeField] private InputActionReference thirdFloorAction;

    [Header("SFX")]
    [SerializeField] private AudioClip elevatorSFX;
    [SerializeField] private AudioClip elevatorStopSFX;

    [Header("Rumble")]
    [SerializeField] private float rumbleDuration = 0.1f;
    [SerializeField] private float rumbleLowFrequency = 0.1f;
    [SerializeField] private float rumbleHighFrequency = 0.1f;

    public List<GameObject> enemiesOnLift = new List<GameObject>();
    private GameObject playerReference;
    private PlayerMovement cachedPlayerMovement;
    private PlayerAnimationController cachedPlayerAnimationController;
    private CharacterController cachedPlayerCharacterController;
    private InputActionMap elevatorActionMap;
    private InputAction runtimeBackToGameplayAction;
    private InputAction runtimeFirstFloorAction;
    private InputAction runtimeSecondFloorAction;
    private InputAction runtimeThirdFloorAction;
    private string gameplayInputBlockOwner;
    private int cachedCameraPriority = 9;
    private bool menuActive;
    private bool wasMenuActiveBeforePause;
    private float nextGroundRecallTime;

    private int currentFloor = 0;
    private bool isMoving = false;
    private bool triggerFollowInitialized;
    private bool triggerFollowUsesColliderCenter;
    private Vector3 triggerLocalPositionOffset;
    private Quaternion triggerLocalRotationOffset = Quaternion.identity;
    private Vector3 triggerCenterLocalOffset;

    private const string ExitMenuActionName = "ExitMenu";
    private const string FloorOneActionName = "FloorOne";
    private const string FloorTwoActionName = "FloorTwo";
    private const string FloorThreeActionName = "FloorThree";

    private void OnEnable()
    {
        PauseCoordinator.OnPaused += HandleGamePaused;
        PauseCoordinator.OnResumed += HandleGameResumed;
    }

    private void OnDisable()
    {
        PauseCoordinator.OnPaused -= HandleGamePaused;
        PauseCoordinator.OnResumed -= HandleGameResumed;

        UnsubscribeFromInputActions();

        if (menuActive)
            RestoreGameplayState();
    }

    private void HandleGamePaused()
    {
        if (menuActive)
        {
            wasMenuActiveBeforePause = true;
            HideElevatorUI();
            UnsubscribeFromInputActions();
            SetElevatorCameraActive(false);
        }
        else
        {
            wasMenuActiveBeforePause = false;
        }
    }

    private void HandleGameResumed()
    {
        if (wasMenuActiveBeforePause)
        {
            // Only restore if menuActive is still true (not closed during pause)
            RefreshLockedFloors();
            SetupElevatorUI();
            SubscribeToInputActions();
            SetElevatorCameraActive(true);
        }
        wasMenuActiveBeforePause = false;
    }



    private void Start()
    {
        // Ensure all floor buttons are initially inactive
        foreach (var button in floorButtonUI)
        {
            if (button != null)
                button.SetActive(false);
        }

        TryResolveLockCoordinates();

        CachePlayerReferences();
        InitializeTriggerFollow();
        RefreshLockedFloors();
    }

    private void RefreshLockedFloors()
    {
        if (lockedFloors == null || lockedFloors.Count == 0)
            return;

        InternalPlayerInventory inventory = InternalPlayerInventory.Instance;

        foreach (LockedElevatorFloorData data in lockedFloors)
        {
            if (data == null)
                continue;

            bool hasKey = string.IsNullOrWhiteSpace(data.keyItemID)
                || (inventory != null && inventory.HasItem(data.keyItemID));

            data.isLocked = !hasKey;

            if (data.buttonText != null)
                data.buttonText.text = data.isLocked ? "LOCKED" : data.floorName;
        }
    }

    private void InitializeTriggerFollow()
    {
        if (elevatorLift == null || elevatorTriggerCollider == null)
            return;

        Transform liftTransform = elevatorLift.transform;
        Transform triggerTransform = elevatorTriggerCollider.transform;

        // If the trigger lives on a parent/root transform of the lift, moving that transform
        // would also move the lift and can cause runaway movement. In that case, move only
        // the collider center during lift motion.
        triggerFollowUsesColliderCenter = liftTransform.IsChildOf(triggerTransform);

        Vector3 triggerCenterWorld = triggerTransform.TransformPoint(elevatorTriggerCollider.center);
        triggerCenterLocalOffset = liftTransform.InverseTransformPoint(triggerCenterWorld);

        triggerLocalPositionOffset = liftTransform.InverseTransformPoint(triggerTransform.position);
        triggerLocalRotationOffset = Quaternion.Inverse(liftTransform.rotation) * triggerTransform.rotation;
        triggerFollowInitialized = true;
    }

    private void SyncTriggerWithLift()
    {
        if (elevatorLift == null || elevatorTriggerCollider == null)
            return;

        if (!triggerFollowInitialized)
            InitializeTriggerFollow();

        Transform liftTransform = elevatorLift.transform;
        Transform triggerTransform = elevatorTriggerCollider.transform;

        if (triggerFollowUsesColliderCenter)
        {
            Vector3 targetCenterWorld = liftTransform.TransformPoint(triggerCenterLocalOffset);
            elevatorTriggerCollider.center = triggerTransform.InverseTransformPoint(targetCenterWorld);
            return;
        }

        triggerTransform.position = liftTransform.TransformPoint(triggerLocalPositionOffset);
        triggerTransform.rotation = liftTransform.rotation * triggerLocalRotationOffset;
    }

    private void TryResolveLockCoordinates()
    {
        if (desiredLiftPosition == null || desiredLiftPosition.Length == 0 || elevatorLift == null)
            return;

        for (int i = 0; i < desiredLiftPosition.Length; i++)
        {
            Vector3 pos = desiredLiftPosition[i];
            if (lockXMovement)
                pos.x = elevatorLift.transform.localPosition.x;
            if (lockYMovement)
                pos.y = elevatorLift.transform.localPosition.y;
            if (lockZMovement)
                pos.z = elevatorLift.transform.localPosition.z;

            desiredLiftPosition[i] = pos;
        }
    }

    private bool TryResolveRuntimeActions()
    {
        PlayerInput playerInput = InputReader.PlayerInput;
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogWarning("[ElevatorLift] PlayerInput or actions asset is missing.");
            return false;
        }

        elevatorActionMap = playerInput.actions.FindActionMap("ElevatorLift", throwIfNotFound: false);
        if (elevatorActionMap == null)
        {
            Debug.LogError("[ElevatorLift] Could not find action map 'ElevatorLift' in PlayerInput actions.");
            return false;
        }

        runtimeBackToGameplayAction = ResolveRuntimeAction(backToGameplayAction);
        runtimeFirstFloorAction = ResolveRuntimeAction(firstFloorAction);
        runtimeSecondFloorAction = ResolveRuntimeAction(secondFloorAction);
        runtimeThirdFloorAction = ResolveRuntimeAction(thirdFloorAction);

        return runtimeBackToGameplayAction != null
            && runtimeFirstFloorAction != null
            && runtimeSecondFloorAction != null
            && runtimeThirdFloorAction != null;
    }

    private InputAction ResolveRuntimeAction(InputActionReference actionReference)
    {
        if (elevatorActionMap == null)
            return null;

        if (actionReference != null && actionReference.action != null)
        {
            InputAction referencedAction = elevatorActionMap.FindAction(actionReference.action.name, throwIfNotFound: false);
            if (referencedAction != null)
                return referencedAction;
        }

        if (ReferenceEquals(actionReference, backToGameplayAction))
            return elevatorActionMap.FindAction(ExitMenuActionName, throwIfNotFound: false);
        if (ReferenceEquals(actionReference, firstFloorAction))
            return elevatorActionMap.FindAction(FloorOneActionName, throwIfNotFound: false);
        if (ReferenceEquals(actionReference, secondFloorAction))
            return elevatorActionMap.FindAction(FloorTwoActionName, throwIfNotFound: false);
        if (ReferenceEquals(actionReference, thirdFloorAction))
            return elevatorActionMap.FindAction(FloorThreeActionName, throwIfNotFound: false);

        return null;
    }

    private bool HasValidLiftConfiguration(int? targetFloor = null)
    {
        if (elevatorLift == null)
        {
            Debug.LogError($"[ElevatorLift] Elevator platform is missing on {name}.");
            return false;
        }

        if (desiredLiftPosition == null || desiredLiftPosition.Length == 0)
        {
            Debug.LogError($"[ElevatorLift] No floor positions are configured on {name}.");
            return false;
        }

        if (targetFloor.HasValue && (targetFloor.Value < 0 || targetFloor.Value >= desiredLiftPosition.Length))
        {
            Debug.LogError($"[ElevatorLift] Target floor {targetFloor.Value} is out of range on {name}.");
            return false;
        }

        return true;
    }

    private void SyncCurrentFloorToLiftPosition()
    {
        if (!HasValidLiftConfiguration())
            return;

        Vector3 liftLocalPosition = elevatorLift.transform.localPosition;
        float bestDistance = float.MaxValue;
        int nearestFloor = currentFloor;

        for (int i = 0; i < desiredLiftPosition.Length; i++)
        {
            float distance = Vector3.SqrMagnitude(liftLocalPosition - desiredLiftPosition[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearestFloor = i;
            }
        }

        currentFloor = nearestFloor;
    }

    private void SubscribeToInputActions()
    {
        UnsubscribeFromInputActions();

        if ((runtimeBackToGameplayAction == null
            || runtimeFirstFloorAction == null
            || runtimeSecondFloorAction == null
            || runtimeThirdFloorAction == null)
            && !TryResolveRuntimeActions())
        {
            return;
        }

        runtimeBackToGameplayAction.performed += ReturnToGameplayAction;
        runtimeFirstFloorAction.performed += MoveToFirstFloor;
        runtimeSecondFloorAction.performed += MoveToSecondFloor;
        runtimeThirdFloorAction.performed += MoveToThirdFloor;
    }

    private void UnsubscribeFromInputActions()
    {
        if (runtimeBackToGameplayAction != null)
            runtimeBackToGameplayAction.performed -= ReturnToGameplayAction;
        if (runtimeFirstFloorAction != null)
            runtimeFirstFloorAction.performed -= MoveToFirstFloor;
        if (runtimeSecondFloorAction != null)
            runtimeSecondFloorAction.performed -= MoveToSecondFloor;
        if (runtimeThirdFloorAction != null)
            runtimeThirdFloorAction.performed -= MoveToThirdFloor;
    }

    public override void StartPuzzle()
    {
        RefreshLockedFloors();
        EnterElevatorLiftMenu();
        ElevatorMenuActive = true;
    }

    public override void EndPuzzle()
    {
        ReturnToGameplay();
        RumbleManager.Instance.StopControllerRumble();
        ElevatorMenuActive = false;
    }

    public override void ConsoleInteracted()
    {
        EnterElevatorLiftMenu();
    }

    public void ConsoleInteracted(PuzzleInteraction interaction)
    {
        EnterElevatorLiftMenu();
    }

    public void EnterElevatorLiftMenu()
    {
        if (menuActive || isMoving)
            return;

        if (!HasValidLiftConfiguration())
            return;

        RemoveDontDestroyFromList();

        if (enemiesOnLift.Count > 0)
        {
            MasterObjectiveClass masterObjective = FindObjectOfType<MasterObjectiveClass>();
            if (masterObjective != null)
                masterObjective.CreateAndShowNotice(null, "elevator_load_exceeded", "Elevator Load Exceeded", "Clear enemies to lighten load", priority: 10);
            return;
        }

        CachePlayerReferences();

        SubscribeToInputActions();

        if (elevatorActionMap == null && !TryResolveRuntimeActions())
        {
            Debug.LogError("[ElevatorLift] Elevator controls could not be initialized.");
            return;
        }

        SwapActionMaps("ElevatorLift");

        elevatorActionMap?.Enable();

        if (string.IsNullOrEmpty(gameplayInputBlockOwner))
            gameplayInputBlockOwner = InputReader.RequestGameplayInputBlock(nameof(ElevatorLift));

        InputReader.inputBusy = true;
        menuActive = true;
        DisablePlayerMovement();
        PauseManager.Instance?.SetGameplayHUDVisible(false);
        DisableInteractUIDuringMenu();
        SetElevatorCameraActive(true);
        RefreshLockedFloors();
        SetupElevatorUI();
    
        ManageElevatorButtons(currentFloor);
    }

    private void CachePlayerReferences()
    {
        if (playerReference == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                playerReference = taggedPlayer.transform.root.gameObject;
        }

        if (playerReference == null)
            return;

        cachedPlayerMovement = FindPlayerMovement(playerReference);
        cachedPlayerAnimationController = FindPlayerAnimationController(playerReference);
        cachedPlayerCharacterController = playerReference.GetComponent<CharacterController>();

        if (cachedPlayerCharacterController == null && cachedPlayerMovement != null)
            cachedPlayerCharacterController = cachedPlayerMovement.GetComponent<CharacterController>();
    }

    private PlayerMovement FindPlayerMovement(GameObject player)
    {
        if (player == null)
            return null;

        var playerMovement = player.GetComponent<PlayerMovement>();
        if (playerMovement != null)
            return playerMovement;

        playerMovement = player.GetComponentInChildren<PlayerMovement>(true);
        if (playerMovement != null)
            return playerMovement;

        playerMovement = player.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
            return playerMovement;

        return FindFirstObjectByType<PlayerMovement>();
    }

    private PlayerAnimationController FindPlayerAnimationController(GameObject player)
    {
        if (player == null)
            return null;

        var animationController = player.GetComponent<PlayerAnimationController>();
        if (animationController != null)
            return animationController;

        animationController = player.GetComponentInChildren<PlayerAnimationController>(true);
        if (animationController != null)
            return animationController;

        animationController = player.GetComponentInParent<PlayerAnimationController>();
        if (animationController != null)
            return animationController;

        return FindFirstObjectByType<PlayerAnimationController>();
    }

    private void DisablePlayerMovement()
    {
        if (cachedPlayerMovement != null)
        {
            cachedPlayerMovement.SuppressLocomotionAnimations(true);
            cachedPlayerMovement.ForceLocomotionRefresh();
            cachedPlayerMovement.enabled = false;
        }

        cachedPlayerAnimationController?.PlayIdle();
    }

    private void RestorePlayerMovement()
    {
        if (cachedPlayerMovement == null && playerReference != null)
            cachedPlayerMovement = FindPlayerMovement(playerReference);

        if (cachedPlayerMovement != null)
        {
            cachedPlayerMovement.enabled = true;
            cachedPlayerMovement.SuppressLocomotionAnimations(false);
            cachedPlayerMovement.ForceLocomotionRefresh();
        }

        if (cachedPlayerCharacterController == null && cachedPlayerMovement != null)
            cachedPlayerCharacterController = cachedPlayerMovement.GetComponent<CharacterController>();

        if (cachedPlayerCharacterController != null && !cachedPlayerCharacterController.enabled)
            cachedPlayerCharacterController.enabled = true;

        cachedPlayerAnimationController?.PlayIdle();
    }

    private void DisableInteractUIDuringMenu()
    {
        var ui = FindFirstObjectByType<InteractionUI>(FindObjectsInactive.Include);
        if (ui == null)
            return;

        if (ui._interactIcon != null)
            ui._interactIcon.gameObject.SetActive(false);

        if (ui._interactText != null)
            ui._interactText.gameObject.SetActive(false);
    }

    private void SetupElevatorUI()
    {
        if (elevatorUI == null || elevatorUI.Length == 0)
            return;

        

        HideElevatorUI();
        ElevatorMenuActive = true;

        string scheme = InputReader.activeControlScheme;
        if (string.IsNullOrEmpty(scheme) && InputReader.PlayerInput != null)
            scheme = InputReader.PlayerInput.currentControlScheme;

        if (string.Equals(scheme, "Gamepad", StringComparison.OrdinalIgnoreCase) && elevatorUI.Length > 1 && elevatorUI[1] != null)
        {
            elevatorUI[1].SetActive(true);
            return;
        }

        if (elevatorUI[0] != null)
            elevatorUI[0].SetActive(true);
    }

    private void HideElevatorUI()
    {
        if (elevatorUI == null)
            return;

        foreach (var uiObject in elevatorUI)
        {
            if (uiObject != null)
                uiObject.SetActive(false);
        }
    }

    private void SetElevatorCameraActive(bool active)
    {
        if (elevatorCamera == null)
            return;

        if (active)
        {
            cachedCameraPriority = elevatorCamera.Priority;
            elevatorCamera.Priority = 21;
        }
        else
        {
            elevatorCamera.Priority = cachedCameraPriority;
        }
    }

    private void ManageElevatorButtons(int currentFloor)
    {
        if (floorButtonUI == null || floorButtonUI.Length == 0)
            return;

        currentFloor = Mathf.Clamp(currentFloor, 0, floorButtonUI.Length - 1);

        foreach (var button in floorButtonUI)
        {
            if (button != floorButtonUI[currentFloor] && button != null)
                button.SetActive(true);
            else
            {
                if (button != null)
                    button.SetActive(false);
            }
        }
    }


    private void ReturnToGameplayAction(InputAction.CallbackContext context)
    {
        ReturnToGameplay();
    }

    private void ReturnToGameplay()
    {
        if (!menuActive && !isMoving)
            return;

        Debug.Log("Returning to gameplay from elevator menu.");
        ElevatorMenuActive = false;
        RestoreGameplayState();
    }

    private void RestoreGameplayState()
    {
        UnsubscribeFromInputActions();
        elevatorActionMap?.Disable();
        HideElevatorUI();
        SetElevatorCameraActive(false);
        RestorePlayerMovement();

        if (!string.IsNullOrEmpty(gameplayInputBlockOwner))
        {
            InputReader.ReleaseGameplayInputBlock(gameplayInputBlockOwner);
            gameplayInputBlockOwner = null;
        }

        SwapActionMaps("Gameplay");
        InputReader.inputBusy = false;
        InputReader.Instance?.SetAllActionsEnabled(true);
        PauseManager.Instance?.SetGameplayHUDVisible(true);
        InteractionUI.Instance?.HideInteractPrompt();
        menuActive = false;

        StartCoroutine(ReturnToFirstFloorAfterDelay(10f));
    }

    private void TurnOffAllButtons()
    {
        foreach (var button in floorButtonUI)
        {
            if (button != null)
                button.SetActive(false);
        }
    }

    private IEnumerator ReturnToFirstFloorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        TryTriggerGroundRecallFailsafe();
    }

    private bool IsFloorLocked(int floorIndex)
    {
        if (lockedFloors == null || floorIndex < 0 || floorIndex >= lockedFloors.Count)
            return false;

        return lockedFloors[floorIndex].isLocked;
    }

    private void NoticeLockedMessage(LockedElevatorFloorData floorData)
    {
        MasterObjectiveClass masterObjective = FindObjectOfType<MasterObjectiveClass>();
        if (masterObjective != null)
            masterObjective.CreateAndShowNotice(null, "floor_locked", "Floor Locked", $"You need {floorData.keyDisplayName} to access this floor.", priority: 10);
    }

    private void MoveToFirstFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 0 || isMoving) return;

        RefreshLockedFloors();

        if (IsFloorLocked(0))
        {
            NoticeLockedMessage(lockedFloors[0]);
            return;
        }

        StartCoroutine(MoveLift(0, carryPlayerWithLift: true));
    }

    private void MoveToSecondFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 1 || isMoving) return;

        RefreshLockedFloors();

        if (IsFloorLocked(1))
        {
            NoticeLockedMessage(lockedFloors[1]);
            return;
        }

        StartCoroutine(MoveLift(1, carryPlayerWithLift: true));
    }

    private void MoveToThirdFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 2 || isMoving) return;

        RefreshLockedFloors();

        if (IsFloorLocked(2))
        {
            NoticeLockedMessage(lockedFloors[2]);
            return;
        }

        StartCoroutine(MoveLift(2, carryPlayerWithLift: true));
    }

    private void SwapActionMaps(string actionMapName)
    {
        InputReader.PlayerInput.SwitchCurrentActionMap(actionMapName);
    }
    
    public void CallElevatorToFloorOne()
    {
        if(currentFloor == 0 || isMoving) return;
        StartCoroutine(MoveElevatorToFirstFloor());
        currentFloor = 0;
    }

    private IEnumerator MoveElevatorToFirstFloor()
    {
        Vector3 targetPosition = desiredLiftPosition[0];
        float moveSpeed = liftSpeed; // units per second

        
        AudioSource sfxSource = SoundManager.Instance != null ? SoundManager.Instance.sfxSource : null;
        if (sfxSource != null)
        {
            sfxSource.clip = elevatorSFX;
            sfxSource.Play();
        }

        while (Vector3.Distance(elevatorLift.transform.localPosition, targetPosition) > 0.001f)
        {
            elevatorLift.transform.localPosition = Vector3.MoveTowards(
                elevatorLift.transform.localPosition, targetPosition, moveSpeed * Time.deltaTime);

            SyncTriggerWithLift();

            yield return null;
        }

        SyncTriggerWithLift();

        if (sfxSource != null)
        {
            sfxSource.Stop();
            if (elevatorStopSFX != null)
                sfxSource.PlayOneShot(elevatorStopSFX);
        }

    }


    private void TryTriggerGroundRecallFailsafe()
    {
        if (!enableGroundRecallFailsafe || menuActive || isMoving || currentFloor == 0)
            return;

        if (Time.time < nextGroundRecallTime)
            return;

        CachePlayerReferences();
        if (playerReference == null || elevatorLift == null || desiredLiftPosition == null || desiredLiftPosition.Length == 0)
            return;

        Vector3 floorOneWorldPosition = GetFloorWorldPosition(0);
        Vector3 playerPosition = playerReference.transform.position;

        Vector2 planarOffset = new Vector2(
            playerPosition.x - floorOneWorldPosition.x,
            playerPosition.z - floorOneWorldPosition.z);

        if (planarOffset.sqrMagnitude > groundRecallRadius * groundRecallRadius)
            return;

        if (playerPosition.y > floorOneWorldPosition.y + groundRecallMaxHeightOffset)
            return;

        nextGroundRecallTime = Time.time + groundRecallCooldown;
        StartCoroutine(MoveLift(0, carryPlayerWithLift: false, restoreGameplayState: false));
    }

    private Vector3 GetFloorWorldPosition(int floorIndex)
    {
        Vector3 localFloorPosition = desiredLiftPosition[floorIndex];
        Transform liftParent = elevatorLift != null ? elevatorLift.transform.parent : null;
        return liftParent != null ? liftParent.TransformPoint(localFloorPosition) : localFloorPosition;
    }

    private CharacterController ReturnPlayerCC()
    {
        if (playerReference == null)
            CachePlayerReferences();

        CharacterController playerCC = cachedPlayerCharacterController;
        if (playerCC == null && playerReference != null)
            playerCC = playerReference.GetComponent<CharacterController>();

        if (playerCC == null)
        {
            Debug.LogWarning("Player CharacterController not found. Make sure the player has a CharacterController component.");
        }

        cachedPlayerCharacterController = playerCC;
        return playerCC;
    }


    private IEnumerator MoveLift(int targetFloor, bool carryPlayerWithLift, bool restoreGameplayState = true)
    {
        if (!HasValidLiftConfiguration(targetFloor))
        {
            if (restoreGameplayState)
                RestoreGameplayState();
            else
                menuActive = false;
            yield break;
        }

        TurnOffAllButtons();

        AudioSource sfxSource = SoundManager.Instance != null ? SoundManager.Instance.sfxSource : null;
        if (sfxSource != null)
        {
            sfxSource.clip = elevatorSFX;
            sfxSource.Play();
        }

        isMoving = true;

        Vector3 targetPosition = desiredLiftPosition[targetFloor];
        float moveSpeed = liftSpeed; // units per second
        CharacterController playerCC = carryPlayerWithLift ? ReturnPlayerCC() : null;
        if (playerCC != null)
            playerCC.enabled = false; // Disable CharacterController to prevent physics issues during movement

        Vector3 previousLiftWorldPosition = elevatorLift.transform.position;

        while (Vector3.Distance(elevatorLift.transform.localPosition, targetPosition) > 0.001f)
        {
            RumbleManager.Instance.RumblePulse(rumbleDuration, rumbleLowFrequency, rumbleHighFrequency); // Subtle rumble while moving elevator
            elevatorLift.transform.localPosition = Vector3.MoveTowards(
                elevatorLift.transform.localPosition, targetPosition, moveSpeed * Time.deltaTime);

            SyncTriggerWithLift();

            Vector3 worldDelta = elevatorLift.transform.position - previousLiftWorldPosition;
            if (carryPlayerWithLift && playerReference != null && worldDelta.sqrMagnitude > 0.000001f)
                playerReference.transform.position += worldDelta;

            previousLiftWorldPosition = elevatorLift.transform.position;
            yield return null;
        }

        elevatorLift.transform.localPosition = targetPosition;
    SyncTriggerWithLift();

        Vector3 finalWorldDelta = elevatorLift.transform.position - previousLiftWorldPosition;
        if (carryPlayerWithLift && playerReference != null && finalWorldDelta.sqrMagnitude > 0.000001f)
            playerReference.transform.position += finalWorldDelta;

        isMoving = false;
        if (sfxSource != null)
        {
            sfxSource.Stop();
            if (elevatorStopSFX != null)
                sfxSource.PlayOneShot(elevatorStopSFX);
        }
        currentFloor = targetFloor;
        if (playerCC != null)
            playerCC.enabled = true; // Re-enable CharacterController after movement
        if (restoreGameplayState)
            ReturnToGameplay();
        else
            menuActive = false;
    }

    private void RemoveDontDestroyFromList()
    {
        // Collect all inactive enemies first
        List<GameObject> toRemove = new List<GameObject>();
        foreach (var enemy in enemiesOnLift)
        {
            if (enemy != null && !enemy.activeInHierarchy)
            {
                Debug.Log($"Removing inactive enemy from elevator list: {enemy.name}");
                toRemove.Add(enemy);
            }
        }
        // Remove them after the loop
        foreach (var enemy in toRemove)
        {
            enemiesOnLift.Remove(enemy);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Collider entered elevator trigger: {other.name}");
        if (other == null) return;
        // Track the actual enemy GameObject (the collider's GameObject) if it or its root is tagged
        if (other.CompareTag("Enemy") || other.transform.root.CompareTag("Enemy") && other.gameObject.scene.name != null)
        {
            GameObject enemyObj = other.gameObject;
            Debug.Log($"Enemy entered elevator trigger: {enemyObj.name}");
            if (!enemiesOnLift.Contains(enemyObj))
                enemiesOnLift.Add(enemyObj);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Enemy") || other.transform.root.CompareTag("Enemy"))
        {
            GameObject enemyObj = other.gameObject;
            enemiesOnLift.Remove(enemyObj);
            
        }
    }
}
