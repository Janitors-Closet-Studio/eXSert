using System.Collections;
using UI.Loading;
using UnityEngine;

public class TempBossSceneTransitionInteractable : UnlockableInteraction
{
    [Header("Scenes")]
    [SerializeField] private SceneAsset sceneToLoad;
    [SerializeField] private SceneAsset sceneToUnload;
    [SerializeField] private bool pauseDuringLoading = true;

    private bool isTransitioning;

    protected override void ExecuteInteraction()
    {
        if (isTransitioning)
            return;

        InteractionUI.Instance?.HideInteractPrompt();
        isTransitioning = true;
        var routine = TransitionRoutine();

        if (LoadingScreenController.HasInstance)
        {
            LoadingScreenController.BeginLoading(routine, pauseDuringLoading);
            return;
        }

        StartCoroutine(routine);
    }

    private IEnumerator TransitionRoutine()
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
}
