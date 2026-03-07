using UnityEngine;
using Singletons;
using UI.Loading;

/// <summary>
/// Static class responsible for managing the loading screen UI and behavior during scene transitions.
/// </summary>
public class LoadScreen : Singleton<LoadScreen>
{
    [SerializeField] GameObject loadScreenPrefab;

    private GameObject loadScren;

    public static bool IsActive => Instance.loadScreenPrefab.activeSelf;

    protected override void Awake()
    {
        base.Awake();

        loadScren = Instantiate(loadScreenPrefab);

        loadScreenPrefab.SetActive(false); // Ensure the load screen is hidden at the start
    }

    public static void StartLoading()
    {
        LoadingScreenController.BeginLoading(null);
    }

}
