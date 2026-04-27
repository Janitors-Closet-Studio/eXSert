/*
    Written by Brandon Wahl

    This script will handle the functionality of the warning buttons in the pause menu
*/


using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using Progression.Checkpoints;
using Unity.VisualScripting;

/// <summary>
/// Centralizes the warning prompt logic so buttons no longer need to wire up multiple GameObject.SetActive calls.
/// </summary>
public class WarningButtonFunctionality : MonoBehaviour
{
    private enum WarningAction
    {
        None,
        RestartCheckpoint,
        ReturnToMainMenu,
        QuitGame
    }

    [Header("UI References")]
    [SerializeField] private GameObject warningCanvas;
    [SerializeField] private GameObject overlayCanvas;
    [SerializeField] private GameObject confirmIcon;
    [SerializeField] private GameObject rejectIcon;
    [SerializeField] private GameObject checkpointText;
    [SerializeField] private GameObject quitText;
    [SerializeField] private GameObject returnToMenuText;

    // When in the menu the footer panel will be parented to the pausemenu so itll be overlaid properly
    [SerializeField] private GameObject footerPanel;
    [SerializeField] private GameObject pauseMenu;

    private GameObject originalFooterParent;
    private bool ownsFooterParenting;

    [Header("Action Handler")]
    [SerializeField] private GameActionHandler actionHandler;

    private WarningAction pendingAction = WarningAction.None;

    private void Awake()
    {
        if (footerPanel != null && footerPanel.transform.parent != null)
            originalFooterParent = footerPanel.transform.parent.gameObject;
    }


    /// <summary>
    /// Existing buttons still call this via OnClick. It now simply delegates to the new confirm handler.
    /// </summary>
    public void WhichFunctionToCarryOut()
    {
        Debug.Log("[WarningButtonFunctionality] WhichFunctionToCarryOut called - delegating to OnConfirmPressed.");
        OnConfirmPressed();
    }

    public void ShowCheckpointWarning()
    {
        PrepareWarning(WarningAction.RestartCheckpoint, checkpointText);
    }

    public void ShowReturnToMenuWarning()
    {
        PrepareWarning(WarningAction.ReturnToMainMenu, returnToMenuText);
    }

    public void ShowQuitWarning()
    {
        PrepareWarning(WarningAction.QuitGame, quitText);
    }

    public void OnConfirmPressed()
    {
        Debug.Log("[WarningButtonFunctionality] OnConfirmPressed called - START");
        var actionToRun = ResolvePendingAction();
        Debug.Log($"[WarningButtonFunctionality] OnConfirmPressed - Resolved action: {actionToRun}");
        if (actionToRun == WarningAction.None)
        { 
            Debug.LogError("[WarningButtonFunctionality] OnConfirmPressed - ERROR: Confirm pressed but no pending action or active warning text found.");
            return; 
        }

        Debug.Log("[WarningButtonFunctionality] OnConfirmPressed - Action is valid, calling HideWarningUI...");
        HideWarningUI();
        Debug.Log("[WarningButtonFunctionality] OnConfirmPressed - HideWarningUI completed, now calling ExecuteAction...");
        ExecuteAction(actionToRun);
        Debug.Log("[WarningButtonFunctionality] OnConfirmPressed - ExecuteAction completed - END");
    }

    public void OnBackPressed()
    {
        HideWarningUI();
    }

    public void ForceHideWarningUI()
    {
        HideWarningUI();
    }

    private void PrepareWarning(WarningAction action, GameObject textToEnable)
    {
        pendingAction = action;
        ActivateTextBlock(textToEnable);
        SetWarningVisible(true);
        ownsFooterParenting = ParentFooterToPauseMenu(true);
    }

    private void ActivateTextBlock(GameObject target)
    {
        if (checkpointText != null)
            checkpointText.SetActive(target == checkpointText);

        if (returnToMenuText != null)
            returnToMenuText.SetActive(target == returnToMenuText);

        if (quitText != null)
            quitText.SetActive(target == quitText);
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningCanvas != null)
            warningCanvas.SetActive(visible);

        if (overlayCanvas != null)
            overlayCanvas.SetActive(visible);

        if (confirmIcon != null)
            confirmIcon.SetActive(visible);

        if (rejectIcon != null)
            rejectIcon.SetActive(visible);
    }

    private void HideWarningUI()
    {
        Debug.Log("[WarningButtonFunctionality] HideWarningUI called");
        pendingAction = WarningAction.None;
        ActivateTextBlock(null);
        SetWarningVisible(false);

        if (ownsFooterParenting)
        {
            ParentFooterToPauseMenu(false);
            ownsFooterParenting = false;
        }

        Debug.Log("[WarningButtonFunctionality] HideWarningUI completed");
    }

    private bool ParentFooterToPauseMenu(bool parentToPause)
    {
        if (footerPanel == null)
            return false;

        if (parentToPause) 
        {
            if (pauseMenu == null)
                return false;

            Transform currentParent = footerPanel.transform.parent;
            if (currentParent != null && currentParent != pauseMenu.transform)
                originalFooterParent = currentParent.gameObject;

            footerPanel.transform.SetParent(pauseMenu.transform, worldPositionStays: false);
            footerPanel.transform.SetSiblingIndex(1); // Above pause ui but below warning canvas
            return true;
        }

        Transform restoreParent = null;

        if (originalFooterParent != null && (pauseMenu == null || originalFooterParent != pauseMenu))
            restoreParent = originalFooterParent.transform;
        else if (warningCanvas != null && warningCanvas.transform.parent != null)
            restoreParent = warningCanvas.transform.parent;

        if (restoreParent != null)
            footerPanel.transform.SetParent(restoreParent, worldPositionStays: false);

        if (footerPanel.transform.parent != null)
            footerPanel.transform.SetAsLastSibling(); // Ensure footer is on top of other UI elements in the warning canvas

        return restoreParent != null;
    }

    private void OnDisable()
    {
        if (ownsFooterParenting)
        {
            ParentFooterToPauseMenu(false);
            ownsFooterParenting = false;
        }
    }

    private WarningAction ResolvePendingAction()
    {
        if (pendingAction != WarningAction.None)
            return pendingAction;

        if (checkpointText != null && checkpointText.activeInHierarchy)
            return WarningAction.RestartCheckpoint;

        if (returnToMenuText != null && returnToMenuText.activeInHierarchy)
            return WarningAction.ReturnToMainMenu;

        if (quitText != null && quitText.activeInHierarchy)
            return WarningAction.QuitGame;

        return WarningAction.None;
    }

    private MusicBox FindSceneMusicBox()
    {
        MusicBox musicBox;
        string foundSceneName;

        List<string> additiveScenes =  new List<string>()
        {
            "Elevator",
            "CargoBay",
            "Hangar",
            "CrewQuarters",
            "Boss"
        };
        List<string> loadedAdditiveScenes = new List<string>();

        foreach (var sceneName in additiveScenes)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                loadedAdditiveScenes.Add(sceneName);
            }
        }

        if(loadedAdditiveScenes.Count == 0)
        {
            Debug.LogWarning("[WarningButtonFunctionality] No additive scenes loaded. Cannot find MusicBox.");
            return null;
        } 
        else
        {
            foundSceneName = loadedAdditiveScenes[0];
        }

        string musicBoxName = foundSceneName + "MusicBox";
        GameObject musicBoxObj = GameObject.Find(musicBoxName);
        if (musicBoxObj == null)
        {
            Debug.LogWarning($"[WarningButtonFunctionality] MusicBox object '{musicBoxName}' not found in scene. Cannot fade music.");
            return null;
        }
        musicBox = musicBoxObj.GetComponent<MusicBox>();
        

        return musicBox;

    }

    private void FadeOutLevelMusic()
    {
        Debug.Log("[WarningButtonFunctionality] FadeOutLevelMusic called - searching for MusicBox...");
        MusicBox musicBox = FindSceneMusicBox();
        if (musicBox != null)
        {
            Debug.Log("[WarningButtonFunctionality] FadeOutLevelMusic - MusicBox found, starting fade coroutines.");
            StopCoroutine(musicBox.FadeOutMusic(1f));
            StopCoroutine(musicBox.FadeOutAmbience(1f));
            Debug.Log("[WarningButtonFunctionality] Fading out music and ambience.");
            Debug.Log("Music Box reference: " + musicBox);
            musicBox.StartCoroutine(musicBox.FadeOutMusic(1f));
            musicBox.StartCoroutine(musicBox.FadeOutAmbience(1f));
        }
        else
        {
            Debug.LogWarning("[WarningButtonFunctionality] FadeOutLevelMusic - No MusicBox found in scene to fade out music.");
        }
        Debug.Log("[WarningButtonFunctionality] FadeOutLevelMusic completed.");
    }

    private void ExecuteAction(WarningAction action)
    {
        Debug.Log($"[WarningButtonFunctionality] Executing action: {action}");

        if (RumbleManager.Instance != null)
            RumbleManager.Instance.StopControllerRumble();

        if (PauseManager.Instance != null && PauseManager.Instance.pauseOverlay != null)
            PauseManager.Instance.pauseOverlay.SetActive(false);

        SceneAsset sceneAsset = SceneAsset.GetSceneAssetOfObject(this.gameObject);
        MasterObjectiveClass masterObjective = sceneAsset != null
            ? MasterObjectiveClass.GetInstance(sceneAsset)
            : FindFirstObjectByType<MasterObjectiveClass>(FindObjectsInactive.Include);
        if (masterObjective != null)
            masterObjective.CancelCurrentCollectNotice(turnOffUI: true);

        switch (action)
        {
            case WarningAction.RestartCheckpoint:
                Debug.Log("[WarningButtonFunctionality] RestartCheckpoint: Starting action execution.");
                PrepareForSceneTransition();
                Debug.Log("[WarningButtonFunctionality] RestartCheckpoint: PrepareForSceneTransition called.");
                FadeOutLevelMusic();
                Debug.Log("[WarningButtonFunctionality] RestartCheckpoint: FadeOutLevelMusic called. Now resolving handler...");
                GameActionHandler restartHandler = ResolveActionHandler();
                Debug.Log($"[WarningButtonFunctionality] RestartCheckpoint: Handler resolved: {(restartHandler != null ? "Found" : "Null")}. About to execute RestartFromCheckpoint.");
                if (restartHandler != null)
                {
                    Debug.Log("[WarningButtonFunctionality] RestartCheckpoint: Calling restartHandler.RestartFromCheckpoint().");
                    restartHandler.RestartFromCheckpoint();
                    Debug.Log("[WarningButtonFunctionality] RestartCheckpoint: restartHandler.RestartFromCheckpoint() completed.");
                }
                else
                {
                    Debug.Log("[WarningButtonFunctionality] RestartCheckpoint: Handler null, calling Player.TriggerRespawn() fallback.");
                    Player.TriggerRespawn();
                    Debug.Log("[WarningButtonFunctionality] RestartCheckpoint: Player.TriggerRespawn() completed.");
                }
                break;

            case WarningAction.ReturnToMainMenu:
                Debug.Log("[WarningButtonFunctionality] ReturnToMainMenu: Starting action execution.");
                PrepareForSceneTransition();
                Debug.Log("[WarningButtonFunctionality] ReturnToMainMenu: PrepareForSceneTransition called.");
                FadeOutLevelMusic();
                Debug.Log("[WarningButtonFunctionality] ReturnToMainMenu: FadeOutLevelMusic called. Now resolving handler...");
                GameActionHandler handler = ResolveActionHandler();
                Debug.Log($"[WarningButtonFunctionality] ReturnToMainMenu: Handler resolved: {(handler != null ? "Found" : "Null")}. About to execute ReturnToMainMenu.");
                if (handler != null)
                {
                    Debug.Log("[WarningButtonFunctionality] ReturnToMainMenu: Calling handler.ReturnToMainMenu().");
                    handler.ReturnToMainMenu();
                    Debug.Log("[WarningButtonFunctionality] ReturnToMainMenu: handler.ReturnToMainMenu() completed.");
                }
                else
                {
                    Debug.Log("[WarningButtonFunctionality] ReturnToMainMenu: Handler null, calling SceneLoader.LoadMainMenu() fallback.");
                    SceneLoader.LoadMainMenu();
                    Debug.Log("[WarningButtonFunctionality] ReturnToMainMenu: SceneLoader.LoadMainMenu() completed.");
                }
                break;

            case WarningAction.QuitGame:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;

            default:
                Debug.LogWarning("[WarningButtonFunctionality] Unknown warning action requested.");
                break;
        }
    }

    private GameActionHandler ResolveActionHandler()
    {
        if (actionHandler != null)
            return actionHandler;

        actionHandler = FindFirstObjectByType<GameActionHandler>(FindObjectsInactive.Include);
        return actionHandler;
    }

    private void PrepareForSceneTransition()
    {
        PauseManager pauseManager = PauseManager.Instance;
        if (pauseManager != null)
        {
            pauseManager.HideMenusForSceneTransition();
            return;
        }

        FooterManager footerManager = FindFirstObjectByType<FooterManager>(FindObjectsInactive.Include);
        footerManager?.UpdateFooterForMenu(null);
    }
}
