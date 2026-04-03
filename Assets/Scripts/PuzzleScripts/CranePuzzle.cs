    
/*
    Written by Brandon Wahl

    The Script handles the crane puzzle in the cargo bay area. Here, the player control different parts
    of the crane with their respective movement keys; player movement is disabled while the puzzle is active.
    There is many QoL options for those working in engines. These include swapping controls and adding smoothing if wanted.

    Used CoPilot to help with custom property drawers for showing/hiding fields in the inspector and properly adding
    lerping functionality.
*/

using Unity.Cinemachine;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.InputSystem;


#if UNITY_EDITOR
using UnityEditor;
#endif


//Once the pieces are in the list, you can set which axes they move on and their min/max positions
[System.Serializable]
public class CranePart
{
    [Tooltip("GameObject to move")]
    public GameObject partObject;
    
    [Tooltip("Enable movement on X axis")]
    public bool moveX = false;
    [Tooltip("Enable movement on Y axis")]
    public bool moveY = false;
    [Tooltip("Enable movement on Z axis")]
    public bool moveZ = false;
    
    [ShowIfX]
    [Tooltip("Min X position")]
    public float minX = -5f;

    [ShowIfX]
    [Tooltip("Max X position")]
    public float maxX = 5f;
    
    [ShowIfY]
    [Tooltip("Min Y position")]
    public float minY = 0f;

    [ShowIfY]
    [Tooltip("Max Y position")]
    public float maxY = 10f;
    
    [ShowIfZ]
    [Tooltip("Min Z position")]
    public float minZ = -5f;
    
    [ShowIfZ]
    [Tooltip("Max Z position")]
    public float maxZ = 5f;

    internal float cachedMinX, cachedMaxX, cachedMinY, cachedMaxY, cachedMinZ, cachedMaxZ;

    // Cache original axis settings
    internal bool cachedMoveX, cachedMoveY, cachedMoveZ;

    public bool useWorldPosition = false; // Option to move using world position instead of local position
}

// These will be used to show/hide fields in the inspector based on which axes are enabled
public class ShowIfXAttribute : PropertyAttribute { }
public class ShowIfYAttribute : PropertyAttribute { }
public class ShowIfZAttribute : PropertyAttribute { }

public class CranePuzzle : PuzzlePart 
        
    
{
    
    // Static flag to block pause menu globally
    public static bool IsCranePuzzleActive = false;

    // Cache of the player's movement component so it can be re-enabled later
    private PlayerMovement cachedPlayerMovement;
    private PlayerAnimationController cachedPlayerAnimationController;

    #region Serializable Fields
    [Header("Input Actions")]
    [SerializeField, CriticalReference] internal InputActionReference craneMoveAction;
    [SerializeField, CriticalReference] internal InputActionReference _escapePuzzleAction;
    [SerializeField, CriticalReference] internal InputActionReference _confirmPuzzleAction;
    [SerializeField, CriticalReference] internal InputActionReference _pauseAction;

    [Space(10)]
    [Header("Camera")]
    // Cinemachine camera for the puzzle
    [SerializeField, CriticalReference] protected CinemachineCamera puzzleCamera;

    [Space(10)]

    // List of crane parts to move
    [Header("Crane Parts")]
    [SerializeField] protected List<CranePart> craneParts = new List<CranePart>();
    [SerializeField] private GameObject magnetBase;

    [Space(10)]

    // Swap input mapping so X uses W/S and Z uses A/D
    [Tooltip("Swap input mapping so X uses W/S and Z uses A/D")]
    [SerializeField] private bool swapXZControls = false;

    [Space(10)]

    [Header("Crane Settings")]
    [SerializeField] private float craneMoveSpeed = 2f;
    [Tooltip("Height to which the magnet extends")]
    [SerializeField] private GameObject[] craneUI; // UI elements to show/hide during puzzle

    [Space(10)]
    [Header("Crane Control Settings")]
    [Tooltip("Invert horizontal input (A/D) so A acts as right and D as left when enabled")]
    [SerializeField] private bool invertHorizontal = false;
    [Tooltip("Invert forward/backward input (W/S or stick Y) when enabled")]
    [SerializeField] private bool invertForwardBackward = false;
    [Tooltip("Optional override for forward/backward speed. Uses Crane Move Speed when set to 0 or less")]
    [SerializeField] private float forwardBackwardMoveSpeed = 0f;
    #endregion

    #region Light Settings
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock lightBulbPropertyBlock;

    [Header("Door Light Settings")]
    [Tooltip("Light bulb GameObject to change color")]
    public GameObject lightBulb;

    [Tooltip("Base color of the bulb material when the door is locked.")]
    [ColorUsage(false, true)]
    public Color lockedLightBulbColor = DefaultLockedBulbBaseColor;

    [Tooltip("Emission color of the bulb material when the door is locked.")]
    [ColorUsage(true, true)]
    public Color lockedLightBulbEmissionColor = DefaultLockedBulbEmissionColor;

    [Tooltip("Base color of the bulb material when the door is unlocked.")]
    [ColorUsage(false, true)]
    public Color unlockedLightBulbColor = DefaultUnlockedBulbBaseColor;

    [Tooltip("Emission color of the bulb material when the door is unlocked.")]
    [ColorUsage(true, true)]
    public Color unlockedLightBulbEmissionColor = DefaultUnlockedBulbEmissionColor;

    [Tooltip("Light component on the door to change color")]
    public Light doorLight;
    [Tooltip("Color of the light when the door is locked")]
    public Color lockedLightColor = DefaultLockedPointLightColor;
    [Tooltip("Color of the light when the door is unlocked")]
    public Color unlockedLightColor = DefaultUnlockedPointLightColor;

    [Tooltip("Speed of the light color transition")]
    public float lightFadeSpeed = 2f;

    // Coroutine handle for light fading
    private Coroutine lightFadeCoroutine;

    // Track last light state
    private bool isLightLocked = false;
    

    private static Color DefaultLockedBulbBaseColor => ColorFromHex("A10000");
    private static Color DefaultLockedBulbEmissionColor => ColorFromHsv(0f, 100f, 38f);
    private static Color DefaultLockedPointLightColor => ColorFromHex("FF1E1E");
    private static Color DefaultUnlockedBulbBaseColor => ColorFromHex("1DC814");
    private static Color DefaultUnlockedBulbEmissionColor => ColorFromHsv(145f, 100f, 13f);
    private static Color DefaultUnlockedPointLightColor => ColorFromHex("44A659");
    #endregion

    internal bool isMoving = false;
    private bool puzzleActive = false;
    internal bool isExtending = false;
    protected bool isAutomatedMovement = false;
    internal bool isRetracting;

    private InputActionMap craneMap;
    private InputAction runtimeCraneMoveAction, runtimeConfirmAction, runtimeEscapeAction;
    
    private Vector2 cachedMoveInput;
    private Coroutine moveCoroutine;

    internal readonly Dictionary<CranePart, Vector3> cranePartStartLocalPositions = new Dictionary<CranePart, Vector3>();

    private void Awake()
    {
        // keep UI hidden initially (original behavior)
        if (craneUI != null)
        {
            foreach (GameObject img in craneUI)
            {
                if (img != null)
                    img.SetActive(false);
            }
        }

        CacheCranePartStartPositions();

        if (!TryResolveRuntimeActions())
        {
            enabled = false;
            return;
        }
    }

    private bool TryResolveRuntimeActions() {
        Debug.Log("[CranePuzzle] TryResolveRuntimeActions called.");
        Debug.Log($"PlayerInput: {(InputReader.PlayerInput != null ? InputReader.PlayerInput.ToString() : "null")}");
        Debug.Log($"craneMoveAction: {(craneMoveAction != null ? craneMoveAction.ToString() : "null")}, _escapePuzzleAction: {(_escapePuzzleAction != null ? _escapePuzzleAction.ToString() : "null")}, _confirmPuzzleAction: {(_confirmPuzzleAction != null ? _confirmPuzzleAction.ToString() : "null")}");
    
        // Safely obtain a PlayerInput reference from InputReader
        PlayerInput playerInput = InputReader.PlayerInput;

        if (playerInput == null)
        {
            return false;
        }

        var actions = playerInput.actions;
        if (actions == null)
        {
            return false;
        }

        // Handle PlayerInput action asset clones
        string mapName = "CranePuzzle";
        craneMap = null;
        foreach (var map in actions.actionMaps)
        {
            if (map.name == mapName)
            {
                craneMap = map;
                break;
            }
        }
        if (craneMap == null)
        {
            Debug.LogError($"[CranePuzzle] Could not find action map '{mapName}' in PlayerInput actions (possible clone issue).");
            return false;
        }

        // Safely resolve runtime actions (only if the serialized references and their .action are valid)
        runtimeCraneMoveAction = ResolveRuntimeAction(craneMoveAction, "craneMoveAction");
        runtimeConfirmAction = ResolveRuntimeAction(_confirmPuzzleAction, "_confirmPuzzleAction");
        runtimeEscapeAction = ResolveRuntimeAction(_escapePuzzleAction, "_escapePuzzleAction");

        return true;
    }

    private InputAction ResolveRuntimeAction(InputActionReference reference, string label)
       
    {

         Debug.Log($"[CranePuzzle] Resolving action for {label}: reference {(reference != null ? reference.ToString() : "null")}, action name {(reference != null && reference.action != null ? reference.action.name : "null")}");
        if (reference != null && reference.action != null)
        {
            // Handle PlayerInput action asset clones: search by name
            InputAction resolved = null;
            foreach (var action in craneMap.actions)
            {
                if (action.name == reference.action.name)
                {
                    resolved = action;
                    break;
                }
            }
            if (resolved == null)
            {
                Debug.LogError($"[CranePuzzle] Could not resolve action '{reference.action.name}' in action map '{craneMap.name}' (possible clone issue).");
            }
            return resolved;
        }
        return null;
    }

    private void HandleGameplayMap(bool enable)
    {
        if (InputReader.PlayerInput == null)
            return;

        var gameplayMap = InputReader.PlayerInput.actions.FindActionMap("Gameplay");
        if (gameplayMap != null)
        {
            if (enable) gameplayMap.Enable();
            else gameplayMap.Disable();
        }
        else
        {
            Debug.LogError($"[CranePuzzle] Could not find action map 'Gameplay' to {(enable ? "enable" : "disable")} during puzzle setup.");
        }
    }


    private int SetupCranePuzzle()
    {
        HandleGameplayMap(false); // Disable gameplay map during puzzle
        _pauseAction.action.Enable(); // Enable the escape action so player can exit puzzle with pause menu
        Debug.Log("Pause action is enabled: " + _pauseAction.action.enabled); // Log to confirm the action is enabled

        CacheCraneBoundaries();
        CacheCranePartStartPositions();
        CacheCraneAxisSettings();

        SetupCraneUI(); // Sets up the crane's custom UI

        SwapActionMaps(true); // Switches player to crane controls


        if (runtimeCraneMoveAction == null || runtimeConfirmAction == null || runtimeEscapeAction == null)
        {
            if (!TryResolveRuntimeActions())
                return EmergencyExit("[CranePuzzle] Missing input actions. Check CranePuzzle action map and input references.");

            if (runtimeCraneMoveAction == null || runtimeConfirmAction == null || runtimeEscapeAction == null)
                return EmergencyExit("[CranePuzzle] Missing input actions. Check CranePuzzle action map and input references.");

            Debug.LogError($"{runtimeConfirmAction}, {runtimeEscapeAction}, {runtimeCraneMoveAction}"); // Log which actions are missing
        }

        runtimeCraneMoveAction.Enable();
        runtimeConfirmAction.Enable();
        runtimeEscapeAction.Enable();

        puzzleActive = true;

        // Prevent player input reads (used across movement, dash, etc.); Jump still wont deactivate idk why
        InputReader.inputBusy = true;

        // Finds the player
        var player = GameObject.FindWithTag("Player");

        if (player == null)
            return EmergencyExit("Error in trying to find player");

        // Try to find PlayerMovement on the player, its children, or parent; fallback to any active instance
        var pm = FindPlayerMovement(player);
        cachedPlayerAnimationController = FindPlayerAnimationController(player);

        // If found, disable movement and cache for restoration
        if (pm != null)
        {
            cachedPlayerMovement = pm;
            pm.SuppressLocomotionAnimations(true);
            pm.ForceLocomotionRefresh();
            pm.enabled = false;
        }

        cachedPlayerAnimationController?.PlaySingleTargetIdleCombat(0.08f);


        SwitchPuzzleCamera();

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        moveCoroutine = StartCoroutine(MoveCraneCoroutine());

        Debug.Log("Crane Puzzle Started");

        return 1; // Returns 1 which means things were set up properly

        // Emergency Exit script in case things are missing;
        // Returns -1 which means things weren't set up correctly
        int EmergencyExit(string reason)
        {
            Debug.LogError(reason);
            EndPuzzle();
            Debug.LogError($"{runtimeConfirmAction}, {runtimeEscapeAction}, {runtimeCraneMoveAction}"); // Log which actions are missing
            return -1;
        }
    }

    private void CacheCraneBoundaries()
    {
        foreach (CranePart part in craneParts)
        {
            if (part != null)
            {
                if (part.moveX)
                {
                    part.cachedMinX = part.minX;
                    part.cachedMaxX = part.maxX;
                }
                if (part.moveY)
                {
                    part.cachedMinY = part.minY;
                    part.cachedMaxY = part.maxY;
                }
                if (part.moveZ)
                {
                    part.cachedMinZ = part.minZ;
                    part.cachedMaxZ = part.maxZ;
                }
            }
        }
    }

    private void ReloadCraneBoundaries()
    {
        foreach (CranePart part in craneParts)
        {
            if (part != null)
            {
                if (part.moveX)
                {
                    part.minX = part.cachedMinX;
                    part.maxX = part.cachedMaxX;
                }
                if (part.moveY)
                {
                    part.minY = part.cachedMinY;
                    part.maxY = part.cachedMaxY;
                }
                if (part.moveZ)
                {
                    part.minZ = part.cachedMinZ;
                    part.maxZ = part.cachedMaxZ;
                }
            }
        }
    }

    private void SwitchPuzzleCamera()
    {
        // Changes camera priority to switch to puzzle camera
        if (puzzleCamera != null)
        {
            puzzleCamera.Priority = 21;
        }
    }

    private PlayerMovement FindPlayerMovement(GameObject player)
    {
        if (player == null)
            return null;

        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
            return pm;

        pm = player.GetComponentInChildren<PlayerMovement>(true);
        if (pm != null)
            return pm;

        pm = player.GetComponentInParent<PlayerMovement>();
        if (pm != null)
            return pm;

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

    protected void SetPuzzleCamera(CinemachineCamera camera)
    {
        if (camera == puzzleCamera)
            return;

        if (puzzleCamera != null)
            puzzleCamera.Priority = 9;

        puzzleCamera = camera;
    }

    #region PuzzlePart Methods
    public override void ConsoleInteracted()
    {
        if (puzzleActive)
            return;

        StartPuzzle();
    }
    // Called by whatever system starts this puzzle
    public override void StartPuzzle()
        
    {   
        if (puzzleActive)
            return;

        IsCranePuzzleActive = true;
        DisableInteractUIDuringPuzzle();
        PauseManager.Instance?.SetGameplayHUDVisible(false);

        int status = SetupCranePuzzle();

        Debug.Log("Action Map after setup: " + (InputReader.PlayerInput != null ? InputReader.PlayerInput.currentActionMap.name : "null"));
    }

    // Call this when the puzzle is finished or cancelled
    public override void EndPuzzle()
        
    {
        ReleasePuzzleControl(stopRunningCoroutines: true, clearAutomationState: true);
        HandleGameplayMap(true); // Re-enable gameplay map after puzzle
        isCompleted = false;
    }

    protected void ReleasePuzzleControl(bool stopRunningCoroutines, bool clearAutomationState)
    {
        IsCranePuzzleActive = false;

        isCompleted = true;

        ReloadCraneBoundaries();

        foreach (GameObject img in craneUI)
        {
            img.SetActive(false);
        }

        puzzleActive = false;

        if (stopRunningCoroutines)
        {
            StopAllCoroutines();
        }

        moveCoroutine = null;
        isMoving = false;

        // Unlock crane movement
        LockOrUnlockMovement(false);
        isExtending = false;
        if (clearAutomationState)
        {
            isAutomatedMovement = false;
        }

        // Disable input actions
        if (_escapePuzzleAction != null && _escapePuzzleAction.action != null)
        {
            _escapePuzzleAction.action.Disable();
        }
        if (_confirmPuzzleAction != null && _confirmPuzzleAction.action != null)
        {
            _confirmPuzzleAction.action.Disable();
        }

        if (craneMoveAction != null && craneMoveAction.action != null)
        {
            craneMoveAction.action.Disable();
        }
        if (runtimeCraneMoveAction != null)
        {
            runtimeCraneMoveAction.Disable();
            runtimeCraneMoveAction = null;
        }
        if (runtimeConfirmAction != null)
        {
            runtimeConfirmAction.Disable();
            runtimeConfirmAction = null;
        }
        if (runtimeEscapeAction != null)
        {
            runtimeEscapeAction.Disable();
            runtimeEscapeAction = null;
        }

        // Sets camera priority back to normal
        if (puzzleCamera != null)
        {
            puzzleCamera.Priority = 9;
        }

        // Re-enable player input
        InputReader.inputBusy = false;

        SwapActionMaps(false);

        if (InputReader.Instance != null && InputReader.PlayerInput != null)
        {
            var cranePuzzleMap = InputReader.PlayerInput.actions.FindActionMap("CranePuzzle");
            if (cranePuzzleMap != null)
            {
                cranePuzzleMap.Disable();
            }

            InputReader.PlayerInput.enabled = true;
            InputReader.PlayerInput.ActivateInput();
            InputReader.PlayerInput.actions.Enable();

            var gameplayMap = InputReader.PlayerInput.actions.FindActionMap("Gameplay");
            if (gameplayMap != null)
            {
                gameplayMap.Enable();
            }
        }

        RestorePlayerMovement();
        PauseManager.Instance?.SetGameplayHUDVisible(true);
        InteractionUI.Instance?.HideInteractPrompt();
    }

    #endregion

    // Read CranePuzzle move action when available (prefer runtime action from PlayerInput)
    private void ReadMoveAction()
    {
        InputAction actionToRead = runtimeCraneMoveAction != null ? runtimeCraneMoveAction : (craneMoveAction != null ? craneMoveAction.action : null);
        if (actionToRead != null)
        {
            cachedMoveInput = actionToRead.ReadValue<Vector2>();
        }
        else
        {
            cachedMoveInput = Vector2.zero;
        }
    }

    private void GetProcessedMoveInput(out float xInput, out float yInput, out float zInput)
    {
        xInput = cachedMoveInput.x;
        yInput = cachedMoveInput.y;
        zInput = cachedMoveInput.y;

        if (invertHorizontal)
        {
            xInput *= -1f;
        }

        if (invertForwardBackward)
        {
            zInput *= -1f;
        }

        if (swapXZControls)
        {
            float temp = xInput;
            xInput = zInput;
            zInput = temp;
        }
    }

    private float GetForwardBackwardMoveSpeed()
    {
        return forwardBackwardMoveSpeed > 0f ? forwardBackwardMoveSpeed : craneMoveSpeed;
    }

    private bool HasMovableYAxis()
    {
        foreach (CranePart part in craneParts)
        {
            if (part != null && part.moveY)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasMovableZAxis()
    {
        foreach (CranePart part in craneParts)
        {
            if (part != null && part.moveZ)
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerator MoveCraneCoroutine()
                           
                
    {
            

        while (puzzleActive && !isAutomatedMovement && !isExtending)
        {
            ReadMoveAction();

            // Always check the runtimeEscapeAction, not the serialized reference, for correct action state
            InputAction escapeActionToRead = runtimeEscapeAction != null ? runtimeEscapeAction : (_escapePuzzleAction != null ? _escapePuzzleAction.action : null);
            if (escapeActionToRead != null && escapeActionToRead.triggered)
            {
                if (HandleEscapeTriggered())
                {
                    yield break;
                }
            }

            CheckForConfirm();

            if (!puzzleActive || isAutomatedMovement || isExtending)
            {
                isMoving = false;
                yield return null;
                continue;
            }


            // Get processed input for each axis (after swap/invert)
            GetProcessedMoveInput(out float xInput, out float yInput, out float zInput);
            bool hasInput = Mathf.Abs(xInput) > 0.01f || Mathf.Abs(yInput) > 0.01f || Mathf.Abs(zInput) > 0.01f;

           
            // Check for blocking on each axis using the processed axes
            bool blockX = false, blockY = false, blockZ = false;
            if (magnetBase != null)
            {
                
                 // Debug: print processed input and block status
                if (hasInput)
                {
                    Debug.Log($"[CranePuzzle] xInput: {xInput}, yInput: {yInput}, zInput: {zInput} | blockX: {blockX}, blockY: {blockY}, blockZ: {blockZ} | swapXZControls: {swapXZControls}");
                }

                Collider magnetCol = magnetBase.GetComponent<Collider>();
                Vector3 origin = magnetBase.transform.position;
                Quaternion orientation = magnetBase.transform.rotation;
                Vector3 halfExtents = magnetCol != null ? magnetCol.bounds.extents : Vector3.one * 0.5f;
                // Use both Default and Obstacle layers for blocking
                int layerMask = LayerMask.GetMask("Default", "Obstacle");

                // X axis (processed)
                if (Mathf.Abs(xInput) > 0.01f)
                {
                    float tryMove = xInput * craneMoveSpeed * Time.deltaTime;
                    Vector3 dir = magnetBase.transform.right * Mathf.Sign(xInput);
                    float dist = Mathf.Abs(tryMove);
                    blockX = Physics.BoxCast(origin, halfExtents, dir, out _, orientation, dist, layerMask, QueryTriggerInteraction.Ignore);
                }
                // Y axis (processed)
                if (Mathf.Abs(yInput) > 0.01f)
                {
                    float tryMove = yInput * craneMoveSpeed * Time.deltaTime;
                    Vector3 dir = magnetBase.transform.up * Mathf.Sign(yInput);
                    float dist = Mathf.Abs(tryMove);
                    blockY = Physics.BoxCast(origin, halfExtents, dir, out _, orientation, dist, layerMask, QueryTriggerInteraction.Ignore);
                }
                // Z axis (processed)
                if (Mathf.Abs(zInput) > 0.01f)
                {
                    float tryMove = zInput * GetForwardBackwardMoveSpeed() * Time.deltaTime;
                    Vector3 dir = magnetBase.transform.forward * Mathf.Sign(zInput);
                    float dist = Mathf.Abs(tryMove);
                    RaycastHit hitInfo;
                    blockZ = Physics.BoxCast(origin, halfExtents, dir, out hitInfo, orientation, dist, layerMask, QueryTriggerInteraction.Ignore);
                    Debug.Log($"[CranePuzzle][Z BoxCast] origin: {origin}, dir: {dir}, dist: {dist}, halfExtents: {halfExtents}, blockZ: {blockZ}, hit: {(blockZ ? hitInfo.collider?.name : "none")}");
                }
            }
            else
            {
                Debug.LogWarning("[CranePuzzle] magnetBase is not assigned!");
            }


            // Light logic: locked if all pressed axes are blocked, unlocked if any pressed axis is unblocked
            // Ignore Y axis for lock logic; only X and Z
            bool anyPressed = false;
            bool allPressedBlocked = true;
            if (Mathf.Abs(xInput) > 0.01f) { anyPressed = true; if (!blockX) allPressedBlocked = false; }
            if (Mathf.Abs(zInput) > 0.01f) { anyPressed = true; if (!blockZ) allPressedBlocked = false; }
             Debug.Log($"[CranePuzzle] anyPressed: {anyPressed}, allPressedBlocked: {allPressedBlocked}, xInput: {xInput}, yInput: {yInput}, zInput: {zInput}, blockX: {blockX}, blockY: {blockY}, blockZ: {blockZ}");
            if (anyPressed && allPressedBlocked)
            {
                
                if (!isLightLocked)
                {
                    Debug.Log("[CranePuzzle] Fading light to LOCKED");
                    if (lightFadeCoroutine != null) StopCoroutine(lightFadeCoroutine);
                    lightFadeCoroutine = StartCoroutine(FadeLightBulbColor(
                        unlockedLightBulbColor, lockedLightBulbColor,
                        unlockedLightBulbEmissionColor, lockedLightBulbEmissionColor,
                        0.25f // fade quickly
                    ));
                    if (doorLight != null)
                        StartCoroutine(FadeColorIntoEachother(unlockedLightColor, lockedLightColor, 0.25f));
                    isLightLocked = true;
                }
            }
            else if (anyPressed && !allPressedBlocked)
            {
                if (isLightLocked)
                {
                    Debug.Log("[CranePuzzle] Fading light to UNLOCKED");
                    if (lightFadeCoroutine != null) StopCoroutine(lightFadeCoroutine);
                    lightFadeCoroutine = StartCoroutine(FadeLightBulbColor(
                        lockedLightBulbColor, unlockedLightBulbColor,
                        lockedLightBulbEmissionColor, unlockedLightBulbEmissionColor,
                        0.25f
                    ));
                    if (doorLight != null)
                        StartCoroutine(FadeColorIntoEachother(lockedLightColor, unlockedLightColor, 0.25f));
                    isLightLocked = false;
                }
            }

            // Only block movement for blocked axes
            float moveAmountX = (blockX ? 0f : xInput * craneMoveSpeed * Time.deltaTime);
            float moveAmountY = (blockY ? 0f : yInput * craneMoveSpeed * Time.deltaTime);
            float moveAmountZ = (blockZ ? 0f : zInput * GetForwardBackwardMoveSpeed() * Time.deltaTime);

            // Move crane parts after collision/movement logic
            for (int i = 0; i < craneParts.Count; i++)
            {
                CranePart part = craneParts[i];
                if (part == null || part.partObject == null) continue;
                Vector3 basePos = part.useWorldPosition ? part.partObject.transform.position : part.partObject.transform.localPosition;
                Vector3 next = basePos;
                if (part.moveX) next.x += moveAmountX;
                if (part.moveY) next.y += moveAmountY;
                if (part.moveZ) next.z += moveAmountZ;
                if (part.moveX) next.x = Mathf.Clamp(next.x, part.minX, part.maxX);
                if (part.moveY) next.y = Mathf.Clamp(next.y, part.minY, part.maxY);
                if (part.moveZ) next.z = Mathf.Clamp(next.z, part.minZ, part.maxZ);
                if (part.useWorldPosition)
                    part.partObject.transform.position = next;
                else
                    part.partObject.transform.localPosition = next;
            }

            isMoving = hasInput && (!blockX || !blockY || !blockZ);
            yield return null;
        }

        isMoving = false;
        moveCoroutine = null;
    }

    public virtual void CraneMovement()
    {
        GetProcessedMoveInput(out float xInput, out float yInput, out float zInput);
        bool hasInput = cachedMoveInput.sqrMagnitude > 0.0001f;
        isMoving = hasInput;

        float moveAmountX = 0f, moveAmountY = 0f, moveAmountZ = 0f;
        if (magnetBase != null && hasInput && magnetBase.GetComponent<Collider>() != null)
        {
            Collider magnetCol = magnetBase.GetComponent<Collider>();
            Vector3 origin = magnetBase.transform.position;
            Quaternion orientation = magnetBase.transform.rotation;
            Vector3 halfExtents = magnetCol.bounds.extents;
            int layerMask = LayerMask.GetMask("Obstacle");

            moveAmountX = GetAxisMoveAmount(xInput, magnetBase.transform.right, craneMoveSpeed, origin, halfExtents, orientation, layerMask, magnetCol, "X");
            moveAmountY = GetAxisMoveAmount(yInput, magnetBase.transform.up, craneMoveSpeed, origin, halfExtents, orientation, layerMask, magnetCol, "Y");
            moveAmountZ = GetAxisMoveAmount(zInput, magnetBase.transform.forward, GetForwardBackwardMoveSpeed(), origin, halfExtents, orientation, layerMask, magnetCol, "Z");
        }
        else if (hasInput)
        {
            Debug.LogWarning("[CranePuzzle] magnetBase is not assigned for CraneMovement!");
        }

        // Move crane parts after collision/movement logic
        for (int i = 0; i < craneParts.Count; i++)
        {
            CranePart part = craneParts[i];
            if (part == null || part.partObject == null) continue;
            Vector3 basePos = part.useWorldPosition ? part.partObject.transform.position : part.partObject.transform.localPosition;
            Vector3 next = basePos;
            if (part.moveX) next.x += moveAmountX;
            if (part.moveY) next.y += moveAmountY;
            if (part.moveZ) next.z += moveAmountZ;
            if (part.moveX) next.x = Mathf.Clamp(next.x, part.minX, part.maxX);
            if (part.moveY) next.y = Mathf.Clamp(next.y, part.minY, part.maxY);
            if (part.moveZ) next.z = Mathf.Clamp(next.z, part.minZ, part.maxZ);
            if (part.useWorldPosition)
                part.partObject.transform.position = next;
            else
                part.partObject.transform.localPosition = next;
        }
    }

    /// <summary>
    /// Calculates the allowed movement along a given axis, performing overlap and boxcast checks to prevent collisions.
    /// Returns the actual move amount (may be zero if blocked).
    /// </summary>
    /// <param name="input">User input for this axis</param>
    /// <param name="axis">World axis direction (right, up, forward)</param>
    /// <param name="speed">Movement speed for this axis</param>
    /// <param name="origin">Start position for collision checks</param>
    /// <param name="halfExtents">Half extents of the collider for box checks</param>
    /// <param name="orientation">Rotation of the collider</param>
    /// <param name="layerMask">Layer mask for collision</param>
    /// <param name="magnetCol">Reference to the magnet's collider (to ignore self)</param>
    /// <param name="axisLabel">Label for debug logs (X, Y, Z)</param>
    /// <returns>Allowed move amount for this axis</returns>
    private float GetAxisMoveAmount(float input, Vector3 axis, float speed, Vector3 origin, Vector3 halfExtents, Quaternion orientation, int layerMask, Collider magnetCol, string axisLabel)
    {
        // Ignore if no input for this axis
        if (Mathf.Abs(input) < 0.0001f) return 0f;
        float tryMove = input * speed * Time.deltaTime;
        Vector3 worldMoveDir = axis * Mathf.Sign(tryMove);
        Vector3 nextWorldPos = origin + worldMoveDir * tryMove;

        // Check for overlap at the intended next position
        Collider[] overlaps = Physics.OverlapBox(nextWorldPos, halfExtents, orientation, layerMask, QueryTriggerInteraction.Ignore);
        foreach (var c in overlaps)
        {
            if (c != magnetCol && !c.isTrigger)
            {
                Debug.Log($"[CranePuzzle][OverlapBlock-{axisLabel}] Would overlap {c.name} at {nextWorldPos}, blocking {axisLabel} move.");
                return 0f;
            }
        }

        // Check for blocking objects along the path
        RaycastHit hitInfo;
        float distance = Mathf.Abs(tryMove);
        bool hit = Physics.BoxCast(origin, halfExtents, worldMoveDir, out hitInfo, orientation, distance, layerMask, QueryTriggerInteraction.Ignore);
        if (hit)
        {
            float allowedMove = hitInfo.distance - 0.01f;
            allowedMove = Mathf.Max(0f, allowedMove);
            Debug.Log($"[CranePuzzle] [BoxCast-{axisLabel}] Blocked by {hitInfo.collider.name} at {hitInfo.point}, allowedMove: {allowedMove}, requested: {distance}, origin: {origin}, dir: {worldMoveDir}, colliderLayer: {LayerMask.LayerToName(hitInfo.collider.gameObject.layer)}, isTrigger: {hitInfo.collider.isTrigger}");
            return Mathf.Sign(tryMove) * allowedMove;
        }
        else
        {
            Debug.Log($"[CranePuzzle] [BoxCast-{axisLabel}] No hit, moving full distance: {distance}, origin: {origin}, dir: {worldMoveDir}");
            return tryMove;
        }
    }
        // Light/mesh color feedback for blocked state
        
    public CraneMovementDirection GetCurrentMovementDirection()
    {
        if (!isMoving)
            return CraneMovementDirection.None;

        GetProcessedMoveInput(out float xInput, out float yInput, out float zInput);

        float absX = Mathf.Abs(xInput);
        float absY = Mathf.Abs(yInput);
        float absZ = Mathf.Abs(zInput);

        if (absX >= absY && absX >= absZ && absX > 0f)
        {
            return xInput > 0 ? CraneMovementDirection.Right : CraneMovementDirection.Left;
        }

        if (HasMovableZAxis() && absZ >= absY && absZ > 0f)
        {
            return zInput > 0 ? CraneMovementDirection.Forward : CraneMovementDirection.Backward;
        }

        if (HasMovableYAxis() && absY > 0f)
        {
            return yInput > 0 ? CraneMovementDirection.Up : CraneMovementDirection.Down;
        }

        return CraneMovementDirection.None;
    }

    public enum CraneMovementDirection
{
    None,
    Up,
    Down,
    Left,
    Right,
    Forward,
    Backward
}

    public bool IsMoving()
    {
        return isMoving;
    }
    
    public bool IsRetracting()
    {
        return isRetracting;
    }

    protected bool IsConfirmTriggered()
    {
        InputAction actionToRead = runtimeConfirmAction != null
            ? runtimeConfirmAction
            : (_confirmPuzzleAction != null ? _confirmPuzzleAction.action : null);

        return actionToRead != null && actionToRead.triggered;
    }

    protected virtual bool HandleEscapeTriggered()
    {
        EndPuzzle();
        return true;
    }

    #region Restrict/Restore Movement
    //After puzzle ends, restore player movement if it was disabled
    private void RestorePlayerMovement()
    {
        // Restore player's movement component; reacquire if cache missing
            if (cachedPlayerMovement == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    cachedPlayerMovement = player.GetComponent<PlayerMovement>();
            }

            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = true;
                cachedPlayerMovement.SuppressLocomotionAnimations(false);
                cachedPlayerMovement.ForceLocomotionRefresh();
                
                var cc = cachedPlayerMovement.GetComponent<CharacterController>();
                if (cc != null && !cc.enabled)
                {
                    cc.enabled = true;
                }
            }

            cachedPlayerAnimationController?.PlayIdle();

            cachedPlayerAnimationController = null;
            cachedPlayerMovement = null;
    }

    protected void LockOrUnlockMovement(bool lockMovement)
    {
        if (lockMovement)
        {
            // Cache original axis settings before locking
            CacheCraneAxisSettings();
            // Lock axes according to puzzle logic (example: lock all axes except Z for part 1, lock all except X for part 0, others lock all)
            for (int i = 0; i < craneParts.Count; i++)
            {
                CranePart part = craneParts[i];
                if (i == 1)
                {
                    part.moveX = false;
                    part.moveY = false;
                    part.moveZ = true;
                }
                else if (i == 0)
                {
                    part.moveX = true;
                    part.moveY = false;
                    part.moveZ = false;
                }
                else
                {
                    part.moveX = false;
                    part.moveY = false;
                    part.moveZ = false;
                }
            }
        }
        else
        {
            // Restore original axis settings
            for (int i = 0; i < craneParts.Count; i++)
            {
                CranePart part = craneParts[i];
                part.moveX = part.cachedMoveX;
                part.moveY = part.cachedMoveY;
                part.moveZ = part.cachedMoveZ;
            }
        }
    }

    private void CacheCraneAxisSettings()
    {
        foreach (CranePart part in craneParts)
        {
            if (part != null)
            {
                part.cachedMoveX = part.moveX;
                part.cachedMoveY = part.moveY;
                part.cachedMoveZ = part.moveZ;
            }
        }
    }
    #endregion

    

    private void CacheCranePartStartPositions()
    {
        cranePartStartLocalPositions.Clear();
        if (craneParts == null)
        {
            return;
        }

        foreach (CranePart part in craneParts)
        {
            if (part != null && part.partObject != null)
            {
                if (part.useWorldPosition)
                    cranePartStartLocalPositions[part] = part.partObject.transform.position;
                else
                    cranePartStartLocalPositions[part] = part.partObject.transform.localPosition;
            }
        }
    }

    private void DisableInteractUIDuringPuzzle()
    {
        var ui = FindObjectOfType<InteractionUI>(true);
        if (ui == null)
            return;

        if (ui._interactIcon != null)
            ui._interactIcon.gameObject.SetActive(false);

        if (ui._interactText != null)
            ui._interactText.gameObject.SetActive(false);
    }

    private void EnableInteractUIAfterPuzzle()
    {
        var ui = FindObjectOfType<InteractionUI>(true);
        if (ui == null)
            return;

        if (ui._interactIcon != null)
            ui._interactIcon.gameObject.SetActive(true);

        if (ui._interactText != null)
            ui._interactText.gameObject.SetActive(true);
    }

    private void SetupCraneUI()
    {
        if (craneUI == null || craneUI.Length < 1)
            return;

        for (int i = 0; i < craneUI.Length; i++)
        {
            if (craneUI[i] != null)
                craneUI[i].SetActive(false);
        }

        string scheme = InputReader.activeControlScheme;
        if (string.IsNullOrEmpty(scheme) && InputReader.PlayerInput != null)
            scheme = InputReader.PlayerInput.currentControlScheme;

        if (scheme == "Gamepad")
        {
            if (craneUI.Length > 1 && craneUI[1] != null)
            {
                craneUI[1].SetActive(true);
            }
            else if (craneUI[0] != null)
            {
                craneUI[0].SetActive(true);
            }
        }
        else if (scheme == "Keyboard&Mouse")
        {
            if (craneUI[0] != null)
            {
                craneUI[0].SetActive(true);
            }
        }
        else
        {
            if (craneUI[0] != null)
                craneUI[0].SetActive(true);
        }
    }

    #region Utility Scripts
    // Swaps action maps
    private void SwapActionMaps(bool toCrane)
    {
        // Null checks to prevent NRE
        if (craneMap == null)
        {
            Debug.LogError("[CranePuzzle] SwapActionMaps: craneMap is null!");
            return;
        }
        if (InputReader.PlayerInput == null)
        {
            Debug.LogError("[CranePuzzle] SwapActionMaps: InputReader.PlayerInput is null!");
            return;
        }

        if (toCrane) craneMap.Enable();
        else craneMap.Disable();

        string map = (toCrane) ? "CranePuzzle" : "Gameplay";
        InputReader.PlayerInput.SwitchCurrentActionMap(map);

        // Re-resolve runtime actions after switching maps
        TryResolveRuntimeActions();
    }

    private string GetLayerMaskNames(LayerMask mask)
    {
        List<string> layers = new List<string>();
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(layerName);
                }
            }
        }
        return layers.Count > 0 ? string.Join(", ", layers) : "None";
    }

    // Checks for confirm input to start magnet extension
    protected virtual void CheckForConfirm(){}

    #endregion
    private void StartingLightColor()
    {
        ApplyDoorLightState(lockedLightBulbColor, unlockedLightBulbEmissionColor, unlockedLightColor);
    }

    

    internal MeshRenderer GetLightMeshRenderer()
    {
        if (lightBulb != null)
        {
            MeshRenderer meshRenderer = lightBulb.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                return meshRenderer;
            }
            else
            {
                Debug.LogWarning("Door light object does not have a MeshRenderer component.");
                return null;
            }
        }
        else
        {
            Debug.LogWarning("Door light object is not assigned.");
            return null;
        }
    }
    private IEnumerator FadeLightBulbColor(
        Color fromBaseColor,
        Color toBaseColor,
        Color fromEmissionColor,
        Color toEmissionColor,
        float duration
    )
    {
        MeshRenderer meshRenderer = GetLightMeshRenderer();
        if (meshRenderer == null)
            yield break;

        Debug.Log($"[CranePuzzle] Starting FadeLightBulbColor: from {fromBaseColor} to {toBaseColor}, fromEmission {fromEmissionColor} toEmission {toEmissionColor}, duration {duration}");

        if (duration <= 0f)
        {
            ApplyBulbMaterialState(meshRenderer, toBaseColor, toEmissionColor);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Color currentBaseColor = Color.Lerp(fromBaseColor, toBaseColor, t);
            Color currentEmissionColor = Color.Lerp(fromEmissionColor, toEmissionColor, t);
            Debug.Log($"[CranePuzzle] Fade step: t={t}, base={currentBaseColor}, emission={currentEmissionColor}");
            ApplyBulbMaterialState(meshRenderer, currentBaseColor, currentEmissionColor);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyBulbMaterialState(meshRenderer, toBaseColor, toEmissionColor);
    }

    // Fade Color into eachother over time, used for light color transitions when opening/closing and locking/unlocking the door
    private IEnumerator FadeColorIntoEachother(Color fromColor, Color toColor, float duration)
    {
        if (doorLight == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            doorLight.color = Color.Lerp(fromColor, toColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        doorLight.color = toColor;
    }

    private void ApplyDoorLightState(Color bulbBaseColor, Color bulbEmissionColor, Color pointLightColor)
    {
        MeshRenderer meshRenderer = GetLightMeshRenderer();
        if (meshRenderer != null)
            ApplyBulbMaterialState(meshRenderer, bulbBaseColor, bulbEmissionColor);

        if (doorLight != null)
            doorLight.color = pointLightColor;
    }

    private void ApplyBulbMaterialState(MeshRenderer meshRenderer, Color baseColor, Color emissionColor)
    {
        if (meshRenderer == null)
            return;

        lightBulbPropertyBlock ??= new MaterialPropertyBlock();

        Material[] materials = meshRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            meshRenderer.GetPropertyBlock(lightBulbPropertyBlock, i);

            if (material.HasProperty(BaseColorProperty))
                lightBulbPropertyBlock.SetColor(BaseColorProperty, baseColor);

            if (material.HasProperty(LegacyColorProperty))
                lightBulbPropertyBlock.SetColor(LegacyColorProperty, baseColor);

            if (material.HasProperty(EmissionColorProperty))
            {
                lightBulbPropertyBlock.SetColor(EmissionColorProperty, emissionColor);
            }

            meshRenderer.SetPropertyBlock(lightBulbPropertyBlock, i);
        }
    }

    private static Color ColorFromHex(string hex)
    {
        if (UnityEngine.ColorUtility.TryParseHtmlString($"#{hex}", out Color parsedColor))
            return parsedColor;

        return Color.white;
    }

    private static Color ColorFromHsv(float hueDegrees, float saturationPercent, float valuePercent, float intensity = 0f)
    {
        Color color = Color.HSVToRGB(hueDegrees / 360f, saturationPercent / 100f, valuePercent / 100f);
        if (!Mathf.Approximately(intensity, 0f))
            color *= Mathf.Pow(2f, intensity);

        return color;
    }

}

// Custom Property Drawers for showing fields based on movement axis toggles

#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(ShowIfXAttribute))]
public class ShowIfXDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {

        string parentPathX = property.propertyPath;
        int lastDot = parentPathX.LastIndexOf('.');
        if (lastDot >= 0)
        {
            string prefix = parentPathX.Substring(0, lastDot);
            var moveXField = property.serializedObject.FindProperty(prefix + ".moveX");
            if (moveXField != null && moveXField.boolValue)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string parentPathH = property.propertyPath;
        int lastDotH = parentPathH.LastIndexOf('.');
        if (lastDotH >= 0)
        {
            string prefix = parentPathH.Substring(0, lastDotH);
            var moveXField = property.serializedObject.FindProperty(prefix + ".moveX");
            if (moveXField != null && moveXField.boolValue)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }
        }
        return 0f;
    }
}

[CustomPropertyDrawer(typeof(ShowIfYAttribute))]
public class ShowIfYDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string parentPathY = property.propertyPath;
        int lastDotY = parentPathY.LastIndexOf('.');
        if (lastDotY >= 0)
        {
            string prefix = parentPathY.Substring(0, lastDotY);
            var moveYField = property.serializedObject.FindProperty(prefix + ".moveY");
            if (moveYField != null && moveYField.boolValue)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string parentPathHY = property.propertyPath;
        int lastDotHY = parentPathHY.LastIndexOf('.');
        if (lastDotHY >= 0)
        {
            string prefix = parentPathHY.Substring(0, lastDotHY);
            var moveYField = property.serializedObject.FindProperty(prefix + ".moveY");
            if (moveYField != null && moveYField.boolValue)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }
        }
        return 0f;
    }
}

[CustomPropertyDrawer(typeof(ShowIfZAttribute))]
public class ShowIfZDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string parentPathZ = property.propertyPath;
        int lastDotZ = parentPathZ.LastIndexOf('.');
        if (lastDotZ >= 0)
        {
            string prefix = parentPathZ.Substring(0, lastDotZ);
            var moveZField = property.serializedObject.FindProperty(prefix + ".moveZ");
            if (moveZField != null && moveZField.boolValue)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string parentPathHZ = property.propertyPath;
        int lastDotHZ = parentPathHZ.LastIndexOf('.');
        if (lastDotHZ >= 0)
        {
            string prefix = parentPathHZ.Substring(0, lastDotHZ);
            var moveZField = property.serializedObject.FindProperty(prefix + ".moveZ");
            if (moveZField != null && moveZField.boolValue)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }
        }
        return 0f;
    }
}
#endif

