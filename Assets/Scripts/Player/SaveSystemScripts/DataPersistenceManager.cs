/*
Written by Brandon Wahl

Handles data persistence and ensures data is properly saved and loaded

*/

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using System.IO;
using Singletons;
using Progression.Checkpoints;

public class DataPersistenceManager : Singleton<DataPersistenceManager>

{
    [Header("Debugging")]
    [SerializeField] private bool initializeDataIfNull = false;

    [SerializeField] private bool disableDataPersistence = false;

    [SerializeField] private bool overrideSelectedProfileId = false;

    [SerializeField] private string testSelectedProfile = "test";

    private static GameData gameData;

    // Defaults to the value set on the DataPersistenceManager prefab.
    // Keeping this non-empty ensures the singleton can safely auto-create if the prefab is missing from a scene.
    [SerializeField] private string fileName = "save.game";

    private static string selectedProfileId = "";

    public static List<IDataPersistenceManager> dataPersistenceObjects;

    private static FileDataHandler fileDataHandler;

    private static SceneAsset lastSavedScene;

    protected override void Awake()
    {
        base.Awake();

        //Defines the save file
        fileDataHandler = new FileDataHandler(Application.persistentDataPath, fileName);

        selectedProfileId = fileDataHandler.GetMostRecentUpdatedProfile();

        //If the editor is using a test profile, it will warn them so they are aware
        if (overrideSelectedProfileId)
        {
            selectedProfileId = testSelectedProfile;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // When a scene is loaded, the function below is called
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //Defines the variable dataPersistenceObjects to be the function below
        RefreshDataPersistenceObjects();
        LoadGame();
    }

    private static void RefreshDataPersistenceObjects()
    {
        if (Instance == null)
        {
            dataPersistenceObjects = new List<IDataPersistenceManager>();
            return;
        }

        dataPersistenceObjects = Instance.FindAllDataPersistenceObjects();
    }

    private static IEnumerable<IDataPersistenceManager> GetValidDataPersistenceObjects()
    {
        RefreshDataPersistenceObjects();

        if (dataPersistenceObjects == null)
            yield break;

        foreach (IDataPersistenceManager dataPersistenceObj in dataPersistenceObjects)
        {
            if (dataPersistenceObj is UnityEngine.Object unityObject && unityObject == null)
                continue;

            if (dataPersistenceObj != null)
                yield return dataPersistenceObj;
        }
    }

    public static void ChangeSelectedProfileId(string newProfileId)
    { 
        selectedProfileId = newProfileId;
        LoadGame();
    }

    //When selecting a new game, new game data is created
    public static void NewGame()
    {
        // Delete the existing save for the currently selected profile (clean reset)
        if (fileDataHandler != null && !string.IsNullOrEmpty(selectedProfileId))
        {
            fileDataHandler.DeleteProfile(selectedProfileId);
        }

        // Create fresh data with defaults
        gameData = new GameData();

        // Immediately persist the new defaults so the next scene load reads them
        if (!Instance.disableDataPersistence)
        {
            // Initialize lastSavedScene for the new profile
            gameData.lastSavedScene = SceneManager.GetActiveScene().name;
            lastSavedScene = gameData.lastSavedScene;
            fileDataHandler.Save(gameData, selectedProfileId);
        }
    }

    public static void LoadGame()
    {
        if (Instance.disableDataPersistence) return;

        //Loads the game data if it exists
        gameData = fileDataHandler.Load(selectedProfileId);

        if(gameData == null && Instance.initializeDataIfNull) NewGame();

        //If it doesnt, it will call the NewGame function
        if (gameData == null) return;

        // Keep the manager's cached lastSavedScene in sync with the loaded profile
        lastSavedScene = gameData.lastSavedScene;
        //Goes through each of the found items that needs to be loaded and loads them
        foreach (IDataPersistenceManager dataPersistenceObj in GetValidDataPersistenceObjects())
        {
            dataPersistenceObj.LoadData(gameData);
        }
    }

    public static void SaveGame()
    {
        if (Instance.disableDataPersistence) return;
        if (gameData == null) return;

        //Goes through each of the found items that needs to be saved and saves them
        foreach (IDataPersistenceManager dataPersistenceObj in GetValidDataPersistenceObjects())
            dataPersistenceObj.SaveData(gameData);

        CheckpointBehavior activeCheckpoint = CheckpointBehavior.currentCheckpoint;
        if (activeCheckpoint != null)
        {
            SceneAsset checkpointScene = activeCheckpoint.CheckpointSceneAsset;
            if (checkpointScene != null)
            {
                gameData.currentSceneName = checkpointScene.SceneName;
                gameData.currentSpawnPointID = activeCheckpoint.CheckpointId;
                gameData.lastSavedScene = checkpointScene.SceneName;
            }
        }

        //Saves the current time, converts to binary, and assigns the data to gameData
        gameData.lastUpdated = System.DateTime.Now.ToBinary();

        // Prefer the checkpoint scene saved by gameplay systems; otherwise fall back to the currently active scene.
        if (string.IsNullOrEmpty(gameData.lastSavedScene))
            gameData.lastSavedScene = SceneManager.GetActiveScene().name;

        lastSavedScene = gameData.lastSavedScene;

        fileDataHandler.Save(gameData, selectedProfileId);
    }

    public static void SetDebugStartupTarget(SceneAsset scene, string spawnPointId = "default")
    {
        if (scene == null)
        {
            Debug.LogError("[DataPersistenceManager] Cannot set debug startup target because scene is null.");
            return;
        }

        gameData ??= new GameData();

        string resolvedSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? "default" : spawnPointId;
        gameData.currentSceneName = scene.SceneName;
        gameData.currentSpawnPointID = resolvedSpawnPointId;
        gameData.lastSavedScene = scene.SceneName;
        gameData.lastUpdated = System.DateTime.Now.ToBinary();
        lastSavedScene = scene;

        if (Instance == null || Instance.disableDataPersistence || fileDataHandler == null)
            return;

        if (string.IsNullOrWhiteSpace(selectedProfileId))
            selectedProfileId = "debug";

        fileDataHandler.Save(gameData, selectedProfileId);
    }

    /// <summary>
    /// Returns the last saved scene for the currently-selected profile (or empty string if none).
    /// </summary>
    public static SceneAsset GetLastSavedScene() => gameData != null ? gameData.lastSavedScene : lastSavedScene;

    /// <summary>
    /// Deletes the save file for the given profile id and refreshes menu data.
    /// </summary>
    public static void DeleteProfile(string profileId)
    {
        if (Instance.disableDataPersistence || fileDataHandler == null || string.IsNullOrEmpty(profileId)) return;

        fileDataHandler.DeleteProfile(profileId);

        // If we deleted the currently-selected profile, pick another most-recent one
        if (selectedProfileId == profileId)
        {
            selectedProfileId = fileDataHandler.GetMostRecentUpdatedProfile();
            gameData = null;
        }
    }

    //Defines a list that will find any instances of game data that needs to be loaded/saved
    private List<IDataPersistenceManager> FindAllDataPersistenceObjects()
    {
        IEnumerable<IDataPersistenceManager> dataPeristenceObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<IDataPersistenceManager>();

        return new List<IDataPersistenceManager>(dataPeristenceObjects);
    }

    //Returns true or false if there is game data
    public static bool HasGameData() => gameData != null;

    public static Dictionary<string, GameData> GetAllProfilesGameData() => fileDataHandler.LoadAllProfiles();
}
