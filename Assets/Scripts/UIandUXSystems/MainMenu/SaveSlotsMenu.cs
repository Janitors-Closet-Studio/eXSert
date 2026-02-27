/*
Written by Brandon Wahl
Updated to work with SceneLoader and CheckpointSystem

Handles the save slot menu and the actions of the buttons clicked

*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveSlotsMenu : Menu
{
    [Header("Menu Navigation")]
    [SerializeField] private MainMenu mainMenu;

    [SerializeField] private Button backButton;
    [SerializeField] private Button playButton;

    private SaveSlots[] saveSlots;

    [SerializeField] internal SaveSlots currentSaveSlotSelected = null;

    [SerializeField] private string SceneName = "PlayerScene";

    // Must match a scene name included in Build Settings.
    [SerializeField] private string DefaultSceneName = "Elevator";

    private bool isLoadingGame = false;
    private bool hasStartedSceneTransition = false;

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        // Slots
        if (saveSlots == null || saveSlots.Length == 0)
        {
            saveSlots = GetComponentsInChildren<SaveSlots>(true);
        }

        // Menu owner
        if (mainMenu == null)
        {
            mainMenu = FindAnyObjectByType<MainMenu>();
        }

        // Buttons (try to recover from missing inspector wiring)
        if (backButton == null || playButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (backButton == null)
                backButton = FindButtonByNameContains(buttons, "back");
            if (playButton == null)
                playButton = FindButtonByNameContains(buttons, "play");
        }
    }

    private static Button FindButtonByNameContains(Button[] buttons, string token)
    {
        if (buttons == null || string.IsNullOrWhiteSpace(token))
            return null;

        token = token.Trim();
        foreach (var button in buttons)
        {
            if (button == null) continue;
            if (button.name != null && button.name.ToLowerInvariant().Contains(token))
                return button;
        }
        return null;
    }

    /// <summary>
    /// When a save slot is clicked, it gathers the profile Id and loads the proper data.
    /// Uses new SceneLoader system for proper scene management.
    /// </summary>
    public void OnSaveSlotClicked()
    {
        EnsureReferences();

        if (hasStartedSceneTransition)
            return;

        hasStartedSceneTransition = true;
        if (playButton != null)
            playButton.interactable = false; // Prevent multiple clicks

        DisableMenuButtons();

        // Safety check for SceneLoader
        if (SceneLoader.Instance == null)
        {
            hasStartedSceneTransition = false;
            return;
        }

        // Ensure a slot is selected; if not, pick a sensible default
        if (currentSaveSlotSelected == null)
        {
            // Try to auto-select the first valid slot
            var profiles = DataPersistenceManager.instance != null
                ? DataPersistenceManager.instance.GetAllProfilesGameData()
                : null;
            SaveSlots fallback = null;
            if (isLoadingGame)
            {
                // Prefer a slot that actually has data when loading a game
                foreach (var slot in saveSlots)
                {
                    if (slot == null) continue;
                    GameData data;
                    if (profiles != null && profiles.TryGetValue(slot.GetProfileId(), out data) && data != null)
                    {
                        fallback = slot;
                        break;
                    }
                }
            }
            // If still null, just take the first slot in the UI
            if (fallback == null && saveSlots != null && saveSlots.Length > 0)
            {
                fallback = saveSlots[0];
            }

            if (fallback != null)
            {
                currentSaveSlotSelected = fallback;
            }
            else
            {
                RestoreMenuButtons();
                hasStartedSceneTransition = false;
                return;
            }
        }

        if (DataPersistenceManager.instance == null)
        {
            RestoreMenuButtons();
            hasStartedSceneTransition = false;
            return;
        }

        string selectedProfileId = currentSaveSlotSelected != null ? currentSaveSlotSelected.GetProfileId() : null;
        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            RestoreMenuButtons();
            hasStartedSceneTransition = false;
            return;
        }

        DataPersistenceManager.instance.ChangeSelectedProfileId(selectedProfileId);

        if (!isLoadingGame)
        { 
            // NEW GAME - Create fresh save data
            DataPersistenceManager.instance.NewGame();
            if (CheckpointSystem.Instance != null)
            {
                CheckpointSystem.Instance.ResetProgress();
            }

            string configuredGameplay = string.IsNullOrWhiteSpace(SceneName) ? "PlayerScene" : SceneName;
            string configuredDefault = string.IsNullOrWhiteSpace(DefaultSceneName) ? configuredGameplay : DefaultSceneName;

            string primaryScene = ResolveLoadableSceneOrFallback(configuredDefault, "Elevator");
            string secondaryScene = ResolveLoadableSceneOrFallback(configuredGameplay, null);

            if (string.IsNullOrWhiteSpace(primaryScene))
            {
                RestoreMenuButtons();
                hasStartedSceneTransition = false;
                return;
            }

            // Secondary scene is optional; only load if valid.
            if (string.IsNullOrWhiteSpace(secondaryScene))
            {
                secondaryScene = null;
            }

            if (string.Equals(primaryScene, secondaryScene))
            {
                secondaryScene = null;
            }

            string spawnId = CheckpointSystem.Instance != null
                ? CheckpointSystem.Instance.GetCurrentSpawnPointID()
                : "default";

            // Pause gameplay while the default scene loads first, then the gameplay scene second
            SceneLoader.Instance.LoadInitialGameScene(
                primaryScene,
                secondaryScene,
                pauseUntilLoaded: true,
                spawnPointIdOverride: spawnId,
                updateCheckpointAfterLoad: true);
        }
        else
        {
            // LOAD GAME - Load existing save data first
            DataPersistenceManager.instance.LoadGame();
            
            // Get checkpoint from the loaded profile's game data
            string savedScene = DataPersistenceManager.instance.GetLastSavedScene();
            
            // Get the profile data we just loaded
            Dictionary<string, GameData> profilesGameData = DataPersistenceManager.instance.GetAllProfilesGameData();
            GameData loadedData = null;
            
            if (profilesGameData.TryGetValue(currentSaveSlotSelected.GetProfileId(), out loadedData))
            {
                if (!string.IsNullOrEmpty(loadedData.currentSceneName))
                {
                    savedScene = loadedData.currentSceneName;
                }
            }

            savedScene = ResolveLoadableSceneOrFallback(savedScene, "Elevator");
            if (string.IsNullOrWhiteSpace(savedScene))
            {
                RestoreMenuButtons();
                hasStartedSceneTransition = false;
                return;
            }

            string savedSpawnPoint = loadedData != null && !string.IsNullOrWhiteSpace(loadedData.currentSpawnPointID)
                ? loadedData.currentSpawnPointID
                : CheckpointSystem.Instance != null
                    ? CheckpointSystem.Instance.GetCurrentSpawnPointID()
                    : "default";
            
            // Load the saved checkpoint scene
            SceneLoader.Instance.LoadInitialGameScene(
                savedScene,
                additiveSceneName: null,
                pauseUntilLoaded: true,
                spawnPointIdOverride: savedSpawnPoint,
                updateCheckpointAfterLoad: false);
        }
    }

    private static string ResolveLoadableSceneOrFallback(string sceneName, string fallbackSceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
            return sceneName;

        if (!string.IsNullOrWhiteSpace(fallbackSceneName) && Application.CanStreamedLevelBeLoaded(fallbackSceneName))
            return fallbackSceneName;

        return null;
    }

    private void RestoreMenuButtons()
    {
        if (saveSlots != null)
        {
            foreach (SaveSlots saveSlot in saveSlots)
            {
                if (saveSlot != null)
                    saveSlot.SetInteractable(true);
            }
        }

        if (backButton != null)
            backButton.gameObject.SetActive(true);

        if (playButton != null)
            playButton.interactable = true;
    }

    /// <summary>
    /// Deletes the currently selected save slot's data from disk and refreshes the UI list.
    /// Wire this to the Delete button's OnClick.
    /// </summary>
    public void OnDeleteSaveClicked()
    {
        if (currentSaveSlotSelected == null)
            return;
        

        string profileId = currentSaveSlotSelected.GetProfileId();
        if (string.IsNullOrEmpty(profileId))
            return;


        // Delete save file for this slot
        DataPersistenceManager.instance.DeleteProfile(profileId);

        // Refresh displayed slots
        currentSaveSlotSelected = null;
        ActivateMenu(isLoadingGame);
    }

    //When the back button is click it activates the main menu again
    public void OnBackClicked()
    {
        EnsureReferences();
        if (mainMenu != null)
            mainMenu.ActivateMenu();

        this.DeactivateMenu();
    }

    //Activates the main menu when called
    public void ActivateMenu(bool isLoadingGame)
    {
        EnsureReferences();
        this.gameObject.SetActive(true);

        this.isLoadingGame = isLoadingGame;

        GameObject firstSelected = backButton != null ? backButton.gameObject : null;

        if (DataPersistenceManager.instance == null)
            return;
        

        Dictionary<string, GameData> profilesGameData = DataPersistenceManager.instance.GetAllProfilesGameData() ?? new Dictionary<string, GameData>();

        // Validate slot profile ids (common merge issue: ids cleared)
        if (saveSlots == null || saveSlots.Length == 0)
            return;

        var seenProfileIds = new HashSet<string>();
        foreach (var slot in saveSlots)
        {
            if (slot == null) continue;
            string id = slot.GetProfileId();
            if (string.IsNullOrWhiteSpace(id))
                continue;
        }

        //Disables and enables interactability of save slots depending if there is data attached to the profile Id
        foreach (SaveSlots saveSlot in saveSlots)
        {
            if (saveSlot == null) continue;
            GameData profileData = null;
            profilesGameData.TryGetValue(saveSlot.GetProfileId(), out profileData);
            saveSlot.SetData(profileData);
            if (profileData == null && isLoadingGame)
            {
                saveSlot.SetInteractable(false);
            }
            else
            {
                saveSlot.SetInteractable(true);
            }
        }

        // Ensure a default selection exists so Play works even if user doesn't click a slot first
        if (currentSaveSlotSelected == null)
        {
            SaveSlots defaultSlot = null;
            if (isLoadingGame)
            {
                // Prefer the first slot with data when loading
                foreach (var slot in saveSlots)
                {
                    GameData data;
                    if (profilesGameData.TryGetValue(slot.GetProfileId(), out data) && data != null)
                    {
                        defaultSlot = slot;
                        break;
                    }
                }
            }
            if (defaultSlot == null && saveSlots != null && saveSlots.Length > 0)
            {
                defaultSlot = saveSlots[0];
            }
            currentSaveSlotSelected = defaultSlot;
        }
    }

    //Makes it so when clicking buttons other buttons are noninteractable so no errors occur
    public void DisableMenuButtons()
    {
        EnsureReferences();
        if (saveSlots != null)
        {
            foreach (SaveSlots saveSlot in saveSlots)
            {
                if (saveSlot == null) continue;
                saveSlot.SetInteractable(false);
            }
        }

        if (backButton != null)
            backButton.gameObject.SetActive(false);
    }

    //Disables main menu
    public void DeactivateMenu()
    {
        this.gameObject.SetActive(false);
    }
}



