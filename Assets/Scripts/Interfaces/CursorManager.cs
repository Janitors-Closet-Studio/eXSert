using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Singletons;

/*
Written by Kyle Woo
Manages the cursor visibility and lock state based on the current input scheme and action map.
*/

[DisallowMultipleComponent]
public class CursorManager : Singleton<CursorManager>
{
    [SerializeField] private string[] uiActionMapNames = new[] { "UI", "Menu" };
    [SerializeField] private string loadingActionMapName = "Loading";
    [SerializeField] private string[] keyboardMouseSchemeNames = new[] { "Keyboard&Mouse", "KeyboardMouse" };
    [SerializeField] private string[] forceShowCursorScenes = new[] { "MainMenu" };
    [SerializeField] private CursorLockMode lockModeWhenHidden = CursorLockMode.Locked;

    private static bool forceHidden;

    private PlayerInput playerInput;
    private string lastMap;
    private string lastScene;
    private string lastScheme;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    public static void SetForceHidden(bool hidden)
    {
        forceHidden = hidden;
        if (!isApplicationQuitting && Instance != null)
            Instance.ApplyCursorPolicy();
    }

    public static void RefreshPolicy()
    {
        if (isApplicationQuitting || Instance == null)
            return;

        Instance.RefreshPlayerInputBinding();
        Instance.OnControlsOrMapPossiblyChanged();
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshPlayerInputBinding();
        CacheState();
        ApplyCursorPolicy();
    }

    protected override void OnDestroy()
    {
        if (playerInput != null)
            playerInput.onControlsChanged -= HandleControlsChanged;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        base.OnDestroy();
    }

    private void Update()
    {
        RefreshPlayerInputBinding();

        // Detect manual SwitchCurrentActionMap calls
        string mapName = playerInput != null && playerInput.currentActionMap != null ? playerInput.currentActionMap.name : string.Empty;
        string sceneName = SceneManager.GetActiveScene().name;
        string schemeName = playerInput != null ? playerInput.currentControlScheme : string.Empty;
        if (!string.Equals(mapName, lastMap, System.StringComparison.Ordinal)
            || !string.Equals(sceneName, lastScene, System.StringComparison.Ordinal)
            || !string.Equals(schemeName, lastScheme, System.StringComparison.Ordinal))
            OnControlsOrMapPossiblyChanged();
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        OnControlsOrMapPossiblyChanged();
    }

    private void HandleControlsChanged(PlayerInput _)
    {
        OnControlsOrMapPossiblyChanged();
    }

    private void OnControlsOrMapPossiblyChanged()
    {
        CacheState();
        ApplyCursorPolicy();
    }

    private void CacheState()
    {
        lastMap = playerInput != null && playerInput.currentActionMap != null ? playerInput.currentActionMap.name : string.Empty;
        lastScene = SceneManager.GetActiveScene().name;
        lastScheme = playerInput != null ? playerInput.currentControlScheme : string.Empty;
    }

    private void RefreshPlayerInputBinding()
    {
        PlayerInput currentPlayerInput = InputReader.PlayerInput;
        if (playerInput == currentPlayerInput)
            return;

        if (playerInput != null)
            playerInput.onControlsChanged -= HandleControlsChanged;

        playerInput = currentPlayerInput;

        if (playerInput != null)
            playerInput.onControlsChanged += HandleControlsChanged;
    }

    private void ApplyCursorPolicy()
    {
        if (forceHidden)
        {
            HideCursor();
            return;
        }

        bool onKeyboardMouse = IsMatch(lastScheme, keyboardMouseSchemeNames);

        if (IsSceneForcedVisible())
        {
            if (onKeyboardMouse)
                ShowCursor();
            else
                HideCursor();
            return;
        }

        bool inLoading = !string.IsNullOrEmpty(loadingActionMapName)
            && string.Equals(lastMap, loadingActionMapName, System.StringComparison.OrdinalIgnoreCase);

        if (inLoading)
        {
            HideCursor();
            return;
        }

        if (onKeyboardMouse && IsMatch(lastMap, uiActionMapNames))
            ShowCursor();
        else
            HideCursor();
    }

    private static bool IsMatch(string value, string[] candidates)
    {
        if (string.IsNullOrEmpty(value) || candidates == null)
            return false;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate))
                continue;
            if (string.Equals(value, candidate, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsSceneForcedVisible()
    {
        if (forceShowCursorScenes == null || forceShowCursorScenes.Length == 0)
            return false;

        string sceneName = SceneManager.GetActiveScene().name;
        return IsMatch(sceneName, forceShowCursorScenes);
    }

    public void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = lockModeWhenHidden;
    }
}
