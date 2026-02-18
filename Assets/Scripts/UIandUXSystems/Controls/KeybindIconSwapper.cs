using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class KeybindIconSwapper : MonoBehaviour

{
    public enum DeviceMode
    {
        Auto,
        KeyboardMouse,
        Gamepad
    }

    public enum CraneDirection
    {
        Forward,
        Back,
        Left,
        Right
    }


    [Header("References")]
    private static KeybindIconSet sharedIconSet;
    [SerializeField] internal Image targetImage;

    [Header("Binding")]
    [SerializeField] private KeybindAction action;
    [SerializeField] private DeviceMode deviceMode = DeviceMode.Auto;

    [Header("Crane Move")]
    [SerializeField] private bool useCraneMoveIcon = false;
    [SerializeField] private CraneDirection craneDirection = CraneDirection.Forward;

    [Header("Behavior")]
    [SerializeField] private bool hideWhenMissing = false;

    private string lastScheme = string.Empty;
    private bool isSubscribed;
    private string lastBindingPath = string.Empty;

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Refresh All Keybind Icons")]
    private static void EditorRefreshAllIconsMenu() => RefreshAllIcons();
#endif

    /// <summary>
    /// Refreshes all KeybindIconSwapper instances in the current scene.
    /// Call this after loading binding overrides or when changing scenes.
    /// </summary>
    public static void RefreshAllIcons()
    {
        var swappers = FindObjectsOfType<KeybindIconSwapper>(true);
        foreach (var swapper in swappers)
        {
            swapper.RefreshIcon();
        }
    }

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        // Ensure all swappers use the same KeybindIconSet from Resources
        if (sharedIconSet == null)
        {
            sharedIconSet = Resources.Load<KeybindIconSet>("KeybindIconSet");
            if (sharedIconSet == null)
            {
                Debug.LogError("[KeybindIconSwapper] Could not find KeybindIconSet asset in Resources! Please place your KeybindIconSet.asset in a Resources folder.");
            }
        }
    }

    private void OnEnable()
    {
        InputSystem.onActionChange += HandleActionChange;
        SubscribeToPlayerInput();
        RefreshIcon();
        StartCoroutine(RefreshCoroutine());
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= HandleActionChange;
        UnsubscribeFromPlayerInput();
        StopAllCoroutines();
    }

    private IEnumerator RefreshCoroutine()
    {
        while (deviceMode == DeviceMode.Auto)
        {
            string scheme = GetCurrentScheme();
            if (!string.Equals(scheme, lastScheme))
            {
                lastScheme = scheme;
                RefreshIcon();
            }
            yield return null;
        }
    }

    private void HandleActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.BoundControlsChanged)
            return;

        RefreshIcon();
    }

    private void HandleControlsChanged(PlayerInput _)
    {
        if (deviceMode == DeviceMode.Auto)
            RefreshIcon();
    }

    private void SubscribeToPlayerInput()
    {
        if (isSubscribed)
            return;

        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.onControlsChanged += HandleControlsChanged;
            isSubscribed = true;
        }
    }

    private void UnsubscribeFromPlayerInput()
    {
        if (!isSubscribed)
            return;

        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.onControlsChanged -= HandleControlsChanged;
        isSubscribed = false;
    }

    private string GetCurrentScheme()
    {
        if (InputReader.Instance != null)
            return InputReader.activeControlScheme ?? string.Empty;

        if (InputReader.PlayerInput != null)
            return InputReader.PlayerInput.currentControlScheme ?? string.Empty;

        return string.Empty;
    }

    private void RefreshIcon()
    {
        if (sharedIconSet == null || targetImage == null)
            return;

        bool useGamepad = deviceMode == DeviceMode.Gamepad;
        if (deviceMode == DeviceMode.Auto)
            useGamepad = sharedIconSet.IsGamepadScheme(GetCurrentScheme());

        if (useCraneMoveIcon)
        {
            string partName = GetCranePartName(craneDirection);
            if (sharedIconSet.TryGetCompositePartIcon(action, useGamepad, partName, out Sprite craneIcon, out _))
            {
                targetImage.sprite = craneIcon;
                targetImage.enabled = true;
            }
            else if (hideWhenMissing)
            {
                targetImage.enabled = false;
            }

            return;
        }

        if (sharedIconSet.TryGetIcon(action, useGamepad, out Sprite icon, out _))
        {
            targetImage.sprite = icon;
            Debug.Log($"Found icon for action {action}: {icon.name}");
            targetImage.enabled = true;
        }
        else if (hideWhenMissing)
        {
            targetImage.enabled = false;
        }
    }

    private static string GetCranePartName(CraneDirection direction)
    {
        switch (direction)
        {
            case CraneDirection.Back:
                return "down";
            case CraneDirection.Left:
                return "left";
            case CraneDirection.Right:
                return "right";
            default:
                return "up";
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (!Application.isPlaying)
            return;

        RefreshIcon();
    }
#endif
}
