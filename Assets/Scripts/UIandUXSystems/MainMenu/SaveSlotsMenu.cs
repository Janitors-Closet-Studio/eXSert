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
    #region Inspector References
    [Header("Menu Navigation")]
    [SerializeField] private MainMenu mainMenu;

    [SerializeField] private Button backButton;
    [SerializeField] private Button playButton;

    [SerializeField] internal SaveSlots currentSaveSlotSelected = null;

    [SerializeField, Tooltip("The first level to be loaded when a player starts a new game")]
    private SceneAsset firstLevel = "Elevator";
    #endregion

    private SaveSlots[] saveSlots;

    private bool isLoadingGame = false;
    private bool hasStartedSceneTransition = false;

    private void Awake() => EnsureReferences();

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

        if (hasStartedSceneTransition) return;
        hasStartedSceneTransition = true;
        playButton.interactable = false; // Prevent multiple clicks

        DisableMenuButtons();

        // Ensure a slot is selected; if not, pick a sensible default
        if (currentSaveSlotSelected == null)
        {
            // Try to auto-select the first valid slot
            var profiles = DataPersistenceManager.Instance != null
                ? DataPersistenceManager.GetAllProfilesGameData()
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

        if (DataPersistenceManager.Instance == null)
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

        DataPersistenceManager.ChangeSelectedProfileId(selectedProfileId);

        if (isLoadingGame) LoadGame();
        else StartNewGame();
    }

    private void StartNewGame()
    {
        DataPersistenceManager.NewGame();

        // Potentially consider adding the ability to reset progress here

        SceneAsset.LoadIntoGame(firstLevel);
    }

    private void LoadGame()
    {
        DataPersistenceManager.LoadGame();

        // Get checkpoint from the loaded profile's game data
        SceneAsset savedScene = DataPersistenceManager.GetLastSavedScene();

        // Get the profile data we just loaded
        Dictionary<string, GameData> profilesGameData = DataPersistenceManager.GetAllProfilesGameData();

        if (profilesGameData.TryGetValue(currentSaveSlotSelected.GetProfileId(), out GameData loadedData) 
            && !string.IsNullOrEmpty(loadedData.currentSceneName)) savedScene = loadedData.currentSceneName;

        savedScene = ResolveLoadableSceneOrFallback(savedScene, firstLevel);

        SceneAsset.LoadIntoGame(savedScene);
    }

    private static SceneAsset ResolveLoadableSceneOrFallback(SceneAsset scene, SceneAsset fallbackScene)
    {
        if (scene != null && Application.CanStreamedLevelBeLoaded(scene)) return scene;

        if (fallbackScene != null && Application.CanStreamedLevelBeLoaded(fallbackScene)) return fallbackScene;

        Debug.LogError($"Neither the saved scene '{scene}' nor the fallback scene '{fallbackScene}' could be loaded. Check that they are included in the build settings and that the saved scene name is correct.");
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
        DataPersistenceManager.DeleteProfile(profileId);

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

        if (DataPersistenceManager.Instance == null)
            return;
        

        Dictionary<string, GameData> profilesGameData = DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>();

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



