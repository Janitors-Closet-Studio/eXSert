/*
Written by Brandon Wahl
Updated to work with SceneLoader and CheckpointSystem

Handles the save slot menu and the actions of the buttons clicked

*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SaveSlotsMenu : Menu
{
    #region Inspector References
    [Header("Menu Navigation")]
    [SerializeField] private MainMenu mainMenu;

    [SerializeField] private Button backButton;
    [SerializeField] private Button playButton;

    [SerializeField] private Button loadButton;
    private bool isInLoadMenu = false;

    [SerializeField] internal SaveSlots currentSaveSlotSelected = null;

    [SerializeField, Tooltip("The first level to be loaded when a player starts a new game")]
    private SceneAsset firstLevel;

    [SerializeField] private TextMeshProUGUI actText;
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
        EnsureTheCorrectSaveSlotText();

    }

    public void GoingToLoadMenu(bool loadMenu)
    {
        isInLoadMenu = loadMenu;
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

        // Show farthest act unlocked for this profile in actText
        string selectedProfileId = currentSaveSlotSelected != null ? currentSaveSlotSelected.GetProfileId() : null;
        if (!string.IsNullOrWhiteSpace(selectedProfileId) && actText != null)
        {
            string farthestAct = GetFarthestUnlockedActName(selectedProfileId);
            actText.text = string.IsNullOrEmpty(farthestAct) ? "No Act Unlocked" : farthestAct;
        }

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

        selectedProfileId = currentSaveSlotSelected != null ? currentSaveSlotSelected.GetProfileId() : null;
        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            RestoreMenuButtons();
            hasStartedSceneTransition = false;
            return;
        }

        DataPersistenceManager.ChangeSelectedProfileId(selectedProfileId);

        LoadMusicScene();

        if (isLoadingGame) LoadGame();
        else StartNewGame();
    }

    // Helper to get the farthest unlocked act name for a profile
    private string GetFarthestUnlockedActName(string profileId)
    {
        var actManager = FindAnyObjectByType<ActsManager>();
        if (actManager != null)
        {
            return actManager.GetFarthestUnlockedActName(profileId);
        }
        return null;
    }

    
    public void EnableAllSaveSlots()
    {
        if (saveSlots == null) return;

        foreach (SaveSlots slot in saveSlots)
        {
            if (slot != null)
                slot.SetInteractable(true);
        }
    }


    private void StartNewGame()
    {
        // Always set the selected profile ID to the current slot before starting a new game
        if (currentSaveSlotSelected != null)
        {
            string selectedProfileId = currentSaveSlotSelected.GetProfileId();
            if (!string.IsNullOrWhiteSpace(selectedProfileId))
            {
                DataPersistenceManager.ChangeSelectedProfileId(selectedProfileId);
            }
        }

        DataPersistenceManager.NewGame();

        // Potentially consider adding the ability to reset progress here

        SceneLoader.LoadIntoGame(firstLevel, newGame: true);
        LogManager.Instance.ResetAllLogs();
        DiaryManager.Instance.ResetAllDiaries();
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

        if (savedScene == null)
        {
            RestoreMenuButtons();
            hasStartedSceneTransition = false;
            return;
        }

        SceneLoader.LoadIntoGame(savedScene, newGame: false);
    }

    private bool IsLoaded(SceneAsset scene)
    {
        if (scene == null) return false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (string.Equals(loadedScene.name, scene.SceneName, System.StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private void LoadMusicScene()
    {
        SceneAsset musicScene = SceneAsset.GetSceneAsset("MusicScene");
        if (musicScene != null && !IsLoaded(musicScene))
        {
            SceneManager.LoadScene(musicScene.SceneName, LoadSceneMode.Additive);
        }
        else
        {
            Debug.LogError("MusicScene could not be loaded. Check that it exists and is included in the build settings.");
        }
    }

    private static SceneAsset ResolveLoadableSceneOrFallback(SceneAsset scene, SceneAsset fallbackScene)
    {
        if (scene != null) return scene;

        if (fallbackScene != null) return fallbackScene;

        Debug.LogError($"Neither the saved scene '{scene}' nor the fallback scene '{fallbackScene}' could be resolved through the SceneAsset system.");
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

        // Ensure load button state is always correct after restoring menu buttons
        TurnOffLoadButtonIfNoData();
    }

    private void ResetTransientMenuState()
    {
        hasStartedSceneTransition = false;
        RestoreMenuButtons();
    }

    public void TurnOffLoadButtonIfNoData()
    {
        int loadableSlots = 0;
        if (DataPersistenceManager.Instance != null)
        {
            Dictionary<string, GameData> profiles = DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>();
            foreach (var kvp in profiles)
            {
                if (kvp.Value != null)
                {
                    loadableSlots++;
                }
            }
        }

        Debug.Log($"[TurnOffLoadButtonIfNoData] Called. Loadable slots: {loadableSlots}. loadButton assigned: {loadButton != null}");
        if (loadButton != null)
        {
            loadButton.interactable = loadableSlots > 0;
            Debug.Log($"[TurnOffLoadButtonIfNoData] loadButton.interactable set to {loadButton.interactable}");
        }
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

        // After deletion, update all slot interactability in load menu
        if (isInLoadMenu)
        {
            // Get latest profile data
            Dictionary<string, GameData> profiles = DataPersistenceManager.Instance != null
                ? DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>()
                : new Dictionary<string, GameData>();

            if (saveSlots != null)
            {
                foreach (var slot in saveSlots)
                {
                    if (slot == null) continue;
                    GameData data = null;
                    profiles.TryGetValue(slot.GetProfileId(), out data);
                    slot.SetData(data); // This will update both visuals and interactability
                    slot.SetInteractable(data != null); // Redundant but ensures interactability
                }
            }

            RestoreMenuButtons();
        }

        bool hasAnyLoadableProfile = false;
        if (DataPersistenceManager.Instance != null)
        {
            Dictionary<string, GameData> profiles = DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>();
            foreach (var kvp in profiles)
            {
                if (kvp.Value != null)
                {
                    hasAnyLoadableProfile = true;
                    break;
                }
            }
        }

        // Refresh displayed slots
        currentSaveSlotSelected = null;
        ActivateMenu(hasAnyLoadableProfile ? isLoadingGame : false);
        TurnOffLoadButtonIfNoData();
    }

    //When the back button is click it activates the main menu again
    public void OnBackClicked()
    {
        EnsureReferences();
        ResetTransientMenuState();

        if (mainMenu != null)
            mainMenu.ActivateMenu();

        this.DeactivateMenu();
    }

    private void EnsureTheCorrectSaveSlotText()
    {
        Dictionary<string, GameData> profilesGameData = DataPersistenceManager.Instance != null
            ? (DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>())
            : new Dictionary<string, GameData>();

        // Always update slot data and interactability for both load and new game
        foreach (SaveSlots saveSlot in saveSlots)
        {
            if (saveSlot == null) continue;
            GameData profileData = null;
            profilesGameData.TryGetValue(saveSlot.GetProfileId(), out profileData);
            saveSlot.SetData(profileData); // This will update the slot's text (e.g., 'no data')
            // Interactability: only disable in load menu if no data, otherwise always interactable
            if (profileData == null && isLoadingGame)
            {
                saveSlot.SetInteractable(false);
            }
            else
            {
                saveSlot.SetInteractable(true);
            }
        }
    }

    //Activates the main menu when called
    public void ActivateMenu(bool isLoadingGame)
    {
        EnsureReferences();
        ResetTransientMenuState();
        this.gameObject.SetActive(true);

        this.isLoadingGame = isLoadingGame;

        GameObject firstSelected = backButton != null ? backButton.gameObject : null;

        Dictionary<string, GameData> profilesGameData = DataPersistenceManager.Instance != null
            ? (DataPersistenceManager.GetAllProfilesGameData() ?? new Dictionary<string, GameData>())
            : new Dictionary<string, GameData>();

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

        TurnOffLoadButtonIfNoData();
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

    // Call this when activating the menu as a load menu
    public void ActivateAsLoadMenu()
    {
        ActivateMenu(true);
    }

    // Call this when activating the menu as a new game menu
    public void ActivateAsNewGameMenu()
    {
        ActivateMenu(false);
    }
}



