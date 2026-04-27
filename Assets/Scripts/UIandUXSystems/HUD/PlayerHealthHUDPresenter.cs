using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges the runtime player health system to HUD UI widgets (slider, fill image, labels, etc.).
/// Attach this to the health section of the HUD prefab and assign the relevant references.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealthHUDPresenter : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Slider slider;
    [SerializeField] private HealthBar healthFill;
    [SerializeField] private TMP_Text valueLabel;
    [SerializeField, Tooltip("Optional root GameObject to toggle when the player does not exist.")]
    private GameObject rootToToggle;

    [Header("Options")]
    [SerializeField, Tooltip("Automatically grab Slider/HealthBar/TMP_Text references from children when not manually assigned.")]
    private bool autoBindChildren = true;
    [SerializeField, Tooltip("Hide the HUD chunk when no player health instance exists (e.g. during menus).")]
    private bool hideWhenPlayerMissing = true;
    [SerializeField, Tooltip("String.Format pattern used for the numeric label. {0}=current, {1}=max")]
    private string valueFormat = "{0}/{1}";

    [SerializeField, Min(0.05f), Tooltip("How often to retry finding the player health manager when missing.")]
    private float missingPlayerRetrySeconds = 0.25f;

    private Coroutine retryResolveRoutine;

    private void Awake()
    {
        if (autoBindChildren)
        {
            if (slider == null)
                slider = GetComponentInChildren<Slider>(true);
            if (healthFill == null)
                healthFill = GetComponentInChildren<HealthBar>(true);
            if (valueLabel == null)
                valueLabel = GetComponentInChildren<TMP_Text>(true);
        }

        if (rootToToggle == null)
            rootToToggle = gameObject;

        if (rootToToggle == gameObject)
        {
            // Never deactivate this presenter object itself; doing so would unsubscribe from
            // events and stop the retry coroutine, preventing rebind after scene transitions.
            rootToToggle = null;
        }
    }

    private void OnEnable()
    {
        PlayerHealthBarManager.OnPlayerHealthChanged += HandleHealthChanged;
        PlayerHealthBarManager.OnPlayerHealthRegistered += HandlePlayerRegistered;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Prime();

        if (retryResolveRoutine == null)
            retryResolveRoutine = StartCoroutine(RetryResolveRoutine());
    }

    private void OnDisable()
    {
        PlayerHealthBarManager.OnPlayerHealthChanged -= HandleHealthChanged;
        PlayerHealthBarManager.OnPlayerHealthRegistered -= HandlePlayerRegistered;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (retryResolveRoutine != null)
        {
            StopCoroutine(retryResolveRoutine);
            retryResolveRoutine = null;
        }
    }

    private void Prime()
    {
        var manager = ResolvePlayerHealthManager();
        if (manager != null)
        {
            HandleHealthChanged(new PlayerHealthBarManager.HealthSnapshot(manager.CurrentHealth, manager.MaxHealth));
            SetRootActive(true);
        }
        else if (hideWhenPlayerMissing)
        {
            SetRootActive(false);
        }
    }

    private void HandlePlayerRegistered(PlayerHealthBarManager manager)
    {
        if (manager == null)
        {
            if (hideWhenPlayerMissing)
                SetRootActive(false);
            return;
        }

        HandleHealthChanged(new PlayerHealthBarManager.HealthSnapshot(manager.CurrentHealth, manager.MaxHealth));
        SetRootActive(true);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Prime();
    }

    private IEnumerator RetryResolveRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, missingPlayerRetrySeconds));

        while (enabled)
        {
            if (PlayerHealthBarManager.Instance == null)
            {
                PlayerHealthBarManager manager = ResolvePlayerHealthManager();
                if (manager != null)
                {
                    HandlePlayerRegistered(manager);
                }
                else if (hideWhenPlayerMissing)
                {
                    SetRootActive(false);
                }
            }

            yield return wait;
        }
    }

    private static PlayerHealthBarManager ResolvePlayerHealthManager()
    {
        if (PlayerHealthBarManager.Instance != null)
            return PlayerHealthBarManager.Instance;

        PlayerHealthBarManager manager = FindFirstObjectByType<PlayerHealthBarManager>();
        if (manager != null)
            return manager;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                manager = rootObjects[rootIndex].GetComponentInChildren<PlayerHealthBarManager>(true);
                if (manager != null)
                    return manager;
            }
        }

        return null;
    }

    private void HandleHealthChanged(PlayerHealthBarManager.HealthSnapshot snapshot)
    {
        if (slider != null)
        {
            slider.maxValue = snapshot.max;
            slider.value = snapshot.current;
        }

        if (healthFill != null)
        {
            healthFill.SetHealth(snapshot.current, snapshot.max);
        }

        if (valueLabel != null)
        {
            valueLabel.text = string.Format(valueFormat,
                Mathf.RoundToInt(snapshot.current),
                Mathf.RoundToInt(snapshot.max));
        }
    }

    private void SetRootActive(bool state)
    {
        if (!hideWhenPlayerMissing)
            return;

        if (rootToToggle != null)
        {
            if (rootToToggle.activeSelf != state)
                rootToToggle.SetActive(state);
            return;
        }

        // Fallback when no dedicated root is assigned, hide visual widgets while keeping this
        // presenter active so it can continue to rebind when the player loads.
        if (slider != null && slider.gameObject.activeSelf != state)
            slider.gameObject.SetActive(state);

        if (healthFill != null && healthFill.gameObject.activeSelf != state)
            healthFill.gameObject.SetActive(state);

        if (valueLabel != null && valueLabel.gameObject.activeSelf != state)
            valueLabel.gameObject.SetActive(state);
    }
}
