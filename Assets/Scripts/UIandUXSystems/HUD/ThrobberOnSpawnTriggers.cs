using System.Collections;
using System.Collections.Generic;
using Progression.Checkpoints;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class ThrobberOnSpawnTriggers : MonoBehaviour, IDataPersistenceManager
{
    [Header("Trigger Sources")]
    [Tooltip("GameObjects that have trigger colliders. When the player enters any of them, this throbber will fade in/out.")]
    [SerializeField] private List<Collider> triggerColliders = new List<Collider>();

    [Header("Checkpoint Integration")]
    [SerializeField] private bool showOnFirstCheckpointPerScene = true;

    [Header("Fade Settings")]
    [SerializeField, Range(0.05f, 5f)] private float fadeDuration = 0.5f;
    [SerializeField, Range(0f, 30f)] private float visibleDuration = 3f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Player Filter")]
    [Tooltip("Tag used to identify the player object.")]
    [SerializeField] private string playerTag = "Player";

    private CanvasGroup canvasGroup;
    private Coroutine activeRoutine;
    private readonly HashSet<string> shownCheckpointScenes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        // Ensure all listed colliders are triggers and have a helper attached
        foreach (var col in triggerColliders)
        {
            if (col == null) continue;

            if (!col.isTrigger)
                col.isTrigger = true;

            var helper = col.gameObject.GetComponent<ThrobberTriggerHelper>();
            if (helper == null)
                helper = col.gameObject.AddComponent<ThrobberTriggerHelper>();

            helper.Initialize(this, playerTag);
        }
    }

    private void OnEnable()
    {
        CheckpointBehavior.OnCheckpointTriggered += HandleCheckpointTriggered;
    }

    private void OnDisable()
    {
        CheckpointBehavior.OnCheckpointTriggered -= HandleCheckpointTriggered;
    }

    /// <summary>
    /// Called by helper components when the player enters a trigger.
    /// </summary>
    public void OnPlayerEnteredTrigger()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(FadeSequence());
    }

    public void LoadData(GameData data)
    {
        shownCheckpointScenes.Clear();

        if (data?.checkpointThrobberScenesShown == null)
            return;

        foreach (string sceneName in data.checkpointThrobberScenesShown)
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
                shownCheckpointScenes.Add(sceneName);
        }
    }

    public void SaveData(GameData data)
    {
        if (data == null)
            return;

        if (data.checkpointThrobberScenesShown == null)
            data.checkpointThrobberScenesShown = new List<string>();
        else
            data.checkpointThrobberScenesShown.Clear();

        foreach (string sceneName in shownCheckpointScenes)
            data.checkpointThrobberScenesShown.Add(sceneName);
    }

    private void HandleCheckpointTriggered(CheckpointBehavior checkpoint)
    {
        if (!showOnFirstCheckpointPerScene || checkpoint == null)
            return;

        SceneAsset checkpointScene = checkpoint.CheckpointSceneAsset;
        string sceneName = checkpointScene != null ? checkpointScene.SceneName : checkpoint.gameObject.scene.name;
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (!shownCheckpointScenes.Add(sceneName))
            return;

        OnPlayerEnteredTrigger();
    }

    private IEnumerator FadeSequence()
    {
        yield return Fade(0f, 1f);

        float timer = 0f;
        while (timer < visibleDuration)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        yield return Fade(1f, 0f);
        activeRoutine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (canvasGroup == null || Mathf.Approximately(fadeDuration, 0f))
        {
            if (canvasGroup != null)
                canvasGroup.alpha = to;
            yield break;
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}

/// <summary>
/// Small helper attached to each trigger collider; forwards player enter events to the main throbber.
/// </summary>
public sealed class ThrobberTriggerHelper : MonoBehaviour
{
    private ThrobberOnSpawnTriggers owner;
    private string playerTag;

    public void Initialize(ThrobberOnSpawnTriggers owner, string playerTag)
    {
        this.owner = owner;
        this.playerTag = playerTag;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;
        if (!other.CompareTag(playerTag)) return;

        owner.OnPlayerEnteredTrigger();
    }
}