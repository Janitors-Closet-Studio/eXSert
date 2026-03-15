using UnityEngine;
using UnityEngine.InputSystem;
using Managers.TimeLord;

public class DebugMenu : Singletons.Singleton<DebugMenu>
{
    public override string ToString() => "Debug Menu";

    private static GameObject _debugMenu;

    private string previousActionMap;

    /// <summary>
    /// Tells Unity to automatically run this method as soon as the first scene loads.
    /// Needs the DebugMenu prefab to be located in a "Resources" folder named "Debug Menu Canvas".
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        // Only load if an instance doesn't already exist in the scene manually
        if (Instance == null) CreateInstance();
    }

    protected override void Awake()
    {
        base.Awake(); // Base singleton setup

        GameObject prefab = Resources.Load<GameObject>("Debug Menu Canvas");
        if (prefab == null)
        {
            Debug.LogWarning("[Debug Menu] Auto-create failed. Could not find a prefab named 'DebugMenu' in any Resources folder.");
            return;
        }

        if (_debugMenu == null) _debugMenu = Instantiate(prefab, Instance.gameObject.transform);
        else return;

        _debugMenu.name = prefab.name; // Remove "(Clone)" from the name for clarity in the hierarchy

        _debugMenu.SetActive(false); // Start with the debug menu hidden
    }

    private void Update()
    {
        if (Keyboard.current.backquoteKey.wasPressedThisFrame) ToggleMenu();
    }

    private void ToggleMenu()
    {
        if (_debugMenu == null)
        {
            Debug.LogWarning("[Debug Menu] Toggle failed. Debug menu prefab not found or instantiated.");
            return;
        }

        if (_debugMenu.activeSelf) CloseMenu();
        else OpenMenu();

        // Local Functions

        void OpenMenu()
        {
            if (InputReader.PlayerInput != null && InputReader.PlayerInput.currentActionMap != null)
                previousActionMap = InputReader.PlayerInput.currentActionMap.name;

            _debugMenu.SetActive(true);
            PauseCoordinator.RequestPause("DebugMenu");
            InputReader.inputBusy = true;

            // Switch to the UI action map to let CursorManager naturally reveal the mouse
            if (InputReader.PlayerInput != null)
                InputReader.PlayerInput.SwitchCurrentActionMap("UI");
        }

        void CloseMenu()
        {
            _debugMenu.SetActive(false);
            PauseCoordinator.ReleaseTimeScale("DebugMenu");
            InputReader.inputBusy = false;

            // Revert back into the previous action map (e.g. "Player" or "Gameplay") to let CursorManager hide the mouse
            if (InputReader.PlayerInput != null && !string.IsNullOrEmpty(previousActionMap))
                InputReader.PlayerInput.SwitchCurrentActionMap(previousActionMap);
        }
    }
}
