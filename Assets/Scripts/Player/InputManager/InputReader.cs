  
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
using System.Runtime.CompilerServices;

internal enum ActionMap
{
    Gameplay,
    Menu,
    Loading
}

[Serializable]
public class InputReader : Singleton<InputReader>
{
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

    public static InputActionAsset playerControls { get; private set; }
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
            else Debug.LogWarning("[InputReader] _playerInput is null in PlayerInput getter.");

            // Tries to get PlayerInput from the singleton GameObject
            if (Instance.TryGetComponent<PlayerInput>(out var existingInput))
            {
                _playerInput = existingInput;
                return _playerInput;
            }
            else Debug.LogWarning("[InputReader] No PlayerInput found on singleton GameObject in getter. Attempting to Create one.");

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
                        rightTargetAction, loadingLookAction, loadingZoomAction;

    private bool callbacksRegistered = false;
    [SerializeField, Range(0f, 0.5f)] private float lockOnDashSuppressionWindow = 0.18f;
    private float lastDashPerformedTime = float.NegativeInfinity;

    public static event Action LockOnPressed;
    public static event Action LeftTargetPressed;
    public static event Action RightTargetPressed;

    public static bool inputBusy = false;

    [Header("DeadzoneValues")]
    [SerializeField, Range(0f, 0.5f)] internal float leftStickDeadzoneValue = 0.15f;
    [SerializeField, Range(0f, 0.5f)] internal float rightStickDeadzoneValue = 0.15f;

    // Gets the input and sets the variable
    public static Vector2 MoveInput { get; private set; }
    public static Vector2 LookInput { get; private set; }

    public InputAction LoadingLookAction => loadingLookAction;
    public InputAction LoadingZoomAction => loadingZoomAction;

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
    }

    #region Action Accessors
    // Centralized action accessors so gameplay scripts never touch InputActions directly
    public static bool JumpTriggered =>
        Instance != null
        && Instance.jumpAction != null
        && Instance.jumpAction.triggered;

    public static bool DashTriggered =>
        Instance != null
        && Instance.dashAction != null
        && Instance.dashAction.triggered;

    public static bool JumpHeld =>
        Instance != null
        && Instance.jumpAction != null
        && Instance.jumpAction.IsPressed();

    public static bool DashHeld =>
        Instance != null
        && Instance.dashAction != null
        && Instance.dashAction.IsPressed();

    public static bool GuardHeld =>
        Instance != null
        && Instance.guardAction != null
        && Instance.guardAction.IsPressed();

    public static bool LightAttackTriggered =>
        Instance != null
        && Instance.lightAttackAction != null
        && Instance.lightAttackAction.triggered;

    public static bool HeavyAttackTriggered =>
        Instance != null
        && Instance.heavyAttackAction != null
        && Instance.heavyAttackAction.triggered;

    public static bool ChangeStanceTriggered =>
        Instance != null
        && Instance.changeStanceAction != null
        && Instance.changeStanceAction.triggered;

    public static bool InteractTriggered =>
        Instance != null
        && Instance.interactAction != null
        && Instance.interactAction.triggered;

    public static bool EscapePuzzleTriggered =>
        Instance != null
        && Instance.escapePuzzleAction != null
        && Instance.escapePuzzleAction.triggered;

    public static bool NavigationMenuTriggered =>
        Instance != null
        && Instance.navigationMenuAction != null
        && Instance.navigationMenuAction.triggered;
    #endregion

    #region Unity Lifecycle
    
    protected override void Awake()
    {

        base.Awake(); // Ensure singleton behavior

        SceneManager.sceneLoaded += HandleSceneLoaded;

        // Prefer a project asset if available, but do not require Resources/.
        // If the asset isn't located under a Resources folder, fall back to a runtime-generated wrapper.
        playerControls = Resources.Load<InputActionAsset>("PlayerControls");
        if (playerControls == null)
        {
            runtimeGeneratedControls = new PlayerControls();
            playerControls = runtimeGeneratedControls.asset;

            if (playerControls == null)
            {
                Debug.LogError("[InputReader] Failed to load PlayerControls from Resources and failed to generate runtime controls.");
                return;
            }

            Debug.LogWarning("[InputReader] PlayerControls asset not found in Resources. Using runtime-generated controls instead.");
        }

        /*
        // Try to bind to an existing PlayerInput found in loaded scenes first; if none found,
        // ensure a PlayerInput component is attached to this InputReader singleton GameObject.
        if (TryAutoBindFromLoadedScenes())
        {
            // Bound to a PlayerInput on a scene object (likely the player prefab).
        }
        else
        {
            // Ensure the singleton GameObject has a PlayerInput component so menus and UI can use input
            var singletonPlayerInput = GetComponent<PlayerInput>();
            if (singletonPlayerInput == null)
            {
                singletonPlayerInput = gameObject.AddComponent<PlayerInput>();
            }
            else Debug.Log("[InputReader] singletonPlayerInput already exists.");

            if (singletonPlayerInput.actions == null && playerControls != null)
            {
                singletonPlayerInput.actions = Instantiate(playerControls);
            }

            // Do not force Gameplay map here; menus generally need Menu map available by default.
            RebindTo(singletonPlayerInput, switchToGameplay: false);

            Debug.Log("[InputReader] No scene PlayerInput found. Attached PlayerInput to InputReader singleton for consistent access.");
        }
        */

        // Use squared deadzone comparisons internally; set the default min to match the smallest deadzone
        InputSystem.settings.defaultDeadzoneMin = Mathf.Min(leftStickDeadzoneValue, rightStickDeadzoneValue);

        EnsureCursorManager(PlayerInput);
    }

    private void Start()
    {
        if (TryGetComponent<PlayerInput>(out var existingInput))
        {
            RebindTo(existingInput);
            Debug.Log("[InputReader] Found existing PlayerInput on Start and bound to it.");
        }
        else
        {
            PlayerInput = gameObject.AddComponent<PlayerInput>();
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
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

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        /*
        // If we already have a PlayerInput, only early-out when it's a real scene/player binding.
        // When starting from MainMenu, InputReader may create a fallback PlayerInput on itself;
        // once gameplay scenes load we must rebind to the actual player PlayerInput.
        if (_playerInput != null)
        {
            // Unity's fake-null for destroyed objects
            if (_playerInput == null)
            {
                _playerInput = null;
            }
            else
            {
                bool isFallbackOnSingleton = _playerInput.gameObject == gameObject
                    || _playerInput.gameObject.scene.name == "DontDestroyOnLoad";

                if (!isFallbackOnSingleton)
                    return;
            }
        }

        Debug.Log("[InputReader] Attempting to bind PlayerInput after scene load.");

        if (scene.isLoaded && TryBindFromScene(scene))
            return;

        // If the specific scene didn't contain a player, try a broader search (e.g., additive load order differences)
        TryAutoBindFromLoadedScenes();
        */
    }

    private void Update()
    {
        // Read move/look only when action exists and is enabled. Use optimized deadzone check.
        if (moveAction != null && moveAction.enabled)
            MoveInput = ApplyDeadzone(moveAction.ReadValue<Vector2>(), leftStickDeadzoneValue);
        else
            MoveInput = Vector2.zero;

        if (lookAction != null && lookAction.enabled)
            LookInput = ApplyDeadzone(lookAction.ReadValue<Vector2>(), rightStickDeadzoneValue);
        else
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
        else Debug.Log("[InputReader] RebindTo received a valid PlayerInput.");

        // Disable any old actions to avoid ghost reads
        SetAllActionsEnabled(false);

        UnregisterActionCallbacks();

        PlayerInput = newPlayerInput;

        EnsureCursorManager(PlayerInput);

        if (PlayerInput.actions == null)
        {
            if (playerControls != null)
            {
                PlayerInput.actions = Instantiate(playerControls);
            }
            else
            {
                Debug.LogError("[InputReader] PlayerInput has no action asset and no fallback is available.");
                return;
            }
        }

        if (!PlayerInput.enabled)
            PlayerInput.enabled = true;

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
            interactAction = GetAction(ActionNames.Interact);
            escapePuzzleAction = GetAction(ActionNames.EscapePuzzle);
            lockOnAction = GetAction(ActionNames.LockOn);
            leftTargetAction = GetAction(ActionNames.LeftTarget);
            rightTargetAction = GetAction(ActionNames.RightTarget);
            navigationMenuAction = GetAction(ActionNames.NavigationMenu);
            loadingLookAction = GetAction(ActionNames.LoadingLook);
            loadingZoomAction = GetAction(ActionNames.LoadingZoom);

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

    private static void EnsureCursorManager(PlayerInput target)
    {
        if (target == null)
            return;

        if (target.GetComponent<CursorBySchemeAndMap>() == null)
            target.gameObject.AddComponent<CursorBySchemeAndMap>();
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

        if (Time.time - lastDashPerformedTime <= lockOnDashSuppressionWindow)
            return;

        LockOnPressed?.Invoke();
    }

    private void HandleLeftTargetPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        LeftTargetPressed?.Invoke();
    }

    private void HandleRightTargetPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        RightTargetPressed?.Invoke();
    }

    private void HandleDashPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        lastDashPerformedTime = Time.time;
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
            Debug.Log("[InputReader] PlayerInput or PlayerInput.actions is null in GetAction.");
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
    private void SetAllActionsEnabled(bool enabled)
    {
        var actions = new InputAction[]
        {
            moveAction, jumpAction, lookAction, changeStanceAction, guardAction,
            lightAttackAction, heavyAttackAction, dashAction, navigationMenuAction,
            interactAction, escapePuzzleAction, lockOnAction, leftTargetAction,
            rightTargetAction, loadingLookAction, loadingZoomAction
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

