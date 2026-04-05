using System.Collections;
using UI.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossSceneTransitionInteractable : UnlockableInteraction
{
    private const string LoadingSceneName = "LoadingScene";

    [Header("Scenes")]
    [SerializeField] private SceneAsset sceneToLoad;
    [SerializeField] private SceneAsset sceneToUnload;
    [SerializeField] private bool pauseDuringLoading = true;
    [SerializeField, Min(0f)] private float loadingControllerWaitTimeout = 5f;

    private bool isTransitioning;

    protected override void ExecuteInteraction()
    {
        if (isTransitioning)
            return;

        InteractionUI.Instance?.HideInteractPrompt();
        isTransitioning = true;
        StartCoroutine(BeginTransition());
    }

    private IEnumerator BeginTransition()
    {
        yield return EnsureLoadingScreenReady();

        IEnumerator routine = TransitionRoutine();

        if (LoadingScreenController.HasInstance)
        {
            LoadingScreenController.BeginLoading(routine, pauseDuringLoading);
            yield break;
        }

        Debug.LogWarning("[BossSceneTransitionInteractable] LoadingScreenController unavailable. Falling back to direct scene transition.");
        yield return StartCoroutine(routine);
    }

    private IEnumerator TransitionRoutine()
    {
        try
        {
            if (sceneToLoad != null)
            {
                yield return SceneLoader.LoadCoroutine(sceneToLoad, loadScreen: false);
            }

            if (sceneToUnload != null)
            {
                yield return SceneLoader.UnloadCoroutine(sceneToUnload);
            }
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private IEnumerator EnsureLoadingScreenReady()
    {
        if (LoadingScreenController.HasInstance)
            yield break;

        Scene loadingScene = SceneManager.GetSceneByName(LoadingSceneName);
        if (!loadingScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                Debug.LogError($"[BossSceneTransitionInteractable] Failed to load '{LoadingSceneName}'.");
                yield break;
            }

            yield return loadOperation;
        }

        if (loadingControllerWaitTimeout <= 0f)
            yield break;

        float timeoutAt = Time.unscaledTime + loadingControllerWaitTimeout;
        while (!LoadingScreenController.HasInstance && Time.unscaledTime < timeoutAt)
            yield return null;
    }
}
