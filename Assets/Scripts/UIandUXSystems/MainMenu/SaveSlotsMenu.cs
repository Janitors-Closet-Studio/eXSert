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

    [SerializeField] private string DefaultSceneName = "VS_Elevator";

    private bool isLoadingGame = false;
    private bool hasStartedSceneTransition = false;

    private void Awake()
    {
        saveSlots = this.GetComponentsInChildren<SaveSlots>();
    }

    /// <summary>
    /// When a save slot is clicked, it gathers the profile Id and loads the proper data.
    /// Uses new SceneLoader system for proper scene management.
    /// </summary>
    public void OnSaveSlotClicked()
    {
        if (hasStartedSceneTransition)
            return;

        hasStartedSceneTransition = true;
        playButton.interactable = false; // Prevent multiple clicks

        DisableMenuButtons();

        // Safety check for SceneLoader
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[SaveSlotsMenu] SceneLoader not found! Please add SceneLoader GameObject to the Main Menu scene.");
            hasStartedSceneTransition = false;
            return;
        }

        // Ensure a slot is selected; if not, pick a sensible default
        if (currentSaveSlotSelected == null)
        {
            // Try to auto-select the first valid slot
            var profiles = DataPersistenceManager.instance.GetAllProfilesGameData();
            SaveSlots fallback = null;
            if (isLoadingGame)
            {
                // Prefer a slot that actually has data when loading a game
                foreach (var slot in saveSlots)
                {
                    GameData data;
                    if (profiles.TryGetValue(slot.GetProfileId(), out data) && data != null)
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
                Debug.LogError("[SaveSlotsMenu] No save slots available to select.");
                hasStartedSceneTransition = false;
                return;
            }
        }

        DataPersistenceManager.instance.ChangeSelectedProfileId(currentSaveSlotSelected.GetProfileId());

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

            string primaryScene = configuredDefault;
            string secondaryScene = configuredGameplay;

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

    /// <summary>
    /// Deletes the currently selected save slot's data from disk and refreshes the UI list.
    /// Wire this to the Delete button's OnClick.
    /// </summary>
    public void OnDeleteSaveClicked()
    {
        if (currentSaveSlotSelected == null)
        {
            Debug.LogWarning("[SaveSlotsMenu] No save slot selected to delete.");
            return;
        }

        string profileId = currentSaveSlotSelected.GetProfileId();
        if (string.IsNullOrEmpty(profileId))
        {
            Debug.LogWarning("[SaveSlotsMenu] Selected slot has no profile id.");
            return;
        }

        // Delete save file for this slot
        DataPersistenceManager.instance.DeleteProfile(profileId);

        // Refresh displayed slots
        currentSaveSlotSelected = null;
        ActivateMenu(isLoadingGame);
    }

    //When the back button is click it activates the main menu again
    public void OnBackClicked()
    {
        mainMenu.ActivateMenu();
        this.DeactivateMenu();
    }

    //Activates the main menu when called
    public void ActivateMenu(bool isLoadingGame)
    {
        this.gameObject.SetActive(true);

        this.isLoadingGame = isLoadingGame;

        GameObject firstSelected = backButton.gameObject;

        Dictionary<string, GameData> profilesGameData = DataPersistenceManager.instance.GetAllProfilesGameData();

        //Disables and enables interactability of save slots depending if there is data attached to the profile Id
        foreach (SaveSlots saveSlot in saveSlots)
        {
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
        foreach(SaveSlots saveSlot in saveSlots)
        {
            saveSlot.SetInteractable(false); 
        }
        backButton.gameObject.SetActive(false);
    }

    //Disables main menu
    public void DeactivateMenu()
    {
        this.gameObject.SetActive(false);
    }
}



