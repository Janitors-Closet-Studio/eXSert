  
/*
 * Written by Brandon Wahl
 * 
 * Assigns events to their action in the player's action map
 * 
 * 
 * 
 * Editted by Will T
 * 
 * removed event assignments and now just reads input values directly from actions
 * tried to simplify input management
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Singletons;
using eXsert;

internal enum ActionMap
{
    Gameplay,
    Menu,
    Loading
}

public class InputReader : Singleton<InputReader>
{
    public override string ToString() => "Input Reader";

    internal static string activeControlScheme;

    /// <summary>
    /// What the current action map being used is
    /// </summary>
    internal static ActionMap CurrentActionMap
    {
        get
        {
            /*
             * implement this later to check if player scene is loaded
             */

            try
            {
                string mapName = PlayerInput.currentActionMap.name;
                return mapName switch
                {
                    "Gameplay" => ActionMap.Gameplay,
                    "Menu" => ActionMap.Menu,
                    "Loading" => ActionMap.Loading,
                    _ => ActionMap.Gameplay,
                };
            }
            catch (Exception)
            {
                return ActionMap.Menu;
            }
        }
        private set
        {
            try
            {
                string mapName = value switch
                {
                    ActionMap.Gameplay => "Gameplay",
                    ActionMap.Menu => "Menu",
                    ActionMap.Loading => "Loading",
                    _ => "Gameplay",
                };
                PlayerInput.SwitchCurrentActionMap(mapName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputReader] Failed to switch action map to {value}: {e.Message}");
            }
        }
    }

    public static InputActionAsset PlayerControls { get; private set; }
    private PlayerControls runtimeGeneratedControls;

    /// <summary>
    /// Gets or sets the singleton <see cref="PlayerInput"/> instance associated with the current game context.
    /// </summary>
    /// <remarks>Accessing this property ensures that a <see cref="PlayerInput"/> component exists on the
    /// singleton GameObject. If no instance is present, one will be created automatically. This property is intended
    /// for global input management scenarios.</remarks>
    public static PlayerInput PlayerInput
    {
        get
        {
            if (isApplicationQuitting)
            {
                Debug.LogWarning("[InputReader] Attempted to access PlayerInput during application quit. Returning null.");
                return null;
            }

            if (_playerInput != null) return _playerInput;

            // Tries to get PlayerInput from the singleton GameObject
            if (Instance.TryGetComponent<PlayerInput>(out var existingInput))
                return _playerInput = existingInput;

            // Creates a PlayerInput component on the singleton GameObject if none exists
            PlayerInput newInput = Instance.gameObject.AddComponent<PlayerInput>();
            newInput.neverAutoSwitchControlSchemes = false;
            return _playerInput = newInput;
        }

        private set { _playerInput = value; }
    }
    private static PlayerInput _playerInput;

    internal float mouseSens;

    // Input Actions
    private InputAction moveAction, jumpAction, lookAction, changeStanceAction, guardAction,
                        lightAttackAction, heavyAttackAction, dashAction, navigationMenuAction,
                        interactAction, escapePuzzleAction, lockOnAction, leftTargetAction,
                        rightTargetAction, loadingLookAction, loadingZoomAction, pauseAction,
                        toggleWalkAction, debugMenu;

    private bool callbacksRegistered = false;
    [SerializeField, Range(0f, 0.5f)] private float lockOnDashSuppressionWindow = 0.18f;
    [SerializeField] private bool enableKeyboardLockOnFallbacks = true;
    [SerializeField] private bool debugLockOnInput = false;
    private float lastDashPerformedTime = float.NegativeInfinity;

    public static event Action LockOnPressed;
    public static event Action LeftTargetPressed;
    public static event Action RightTargetPressed;

    public static bool inputBusy = false;
    private static readonly HashSet<string> gameplayInputBlockOwners = new();

    [Header("DeadzoneValues")]
    [SerializeField, Range(0f, 0.5f)] internal float leftStickDeadzoneValue = 0.15f;
    [SerializeField, Range(0f, 0.5f)] internal float rightStickDeadzoneValue = 0.15f;

    // Gets the input and sets the variable
    public static Vector2 MoveInput { get; private set; }
    public static Vector2 LookInput { get; private set; }

    public static bool IsGameplayInputBlocked => gameplayInputBlockOwners.Count > 0;

    public InputAction LoadingLookAction => loadingLookAction;
    public InputAction LoadingZoomAction => loadingZoomAction;
    public InputAction DebugMenu => debugMenu;

    // Centralize action names to avoid typos and make maintenance easier
    private static class ActionNames
    {
        public const string Move = "Move";
        public const string Jump = "Jump";
        public const string Look = "Look";
        public const string ChangeStance = "ChangeStance";
        public const string Guard = "Guard";
        public const string LightAttack = "LightAttack";
        public const string HeavyAttack = "HeavyAttack";
        public const string Dash = "Dash";
        public const string NavigationMenu = "NavigationMenu";
        public const string Interact = "Interact";
        public const string EscapePuzzle = "EscapePuzzle";
        public const string LockOn = "LockOn";
        public const string LeftTarget = "LeftTarget";
        public const string RightTarget = "RightTarget";
        public const string LoadingLook = "LoadingLook";
        public const string LoadingZoom = "LoadingZoom";
        public const string Pause = "Pause";
        public const string ToggleWalk = "ToggleWalk";
        public const string DebugMenu = "Open/Close Debug Menu";
    }

    #region Action Accessors
    // Centralized action accessors so gameplay scripts never touch InputActions directly
    public static bool JumpTriggered =>
        IsGameplayActionTriggered(Instance?.jumpAction);

    public static bool DashTriggered =>
        IsGameplayActionTriggered(Instance?.dashAction);

    public static bool ToggleWalkTriggered =>
        IsGameplayActionTriggered(Instance?.toggleWalkAction);

    public static bool JumpHeld =>
        IsGameplayActionPressed(Instance?.jumpAction);

    public static bool DashHeld =>
        IsGameplayActionPressed(Instance?.dashAction);

    public static bool GuardHeld =>
        IsGameplayActionPressed(Instance?.guardAction);

    public static bool LightAttackTriggered =>
        IsGameplayActionTriggered(Instance?.lightAttackAction);

    public static bool HeavyAttackTriggered =>
        IsGameplayActionTriggered(Instance?.heavyAttackAction);

    public static bool ChangeStanceTriggered =>
        IsGameplayActionTriggered(Instance?.changeStanceAction);

    public static bool InteractTriggered =>
        IsGameplayActionTriggered(Instance?.interactAction);

    public static bool EscapePuzzleTriggered =>
        IsGameplayActionTriggered(Instance?.escapePuzzleAction);

    public static bool NavigationMenuTriggered =>
        Instance != null
        && Instance.navigationMenuAction != null
        && Instance.navigationMenuAction.triggered;

    public static bool PauseMenuTriggered =>
        Instance != null
        && Instance.pauseAction != null
        && Instance.pauseAction.triggered;

    #endregion

    #region Unity Lifecycle
    protected override void Awake()
    {
        base.Awake(); // Ensure singleton behavior

        PlayerControls = Resources.Load<InputActionAsset>("PlayerControls");
        if (PlayerControls == null)
        {
            Debug.LogWarning("[InputReader] PlayerControls asset not found in Resources. Attempting to generate controls at runtime.");
            runtimeGeneratedControls = new PlayerControls();
            PlayerControls = runtimeGeneratedControls.asset;

            if (PlayerControls == null)
            {
                Debug.LogError("[InputReader] Failed to load PlayerControls from Resources and failed to generate runtime controls.");
                return;
            }
        }

        // Use squared deadzone comparisons internally; set the default min to match the smallest deadzone
        InputSystem.settings.defaultDeadzoneMin = Mathf.Min(leftStickDeadzoneValue, rightStickDeadzoneValue);

        debugMenu = GetAction(ActionNames.DebugMenu);
    }

    private void Start()
    {
        if (TryGetComponent<PlayerInput>(out var existingInput))
        {
            RebindTo(existingInput);
            DebugLogSettingsM.ConditionalLog(DebugLogCategory.Input, "[InputReader] Found existing PlayerInput on Start and bound to it.");
        }
        else
        {
            PlayerInput = gameObject.AddComponent<PlayerInput>();
            PlayerInput.neverAutoSwitchControlSchemes = false;
            RebindTo(PlayerInput);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        UnregisterActionCallbacks();

        if (runtimeGeneratedControls != null)
        {
            runtimeGeneratedControls.Dispose();
            runtimeGeneratedControls = null;
        }
    }

    //Turns the actions on
    private void OnEnable() => SetAllActionsEnabled(true);

    private void OnDisable() => SetAllActionsEnabled(false);

    private void Update()
    {
        if (IsGameplayInputBlocked)
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
        }

        // Read move/look only when action exists and is enabled. Use optimized deadzone check.
        if (!IsGameplayInputBlocked && moveAction != null && moveAction.enabled)
            MoveInput = ApplyDeadzone(moveAction.ReadValue<Vector2>(), leftStickDeadzoneValue);
        else if (!IsGameplayInputBlocked)
            MoveInput = Vector2.zero;

        if (!IsGameplayInputBlocked && lookAction != null && lookAction.enabled)
            LookInput = ApplyDeadzone(lookAction.ReadValue<Vector2>(), rightStickDeadzoneValue);
        else if (!IsGameplayInputBlocked)
            LookInput = Vector2.zero;

        if (_playerInput != null)
        {
            try
            {
                activeControlScheme = _playerInput.currentControlScheme;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputReader] Failed to read currentControlScheme: {e.Message}");
                activeControlScheme = string.Empty;
            }
        }
        else
        {
            Debug.Log("[InputReader] _playerInput is null in LookInput update.");
            activeControlScheme = string.Empty;
        }

        HandleKeyboardLockOnFallbacks();
    }
    #endregion

    

    /// <summary>
    /// Rebind this InputReader to a new PlayerInput instance (e.g., after scene restart or player respawn).
    /// Safely swaps action references and ensures the correct action map is active.
    /// </summary>
    /// <param name="newPlayerInput">The PlayerInput to bind to.</param>
    /// <param name="switchToGameplay">If true, switch the current action map to "Gameplay".</param>
    public void RebindTo(PlayerInput newPlayerInput, bool switchToGameplay = true)
    {
        if (newPlayerInput == null)
        {
            Debug.LogWarning("[InputReader] RebindTo called with null PlayerInput");
            return;
        }
        else DebugLogSettingsM.ConditionalLog(DebugLogCategory.Input, "[InputReader] RebindTo received a valid PlayerInput.");

        // Disable any old actions to avoid ghost reads
        SetAllActionsEnabled(false);

        UnregisterActionCallbacks();

        if (PlayerInput.actions == null)
        {
            if (PlayerControls != null)
                PlayerInput.actions = Instantiate(PlayerControls);
            else
            {
                Debug.LogError("[InputReader] PlayerInput has no action asset and no fallback is available.");
                return;
            }
        }

        if (!PlayerInput.enabled) PlayerInput.enabled = true;

        PlayerInput.neverAutoSwitchControlSchemes = false;

        // Optionally set the correct map first so action lookups succeed
        if (switchToGameplay)
        {
            try { PlayerInput.SwitchCurrentActionMap("Gameplay"); }
            catch (Exception e) { Debug.LogWarning($"[InputReader] Failed to switch to Gameplay map during rebind: {e.Message}"); }
        }

        try
        {
            // Use helper to safely look up actions and avoid repeated try/catch blocks
            moveAction = GetAction(ActionNames.Move);
            jumpAction = GetAction(ActionNames.Jump);
            lookAction = GetAction(ActionNames.Look);
            changeStanceAction = GetAction(ActionNames.ChangeStance);
            guardAction = GetAction(ActionNames.Guard);
            lightAttackAction = GetAction(ActionNames.LightAttack);
            heavyAttackAction = GetAction(ActionNames.HeavyAttack);
            dashAction = GetAction(ActionNames.Dash);
            toggleWalkAction = GetAction(ActionNames.ToggleWalk);
            interactAction = GetAction(ActionNames.Interact);
            escapePuzzleAction = GetAction(ActionNames.EscapePuzzle);
            lockOnAction = GetAction(ActionNames.LockOn);
            leftTargetAction = GetAction(ActionNames.LeftTarget);
            rightTargetAction = GetAction(ActionNames.RightTarget);
            navigationMenuAction = GetAction(ActionNames.NavigationMenu);
            loadingLookAction = GetAction(ActionNames.LoadingLook);
            loadingZoomAction = GetAction(ActionNames.LoadingZoom);
            pauseAction = GetAction(ActionNames.Pause);

            RegisterActionCallbacks();
        }
        catch (Exception e)
        {
            Debug.LogError($"[InputReader] Failed to assign actions during rebind: {e.Message}");
        }

        // Re-enable actions if this component is active
        if (isActiveAndEnabled)
            SetAllActionsEnabled(true);

        try
        {
            activeControlScheme = PlayerInput.currentControlScheme;
        }
        catch (Exception)
        {
            activeControlScheme = string.Empty;
        }

        Debug.Log("[InputReader] Rebound to new PlayerInput and actions re-enabled.");
    }

    private void RegisterActionCallbacks()
    {
        if (callbacksRegistered)
            return;

        // Map actions to handlers in a single place for clarity and maintainability
        var mappings = new (InputAction Action, Action<InputAction.CallbackContext> Handler)[]
        {
            (lockOnAction, HandleLockOnPerformed),
            (leftTargetAction, HandleLeftTargetPerformed),
            (rightTargetAction, HandleRightTargetPerformed),
            (dashAction, HandleDashPerformed)
        };

        foreach (var (action, handler) in mappings)
        {
            if (action != null && handler != null)
                action.performed += handler;
        }

        LogLockOnBindings();

        callbacksRegistered = true;
    }

    private void UnregisterActionCallbacks()
    {
        if (!callbacksRegistered)
            return;

        var mappings = new (InputAction Action, Action<InputAction.CallbackContext> Handler)[]
        {
            (lockOnAction, HandleLockOnPerformed),
            (leftTargetAction, HandleLeftTargetPerformed),
            (rightTargetAction, HandleRightTargetPerformed),
            (dashAction, HandleDashPerformed)
        };

        foreach (var (action, handler) in mappings)
        {
            if (action != null && handler != null)
                action.performed -= handler;
        }

        callbacksRegistered = false;
    }

    private void HandleLockOnPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (IsGameplayInputBlocked)
        {
            LogLockOnInput("LockOn input received but gameplay input is blocked.");
            return;
        }

        if (Time.time - lastDashPerformedTime <= lockOnDashSuppressionWindow)
        {
            LogLockOnInput($"LockOn input suppressed by dash window ({lockOnDashSuppressionWindow:0.###}s).");
            return;
        }

        LogLockOnInput("LockOn action performed -> invoking LockOnPressed event.");
        LockOnPressed?.Invoke();
    }

    private void HandleLeftTargetPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (IsGameplayInputBlocked)
        {
            LogLockOnInput("LeftTarget input received but gameplay input is blocked.");
            return;
        }

        LogLockOnInput("LeftTarget action performed -> invoking LeftTargetPressed event.");
        LeftTargetPressed?.Invoke();
    }

    private void HandleRightTargetPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (IsGameplayInputBlocked)
        {
            LogLockOnInput("RightTarget input received but gameplay input is blocked.");
            return;
        }

        LogLockOnInput("RightTarget action performed -> invoking RightTargetPressed event.");
        RightTargetPressed?.Invoke();
    }

    private void HandleDashPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (IsGameplayInputBlocked)
            return;

        lastDashPerformedTime = Time.time;
        LogLockOnInput($"Dash performed at t={lastDashPerformedTime:0.###}. LockOn suppression window active for {lockOnDashSuppressionWindow:0.###}s.");
    }

    private void HandleKeyboardLockOnFallbacks()
    {
        if (!enableKeyboardLockOnFallbacks)
            return;

        if (IsGameplayInputBlocked || CurrentActionMap != ActionMap.Gameplay)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool lockOnKeyPressed = keyboard.cKey.wasPressedThisFrame;
        bool leftTargetKeyPressed = keyboard.tabKey.wasPressedThisFrame;
        bool rightTargetKeyPressed = keyboard.leftAltKey.wasPressedThisFrame;

        bool useLockOnFallback = ShouldUseKeyboardFallback(lockOnAction);
        bool useLeftFallback = ShouldUseKeyboardFallback(leftTargetAction);
        bool useRightFallback = ShouldUseKeyboardFallback(rightTargetAction);

        if (lockOnKeyPressed && !useLockOnFallback)
            LogLockOnInput("Keyboard C pressed, but LockOn fallback skipped because LockOn action already has keyboard binding(s).");

        if (leftTargetKeyPressed && !useLeftFallback)
            LogLockOnInput("Keyboard Tab pressed, but LeftTarget fallback skipped because LeftTarget action already has keyboard binding(s).");

        if (rightTargetKeyPressed && !useRightFallback)
            LogLockOnInput("Keyboard LeftAlt pressed, but RightTarget fallback skipped because RightTarget action already has keyboard binding(s).");

        if (useLockOnFallback
            && lockOnKeyPressed
            && Time.time - lastDashPerformedTime > lockOnDashSuppressionWindow)
        {
            LogLockOnInput("Keyboard fallback: C -> LockOnPressed event.");
            LockOnPressed?.Invoke();
        }

        if (useLockOnFallback
            && lockOnKeyPressed
            && Time.time - lastDashPerformedTime <= lockOnDashSuppressionWindow)
        {
            LogLockOnInput($"Keyboard fallback C pressed but suppressed by dash window ({lockOnDashSuppressionWindow:0.###}s).");
        }

        if (useLeftFallback && leftTargetKeyPressed)
        {
            LogLockOnInput("Keyboard fallback: Tab -> LeftTargetPressed event.");
            LeftTargetPressed?.Invoke();
        }

        if (useRightFallback && rightTargetKeyPressed)
        {
            LogLockOnInput("Keyboard fallback: LeftAlt -> RightTargetPressed event.");
            RightTargetPressed?.Invoke();
        }
    }

    private void LogLockOnBindings()
    {
        if (!debugLockOnInput)
            return;

        LogActionBindings("LockOn", lockOnAction);
        LogActionBindings("LeftTarget", leftTargetAction);
        LogActionBindings("RightTarget", rightTargetAction);
    }

    private void LogActionBindings(string actionName, InputAction action)
    {
        if (!debugLockOnInput)
            return;

        if (action == null)
        {
            Debug.LogWarning($"[InputReader][LockOnDebug] Action '{actionName}' is null.");
            return;
        }

        var bindings = action.bindings;
        if (bindings.Count == 0)
        {
            Debug.LogWarning($"[InputReader][LockOnDebug] Action '{actionName}' has no bindings.");
            return;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            string path = bindings[i].effectivePath;
            if (string.IsNullOrEmpty(path))
                path = bindings[i].path;

            Debug.Log($"[InputReader][LockOnDebug] {actionName} binding[{i}] path='{path}' groups='{bindings[i].groups}'.");
        }
    }

    private void LogLockOnInput(string message)
    {
        if (!debugLockOnInput)
            return;

        Debug.Log($"[InputReader][LockOnDebug] {message}");
    }

    private static bool ShouldUseKeyboardFallback(InputAction action)
    {
        if (action == null)
            return true;

        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            string path = bindings[i].effectivePath;
            if (string.IsNullOrEmpty(path))
                path = bindings[i].path;

            if (!string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static string RequestGameplayInputBlock(string ownerId = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            ownerId = Guid.NewGuid().ToString();

        if (gameplayInputBlockOwners.Add(ownerId))
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
        }

        return ownerId;
    }

    public static void ReleaseGameplayInputBlock(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        if (!gameplayInputBlockOwners.Remove(ownerId))
            return;

        if (gameplayInputBlockOwners.Count == 0)
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
        }
    }

    private static bool IsGameplayActionTriggered(InputAction action)
    {
        return !IsGameplayInputBlocked
            && action != null
            && action.triggered;
    }

    private static bool IsGameplayActionPressed(InputAction action)
    {
        return !IsGameplayInputBlocked
            && action != null
            && action.IsPressed();
    }

    private static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
    {
        if (deadzone <= 0f)
            return value;

        // Use squared magnitude to avoid an expensive sqrt call every frame
        var threshold = deadzone * deadzone;
        return value.sqrMagnitude < threshold ? Vector2.zero : value;
    }

    // Helper: safely get an action from the current PlayerInput actions asset
    private InputAction GetAction(string name)
    {
        if (PlayerInput == null || PlayerInput.actions == null)
        {
            DebugLogSettingsM.ConditionalLog(DebugLogCategory.Input, "[InputReader] PlayerInput or PlayerInput.actions is null in GetAction.");
            return null;
        }

        try
        {
            return PlayerInput.actions[name];
        }
        catch (Exception)
        {
            // The indexer throws when the action doesn't exist; swallow and return null.
            return null;
        }
    }

    // Helper: enable/disable every tracked action in one place to reduce duplication
    public void SetAllActionsEnabled(bool enabled)
    {
        var actions = new InputAction[]
        {
            moveAction, jumpAction, lookAction, changeStanceAction, guardAction,
            lightAttackAction, heavyAttackAction, dashAction, navigationMenuAction,
            interactAction, escapePuzzleAction, lockOnAction, leftTargetAction,
            rightTargetAction, loadingLookAction, loadingZoomAction, pauseAction,
            toggleWalkAction
        };

        foreach (var a in actions)
        {
            if (a == null) continue;
            try
            {
                if (enabled)
                {
                    if (!a.enabled) a.Enable();
                }
                else
                {
                    if (a.enabled) a.Disable();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputReader] Failed to {(enabled ? "enable" : "disable")} action '{a.name}': {e.Message}");
            }
        }
    }
}

