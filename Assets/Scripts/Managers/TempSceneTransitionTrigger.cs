using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temporary vertical-slice trigger that loads or unloads scenes additively when the player passes through.
/// Attach this to box colliders inside PlayerScene. Configure one trigger for loading the next slice
/// and another trigger further inside the destination slice to unload the previous scene once the
/// player is safely inside. Designed to fire only once.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TempSceneTransitionTrigger : MonoBehaviour
{
    public enum TransitionMode
    {
        LoadScene,
        UnloadScene
    }

    [Header("Trigger Settings")]
    [SerializeField] private TransitionMode mode = TransitionMode.LoadScene;
    [SerializeField, Tooltip("Name of the scene asset to load or unload (must be added to Build Settings).")]
    private string sceneName;
    [SerializeField, Tooltip("Tag used to detect the player.")]
    private string playerTag = "Player";
    [SerializeField, Tooltip("If true, the trigger only fires once.")]
    private bool triggerOnce = true;

    [Header("Door/Blocker (optional)")]
    [SerializeField, Tooltip("Door or blocker GameObject to enable after the trigger fires (prevents backtracking).")]
    private GameObject doorToEnable;
    [SerializeField, Tooltip("Optional collider to enable when the door closes. Defaults to the collider on doorToEnable if left empty.")]
    private Collider doorCollider;
    [SerializeField, Tooltip("Optional animator for the door. Leave unset if not needed.")]
    private Animator doorAnimator;
    [SerializeField, Tooltip("Animator trigger name used to play the door close animation.")]
    private string doorCloseTrigger = "Close";

    private bool consumed;

    private void Reset()
    {
        if (TryGetComponent(out Collider col))
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (TryGetComponent(out Collider col))
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && consumed)
            return;
        if (other == null || !other.CompareTag(playerTag))
            return;

        consumed = true;
        if (TryGetComponent(out Collider col))
        {
            col.enabled = false;
        }

        switch (mode)
        {
            case TransitionMode.LoadScene:
                StartCoroutine(LoadSceneRoutine());
                break;
            case TransitionMode.UnloadScene:
                StartCoroutine(UnloadSceneRoutine());
                break;
        }

        ActivateDoor();
    }

    private IEnumerator LoadSceneRoutine()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[TempSceneTransitionTrigger] No scene name provided for load trigger.", this);
            yield break;
        }

        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            yield break;
        }

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogWarning($"[TempSceneTransitionTrigger] Failed to start loading scene '{sceneName}'.", this);
            yield break;
        }

        while (!op.isDone)
        {
            yield return null;
        }

        Debug.Log($"[TempSceneTransitionTrigger] Loaded scene '{sceneName}'.");
    }

    private IEnumerator UnloadSceneRoutine()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[TempSceneTransitionTrigger] No scene name provided for unload trigger.", this);
            yield break;
        }

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            yield break;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (activeScene == scene)
        {
            var fallback = FindFallbackActiveScene(scene);
            if (fallback.IsValid())
            {
                SceneManager.SetActiveScene(fallback);
            }
            else
            {
                Debug.LogWarning($"[TempSceneTransitionTrigger] Cannot unload active scene '{sceneName}' because no fallback scene is loaded.", this);
                yield break;
            }
        }

        var op = SceneManager.UnloadSceneAsync(scene);
        if (op == null)
        {
            Debug.LogWarning($"[TempSceneTransitionTrigger] Failed to start unloading scene '{sceneName}'.", this);
            yield break;
        }

        while (!op.isDone)
        {
            yield return null;
        }

        Debug.Log($"[TempSceneTransitionTrigger] Unloaded scene '{sceneName}'.");
    }

    private static Scene FindFallbackActiveScene(Scene excludedScene)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var candidate = SceneManager.GetSceneAt(i);
            if (candidate.IsValid() && candidate.isLoaded && candidate != excludedScene)
            {
                return candidate;
            }
        }

        return default;
    }

    private void ActivateDoor()
    {
        if (doorToEnable != null)
        {
            doorToEnable.SetActive(true);
        }

        var targetCollider = doorCollider;
        if (targetCollider == null && doorToEnable != null)
        {
            doorToEnable.TryGetComponent(out targetCollider);
        }

        if (targetCollider != null)
        {
            targetCollider.enabled = true;
        }

        if (doorAnimator != null)
        {
            if (!string.IsNullOrEmpty(doorCloseTrigger))
            {
                doorAnimator.SetTrigger(doorCloseTrigger);
            }
            else
            {
                doorAnimator.SetBool("Closed", true);
            }
        }
    }
}
