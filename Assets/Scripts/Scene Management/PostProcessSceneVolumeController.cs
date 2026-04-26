using UnityEngine;
using UnityEngine.SceneManagement;

public class PostProcessSceneVolumeController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private SceneAsset engineCoreScene;
    [SerializeField] private SceneAsset conservatoryScene;

    [Header("Volume Objects")]
    [SerializeField] private GameObject engineVolume;
    [SerializeField] private GameObject conservatoryVolume;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        RefreshVolumeState();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            RefreshVolumeState();
    }

    private void OnSceneChanged(Scene scene, LoadSceneMode loadSceneMode)
    {
        RefreshVolumeState();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        RefreshVolumeState();
    }

    private void RefreshVolumeState()
    {
        bool isEngineCoreLoaded = engineCoreScene != null && engineCoreScene.IsLoaded();
        bool isConservatoryLoaded = conservatoryScene != null && conservatoryScene.IsLoaded();

        bool enableEngineVolume = isEngineCoreLoaded;
        bool enableConservatoryVolume = !isEngineCoreLoaded && isConservatoryLoaded;

        if (engineVolume != null)
            engineVolume.SetActive(enableEngineVolume);

        if (conservatoryVolume != null)
            conservatoryVolume.SetActive(enableConservatoryVolume);
    }
}