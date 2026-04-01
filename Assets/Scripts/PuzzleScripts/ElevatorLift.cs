using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(BoxCollider))]
public class ElevatorLift : PuzzlePart, IConsoleSelectable
{
    // FixedUpdate movement reverted; coroutine will handle movement

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
    private float nextGroundRecallTime;



    private int currentFloor = 0;
    private bool isMoving = false;

    private void Awake()
    {
        HideElevatorUI();

        if (elevatorCamera != null)
            cachedCameraPriority = elevatorCamera.Priority;

        TryResolveRuntimeActions();

        TryResolveLockCoordinates();
    }

    private void OnDisable()
    {
        UnsubscribeFromInputActions();

        if (menuActive)
            RestoreGameplayState();
    }

    private void Start()
    {
        // Ensure all floor buttons are initially inactive
        foreach (var button in floorButtonUI)
        {
            if (button != null)
                button.SetActive(false);
        }

        CachePlayerReferences();
    }

    private void Update()
    {
        TryTriggerGroundRecallFailsafe();
    }

    private void TryResolveLockCoordinates()
    {
        if (desiredLiftPosition == null || desiredLiftPosition.Length == 0)
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
        if (actionReference == null || actionReference.action == null || elevatorActionMap == null)
            return null;

        return elevatorActionMap.FindAction(actionReference.action.name, throwIfNotFound: false);
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
        EnterElevatorLiftMenu();
    }

    public override void EndPuzzle()
    {
        ReturnToGameplay();
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

        return FindObjectOfType<PlayerMovement>();
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

        return FindObjectOfType<PlayerAnimationController>();
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
        var ui = FindObjectOfType<InteractionUI>(true);
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
    }

    private void TurnOffAllButtons()
    {
        foreach (var button in floorButtonUI)
        {
            if (button != null)
                button.SetActive(false);
        }
    }

    private void MoveToFirstFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 0 || isMoving) return;
        StartCoroutine(MoveLift(0, carryPlayerWithLift: true));
    }

    private void MoveToSecondFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 1 || isMoving) return;
        StartCoroutine(MoveLift(1, carryPlayerWithLift: true));
    }

    private void MoveToThirdFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 2 || isMoving) return;
        StartCoroutine(MoveLift(2, carryPlayerWithLift: true));
    }

    private void SwapActionMaps(string actionMapName)
    {
        InputReader.PlayerInput.SwitchCurrentActionMap(actionMapName);
    }
    
    public void CallElevatorToFloorOne()
    {
        if(currentFloor == 0 || isMoving) return;
        StartCoroutine(MoveLift(0, carryPlayerWithLift: true));
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
        StartCoroutine(MoveLift(0, carryPlayerWithLift: false));
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

    private IEnumerator MoveLift(int targetFloor, bool carryPlayerWithLift)
    {
        TurnOffAllButtons();
        isMoving = true;
        Vector3 targetPosition = desiredLiftPosition[targetFloor];
        float moveSpeed = liftSpeed; // units per second
        CharacterController playerCC = carryPlayerWithLift ? ReturnPlayerCC() : null;
        if (playerCC != null)
            playerCC.enabled = false; // Disable CharacterController to prevent physics issues during movement

        Vector3 previousLiftWorldPosition = elevatorLift.transform.position;

        while (Vector3.Distance(elevatorLift.transform.localPosition, targetPosition) > 0.001f)
        {
            elevatorLift.transform.localPosition = Vector3.MoveTowards(
                elevatorLift.transform.localPosition, targetPosition, moveSpeed * Time.deltaTime);

            Vector3 worldDelta = elevatorLift.transform.position - previousLiftWorldPosition;
            if (carryPlayerWithLift && playerReference != null && worldDelta.sqrMagnitude > 0.000001f)
                playerReference.transform.position += worldDelta;

            previousLiftWorldPosition = elevatorLift.transform.position;
            yield return null;
        }

        elevatorLift.transform.localPosition = targetPosition;

        Vector3 finalWorldDelta = elevatorLift.transform.position - previousLiftWorldPosition;
        if (carryPlayerWithLift && playerReference != null && finalWorldDelta.sqrMagnitude > 0.000001f)
            playerReference.transform.position += finalWorldDelta;

        isMoving = false;
        currentFloor = targetFloor;
        if (playerCC != null)
            playerCC.enabled = true; // Re-enable CharacterController after movement
        ReturnToGameplay();
    }

}
