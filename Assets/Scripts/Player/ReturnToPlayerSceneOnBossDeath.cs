using System.Collections;
using EnemyBehavior.Boss;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ReturnToPlayerSceneOnBossDeath : MonoBehaviour
{
    private const string DefaultBossSceneName = "ChargingStation";
    private const string DefaultPlayerSceneName = "PlayerScene";

    [Header("Configuration")]
    [SerializeField, Tooltip("Scene name where the Augur boss lives.")]
    private string bossSceneName = DefaultBossSceneName;

    [SerializeField, Tooltip("Scene name to return the player root to when the boss dies.")]
    private string playerSceneName = DefaultPlayerSceneName;

    [SerializeField, Tooltip("How often to retry finding BossHealth when it is not available yet.")]
    private float bossSearchIntervalSeconds = 0.5f;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    private BossHealth subscribedBossHealth;
    private Coroutine bossSearchRoutine;
    private bool hasReturnedToPlayerScene;

    private void OnEnable()
    {
        hasReturnedToPlayerScene = false;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureBossSubscription();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeFromBossHealth();

        if (bossSearchRoutine != null)
        {
            StopCoroutine(bossSearchRoutine);
            bossSearchRoutine = null;
        }
    }

    private void EnsureBossSubscription()
    {
        if (subscribedBossHealth != null)
            return;

        BossHealth bossHealth = FindBossHealthInPreferredScene();
        if (bossHealth == null)
            bossHealth = FindFirstObjectByType<BossHealth>();

        if (bossHealth != null)
        {
            SubscribeToBossHealth(bossHealth);
            return;
        }

        if (bossSearchRoutine == null)
            bossSearchRoutine = StartCoroutine(FindBossHealthRoutine());
    }

    private IEnumerator FindBossHealthRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.1f, bossSearchIntervalSeconds));

        while (subscribedBossHealth == null)
        {
            BossHealth bossHealth = FindBossHealthInPreferredScene();
            if (bossHealth == null)
                bossHealth = FindFirstObjectByType<BossHealth>();

            if (bossHealth != null)
            {
                SubscribeToBossHealth(bossHealth);
                bossSearchRoutine = null;
                yield break;
            }

            yield return wait;
        }

        bossSearchRoutine = null;
    }

    private void SubscribeToBossHealth(BossHealth bossHealth)
    {
        if (bossHealth == null)
            return;

        UnsubscribeFromBossHealth();

        subscribedBossHealth = bossHealth;
        subscribedBossHealth.BossDefeated += HandleBossDefeated;
        Log($"Subscribed to boss defeat from '{bossHealth.gameObject.scene.name}'.");

        if (subscribedBossHealth.IsDefeated)
            HandleBossDefeated();
    }

    private BossHealth FindBossHealthInPreferredScene()
    {
        Scene bossScene = SceneManager.GetSceneByName(bossSceneName);
        if (!bossScene.IsValid() || !bossScene.isLoaded)
            return null;

        GameObject[] rootObjects = bossScene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            BossHealth bossHealth = rootObjects[rootIndex].GetComponentInChildren<BossHealth>(true);
            if (bossHealth != null)
                return bossHealth;
        }

        return null;
    }

    private void UnsubscribeFromBossHealth()
    {
        if (subscribedBossHealth == null)
            return;

        subscribedBossHealth.BossDefeated -= HandleBossDefeated;
        subscribedBossHealth = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, bossSceneName, System.StringComparison.Ordinal))
            return;

        EnsureBossSubscription();
    }

    private void HandleBossDefeated()
    {
        if (hasReturnedToPlayerScene)
            return;

        hasReturnedToPlayerScene = true;

        Transform playerRoot = transform.root;
        if (playerRoot == null)
        {
            Log("Boss defeat received, but player root could not be resolved.");
            return;
        }

        Scene playerScene = SceneManager.GetSceneByName(playerSceneName);
        if (!playerScene.IsValid() || !playerScene.isLoaded)
        {
            Log($"Boss defeat received, but target scene '{playerSceneName}' is not loaded.");
            return;
        }

        if (playerRoot.parent != null)
            playerRoot.SetParent(null, true);

        SceneManager.MoveGameObjectToScene(playerRoot.gameObject, playerScene);

        if (PlayerPresenceManager.Instance != null)
            PlayerPresenceManager.Instance.RegisterPlayer(playerRoot);

        Log($"Moved player root '{playerRoot.name}' back to '{playerSceneName}' after boss defeat.");
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
            return;

        Debug.Log($"[ReturnToPlayerSceneOnBossDeath] {message}", this);
    }
}