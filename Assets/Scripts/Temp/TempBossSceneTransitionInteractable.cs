using System.Collections;
using UI.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TempBossSceneTransitionInteractable : UnlockableInteraction
{
    [Header("Scenes")]
    [SerializeField] private string sceneToLoad = "FinalBoss";
    [SerializeField] private string sceneToUnload = "Conservatory";
    [SerializeField] private bool pauseDuringLoading = true;

    private bool isTransitioning;

    protected override void ExecuteInteraction()
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        var routine = TransitionRoutine();

        if (LoadingScreenController.HasInstance)
        {
            LoadingScreenController.Instance.BeginLoading(routine, pauseDuringLoading);
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.StartCoroutine(routine);
            return;
        }

        StartCoroutine(routine);
    }

    private IEnumerator TransitionRoutine()
    {
        if (!string.IsNullOrWhiteSpace(sceneToLoad))
        {
            var loadScene = SceneManager.GetSceneByName(sceneToLoad);
            if (!loadScene.isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
                if (loadOp != null)
                {
                    while (!loadOp.isDone)
                        yield return null;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(sceneToUnload))
        {
            var unloadScene = SceneManager.GetSceneByName(sceneToUnload);
            if (unloadScene.IsValid() && unloadScene.isLoaded)
            {
                var unloadOp = SceneManager.UnloadSceneAsync(unloadScene);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                        yield return null;
                }
            }
        }
    }
}
