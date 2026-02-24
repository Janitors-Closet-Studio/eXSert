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
}

// These will be used to show/hide fields in the inspector based on which axes are enabled
public class ShowIfXAttribute : PropertyAttribute { }
public class ShowIfYAttribute : PropertyAttribute { }
public class ShowIfZAttribute : PropertyAttribute { }

public class CranePuzzle : PuzzlePart
{
    // Cache of the player's movement component so it can be re-enabled later
    private PlayerMovement cachedPlayerMovement;

    #region Serializable Fields
    [Header("Input Actions")]
    [SerializeField, CriticalReference] internal InputActionReference craneMoveAction;
    [SerializeField, CriticalReference] internal InputActionReference _escapePuzzleAction;
    [SerializeField, CriticalReference] internal InputActionReference _confirmPuzzleAction;

    [Space(10)]
    [Header("Camera")]
    // Cinemachine camera for the puzzle
    [SerializeField, CriticalReference] protected CinemachineCamera puzzleCamera;

    [Space(10)]

    // List of crane parts to move
    [Header("Crane Parts")]
    [SerializeField] protected List<CranePart> craneParts = new List<CranePart>();

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

    private bool TryResolveRuntimeActions()
    {
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

        craneMap = actions.FindActionMap("CranePuzzle");
        if (craneMap == null)
        {
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
        if (reference != null && reference.action != null)
        {
            InputAction resolved = craneMap.FindAction(reference.action.name);
            if (resolved == null)
            
            return resolved;
        }
        return null;
    }

    private int SetupCranePuzzle()
    {
        CacheCranePartStartPositions();

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

        // If found, disable movement and cache for restoration
        if (pm != null)
        {
            cachedPlayerMovement = pm;
            pm.enabled = false;
        }


        SwitchPuzzleCamera();

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
        StartPuzzle();
    }
    // Called by whatever system starts this puzzle
    public override void StartPuzzle()
    {   
        DisableInteractUIDuringPuzzle();

        int status = SetupCranePuzzle();
    }

    // Call this when the puzzle is finished or cancelled
    public override void EndPuzzle()
    {

        isCompleted = true;

        foreach (GameObject img in craneUI)
        {
            img.SetActive(false);
        }

        puzzleActive = false;

        StopAllCoroutines();
        moveCoroutine = null;
        isMoving = false;

        // Unlock crane movement
        LockOrUnlockMovement(false);
        isExtending = false;
        isAutomatedMovement = false;

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
        isCompleted = false;
        RestorePlayerMovement();
    }

    #endregion
    // Read CranePuzzle move action when available (prefer runtime action from PlayerInput)
    private void ReadMoveAction()
    {
        InputAction actionToRead = runtimeCraneMoveAction != null ? runtimeCraneMoveAction : (craneMoveAction != null ? craneMoveAction.action : null);
        if (actionToRead != null)
        {
            cachedMoveInput = actionToRead.ReadValue<Vector2>();

            if (invertHorizontal)
                cachedMoveInput.x *= -1f; 
        }
    }

    public IEnumerator MoveCraneCoroutine()
    {
        while (puzzleActive && !isAutomatedMovement && !isExtending)
        {

            ReadMoveAction();

            if(_escapePuzzleAction != null && _escapePuzzleAction.action != null && _escapePuzzleAction.action.triggered)
            {
                EndPuzzle();
                yield break;
            }

            CheckForConfirm();

            CraneMovement();

            yield return null;
        }

        isMoving = false;
        moveCoroutine = null;
    }

    public virtual void CraneMovement()
    {
        Vector2 input = cachedMoveInput;
        float xInput = input.x;
        float yInput = input.y;

        if (swapXZControls)
        {
            float temp = xInput;
            xInput = yInput;
            yInput = temp;
        }

        bool hasInput = input.sqrMagnitude > 0.0001f;
        isMoving = hasInput;

        if (hasInput)
        {
            for (int i = 0; i < craneParts.Count; i++)
            {
                CranePart part = craneParts[i];
                if (part == null || part.partObject == null) continue;

                Vector3 localPos = part.partObject.transform.localPosition;
                Vector3 delta = Vector3.zero;

                if (part.moveX)
                {
                    delta.x = xInput;
                }
                if (part.moveY)
                {
                    delta.y = yInput;
                }
                if (part.moveZ)
                {
                    delta.z = yInput;
                }
                if (delta != Vector3.zero)
                {
                    Vector3 next = localPos + delta * craneMoveSpeed * Time.deltaTime;

                    if (part.moveX)
                    {
                        next.x = Mathf.Clamp(next.x, part.minX, part.maxX);
                    }
                    if (part.moveY)
                    {
                        next.y = Mathf.Clamp(next.y, part.minY, part.maxY);
                    }
                    if (part.moveZ)
                    {
                        next.z = Mathf.Clamp(next.z, part.minZ, part.maxZ);
                    }

                    part.partObject.transform.localPosition = next;
                }
            }
        }
    }

    public CraneMovementDirection GetCurrentMovementDirection()
    {
        if (!isMoving)
            return CraneMovementDirection.None;

        Vector2 input = cachedMoveInput;
        float xInput = input.x;
        float yInput = input.y;

        if (swapXZControls)
        {
            float temp = xInput;
            xInput = yInput;
            yInput = temp;
        }

        if (Mathf.Abs(xInput) > Mathf.Abs(yInput))
        {
            return xInput > 0 ? CraneMovementDirection.Right : CraneMovementDirection.Left;
        }
        else if (Mathf.Abs(yInput) > Mathf.Abs(xInput))
        {
            return yInput > 0 ? CraneMovementDirection.Up : CraneMovementDirection.Down;
        }
        else if (Mathf.Abs(yInput) > 0 && swapXZControls)
        {
            return yInput > 0 ? CraneMovementDirection.Forward : CraneMovementDirection.Backward;
        }
        else
        {
            return CraneMovementDirection.None;
        }
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
                
                var cc = cachedPlayerMovement.GetComponent<CharacterController>();
                if (cc != null && !cc.enabled)
                {
                    cc.enabled = true;
                }
            }
            cachedPlayerMovement = null;
    }

    protected void LockOrUnlockMovement(bool lockMovement)
    {
        for (int i = 0; i < craneParts.Count; i++)
        {
            CranePart part = craneParts[i];
            
            // craneParts[1]: Lock X and Y, control Z only
            if (i == 1)
            {
                part.moveX = false;
                part.moveY = false;
                part.moveZ = !lockMovement;
            }
            // craneParts[0]: Lock Y and Z, control X only
            else if (i == 0)
            {
                part.moveX = !lockMovement;
                part.moveY = false;
                part.moveZ = false;
            }
            // Other parts: Lock/unlock all axes
            else
            {
                part.moveX = !lockMovement;
                part.moveY = !lockMovement;
                part.moveZ = !lockMovement;
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
        if (toCrane) craneMap.Enable();
        else craneMap.Disable();

        string map = (toCrane) ? "CranePuzzle" : "Gameplay";
        InputReader.PlayerInput.SwitchCurrentActionMap(map);
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

