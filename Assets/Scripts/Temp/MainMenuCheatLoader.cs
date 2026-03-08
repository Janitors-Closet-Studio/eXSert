using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenuCheatLoader : MonoBehaviour
{
    [System.Serializable]
    private class CheatEntry
    {
        public KeyCode key = KeyCode.Alpha1;
        public string sceneName = "";
        public string spawnPointId = "default";
    }

    [Header("Activation")]
    [SerializeField] private bool requireCtrl = true;
    [SerializeField] private bool requireShift = true;
    [SerializeField] private bool onlyFromMainMenu = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;
    [SerializeField] private float debugStatusIntervalSeconds = 2f;

    [Header("Cheat Targets")]
    [SerializeField] private List<CheatEntry> entries = new()
    {
        new CheatEntry { key = KeyCode.Alpha1, sceneName = "CargoBay", spawnPointId = "default" },
        new CheatEntry { key = KeyCode.Alpha2, sceneName = "CrewQuarters", spawnPointId = "default" },
        new CheatEntry { key = KeyCode.Alpha3, sceneName = "Hangar", spawnPointId = "default" },
        new CheatEntry { key = KeyCode.Alpha4, sceneName = "FinalBoss", spawnPointId = "default" }
    };

    private float _nextDebugStatusTime;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (onlyFromMainMenu && !IsMainMenuActive())
        {
            DebugStatus("Blocked: not in main menu", keyboard);
            return;
        }

        if (requireCtrl && !(keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed))
        {
            DebugStatus("Blocked: Ctrl not held", keyboard);
            return;
        }

        if (requireShift && !(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
        {
            DebugStatus("Blocked: Shift not held", keyboard);
            return;
        }

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.sceneName))
                continue;

            if (IsKeyPressedThisFrame(keyboard, entry.key))
            {
                if (debugLogging)
                    Debug.Log($"[MainMenuCheatLoader] Triggered {entry.key}: loading '{entry.sceneName}' (spawn '{entry.spawnPointId}')", this);
                LoadTarget(entry);
                return;
            }
        }
    }

    private bool IsMainMenuActive()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            return true;

        return string.Equals(SceneManager.GetActiveScene().name, mainMenuSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadTarget(CheatEntry entry)
    {
        if (!Application.CanStreamedLevelBeLoaded(entry.sceneName))
        {
            Debug.LogError($"[MainMenuCheatLoader] Scene '{entry.sceneName}' cannot be loaded. Verify the scene name matches exactly and that it is added to Build Settings.", this);
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadInitialGameScene(
                entry.sceneName,
                additiveSceneName: null,
                pauseUntilLoaded: false,
                spawnPointIdOverride: entry.spawnPointId,
                updateCheckpointAfterLoad: false);
            return;
        }

        SceneManager.LoadScene(entry.sceneName, LoadSceneMode.Single);
    }

    private static bool IsKeyPressedThisFrame(Keyboard keyboard, KeyCode key)
    {
        return key switch
        {
            KeyCode.Alpha0 => keyboard.digit0Key.wasPressedThisFrame,
            KeyCode.Alpha1 => keyboard.digit1Key.wasPressedThisFrame,
            KeyCode.Alpha2 => keyboard.digit2Key.wasPressedThisFrame,
            KeyCode.Alpha3 => keyboard.digit3Key.wasPressedThisFrame,
            KeyCode.Alpha4 => keyboard.digit4Key.wasPressedThisFrame,
            KeyCode.Alpha5 => keyboard.digit5Key.wasPressedThisFrame,
            KeyCode.Alpha6 => keyboard.digit6Key.wasPressedThisFrame,
            KeyCode.Alpha7 => keyboard.digit7Key.wasPressedThisFrame,
            KeyCode.Alpha8 => keyboard.digit8Key.wasPressedThisFrame,
            KeyCode.Alpha9 => keyboard.digit9Key.wasPressedThisFrame,

            KeyCode.Keypad0 => keyboard.numpad0Key.wasPressedThisFrame,
            KeyCode.Keypad1 => keyboard.numpad1Key.wasPressedThisFrame,
            KeyCode.Keypad2 => keyboard.numpad2Key.wasPressedThisFrame,
            KeyCode.Keypad3 => keyboard.numpad3Key.wasPressedThisFrame,
            KeyCode.Keypad4 => keyboard.numpad4Key.wasPressedThisFrame,
            KeyCode.Keypad5 => keyboard.numpad5Key.wasPressedThisFrame,
            KeyCode.Keypad6 => keyboard.numpad6Key.wasPressedThisFrame,
            KeyCode.Keypad7 => keyboard.numpad7Key.wasPressedThisFrame,
            KeyCode.Keypad8 => keyboard.numpad8Key.wasPressedThisFrame,
            KeyCode.Keypad9 => keyboard.numpad9Key.wasPressedThisFrame,
            _ => false
        };
    }

    private void DebugStatus(string message, Keyboard keyboard)
    {
        if (!debugLogging)
            return;

        if (Time.unscaledTime < _nextDebugStatusTime)
            return;

        _nextDebugStatusTime = Time.unscaledTime + Mathf.Max(0.1f, debugStatusIntervalSeconds);

        string activeSceneName = SceneManager.GetActiveScene().name;
        bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        Debug.Log($"[MainMenuCheatLoader] {message}. ActiveScene='{activeSceneName}', requireCtrl={requireCtrl} (held={ctrlHeld}), requireShift={requireShift} (held={shiftHeld}), onlyFromMainMenu={onlyFromMainMenu} (mainMenuSceneName='{mainMenuSceneName}')", this);
    }
}
