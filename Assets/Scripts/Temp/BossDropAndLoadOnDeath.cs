using System.Collections;
using UnityEngine;
using EnemyBehavior.Boss;
using Progression.SceneManagement;
using UnityEngine.Video;

public class BossDropAndLoadOnDeath : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Transform bossDropTransform;

    [Header("Drop")]
    [SerializeField] private GameObject cardToEnable;
    [SerializeField] private Vector3 cardSpawnOffset = Vector3.up;
    [SerializeField] private bool detachCardOnDrop = true;

    [Header("Scene")]
    [SerializeField] private bool loadSceneOnDrop = true;
    [SerializeField] private bool preloadSceneOnEnable = true;
    [SerializeField] private SceneAsset sceneToActivate;
    [SerializeField] private SceneLoadZone sceneLoadZone;

    [Header("Ending Flow")]
    [SerializeField] private bool playEndingCutsceneOnDefeat = false;
    [SerializeField] private string endingCutsceneName = "Final Cutscene";
    [SerializeField] private SceneAsset creditsScene;
    [SerializeField] private bool loadCreditsAfterEndingCutscene = false;

    private bool triggered;
    private Coroutine preloadRoutine;

    private void OnEnable()
    {
        if (preloadSceneOnEnable)
            preloadRoutine = StartCoroutine(BeginScenePreloadWhenReady());

        if (bossHealth == null)
            return;

        bossHealth.BossDefeated += HandleBossDefeated;

        if (bossHealth.IsDefeated)
            HandleBossDefeated();
    }

    private void OnDisable()
    {
        if (preloadRoutine != null)
        {
            StopCoroutine(preloadRoutine);
            preloadRoutine = null;
        }

        if (bossHealth == null)
            return;

        bossHealth.BossDefeated -= HandleBossDefeated;
    }

    private void HandleBossDefeated()
    {
        if (triggered)
            return;

        triggered = true;
        SpawnCard();

        if (loadSceneOnDrop || playEndingCutsceneOnDefeat || loadCreditsAfterEndingCutscene)
            StartCoroutine(HandlePostDefeatSceneFlow());
    }

    private IEnumerator HandlePostDefeatSceneFlow()
    {
        if (playEndingCutsceneOnDefeat)
        {
            VideoClip endingClip = Cutscene.GetCutscene(endingCutsceneName);
            if (endingClip != null)
            {
                bool cutsceneFinished = false;
                CutsceneManager.PlayCutscene(endingClip, () => cutsceneFinished = true);

                while (!cutsceneFinished)
                    yield return null;
            }
            else
            {
                Debug.LogWarning($"[BossDropAndLoadOnDeath] Ending cutscene '{endingCutsceneName}' was not found. Continuing with scene flow.");
            }
        }

        if (loadCreditsAfterEndingCutscene && creditsScene != null)
        {
            yield return SceneLoader.LoadCoroutine(creditsScene, loadScreen: false);
            yield break;
        }

        ActivateScene();
    }

    private void SpawnCard()
    {
        if (cardToEnable == null)
            return;

        Transform dropTransform = ResolveDropTransform();
        if (dropTransform != null)
        {
            cardToEnable.transform.position = dropTransform.position + cardSpawnOffset;
            cardToEnable.transform.rotation = dropTransform.rotation;
        }

        if (detachCardOnDrop)
            cardToEnable.transform.SetParent(null, true);

        cardToEnable.SetActive(true);
    }

    private Transform ResolveDropTransform()
    {
        if (bossDropTransform != null)
            return bossDropTransform;

        if (bossHealth != null)
            return bossHealth.transform;

        return transform;
    }

    private void ActivateScene()
    {
        if (sceneLoadZone != null)
        {
            sceneLoadZone.ActivateManagedScene();
            return;
        }

        if (sceneToActivate == null)
        {
            Debug.LogWarning("[BossDropAndLoadOnDeath] No scene configured to activate after boss defeat.");
            return;
        }

        SceneLoader.ActivatePreparedScene(sceneToActivate, loadScreen: false);
    }

    private void BeginScenePreload()
    {
        if (sceneLoadZone != null)
        {
            sceneLoadZone.PreloadManagedScene();
            return;
        }

        if (sceneToActivate == null)
            return;

        SceneLoader.PreloadAdditive(sceneToActivate);
    }

    private IEnumerator BeginScenePreloadWhenReady()
    {
        while (!SceneAsset.PlayerLoaded)
            yield return null;

        yield return null;

        BeginScenePreload();
        preloadRoutine = null;
    }
}
