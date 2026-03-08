using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-stop health bar widget for world-space enemy HP displays.
/// Finds the enemy's IHealthSystem, keeps a slider in sync, and billboards toward the active camera.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Slider slider;
    [SerializeField, Tooltip("Optional visual root to show/hide. Leave empty to auto-resolve a safe target.")]
    private GameObject visibilityTarget;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField, Tooltip("Attempt to bind to the closest IHealthSystem in the parent hierarchy on Awake.")]
    private bool autoBindParentHealth = true;

    [Header("Behavior")]
    [SerializeField, Tooltip("Hide the bar when the enemy is at full health to cut down on clutter.")]
    private bool hideWhenFull = true;
    [SerializeField, Tooltip("Hide the bar when the enemy is at zero health.")]
    private bool hideWhenEmpty = true;
    [SerializeField, Tooltip("How quickly the slider value interpolates toward the real HP value.")]
    private float sliderLerpSpeed = 12f;
    [SerializeField, Tooltip("Limit fallback camera selection to specific layers.")]
    private LayerMask cameraLayerMask = ~0;
    [SerializeField, Tooltip("Keep the bar flat to the camera (screen-parallel) instead of yaw-only billboarding.")]
    private bool alignToCameraRotation = true;
    [SerializeField, Tooltip("Flip the bar 180 degrees if it appears backwards.")]
    private bool flipFacing = false;

    private IHealthSystem health;
    private Transform enemyTransform;
    private Transform fallbackCameraTransform;
    private CanvasGroup visibilityCanvasGroup;
    private bool visualsVisible = true;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        ResolveVisibilityTarget();

        if (autoBindParentHealth)
        {
            BindToHealthSystem(GetComponentInParent<IHealthSystem>());
        }
    }

    private void LateUpdate()
    {
        if (health == null || enemyTransform == null || slider == null)
            return;

        UpdateSlider();
        AlignAboveEnemy();
        FaceActiveCamera();
    }

    /// <summary>
    /// Public hook so spawners can manually provide the enemy's health interface.
    /// </summary>
    public void BindToHealthSystem(IHealthSystem system)
    {
        health = system;
        enemyTransform = (system as Component)?.transform;

        if (health == null || enemyTransform == null)
        {
            Debug.LogWarning($"[EnemyHealthBar] Failed to bind IHealthSystem on {name}.", this);
            enabled = false;
            return;
        }

        slider.maxValue = health.maxHP;
        slider.value = health.currentHP;
        UpdateVisibility(force: true);
    }

    private void UpdateSlider()
    {
        slider.maxValue = Mathf.Max(0.01f, health.maxHP);
        float target = Mathf.Clamp(health.currentHP, 0f, slider.maxValue);
        slider.value = Mathf.MoveTowards(slider.value, target, sliderLerpSpeed * Time.deltaTime * slider.maxValue);

        UpdateVisibility();
    }

    private void UpdateVisibility(bool force = false)
    {
        if (slider == null)
            return;

        float target = Mathf.Clamp(slider.value, 0f, slider.maxValue);
        bool shouldHide = (hideWhenFull && Mathf.Approximately(target, slider.maxValue))
            || (hideWhenEmpty && Mathf.Approximately(target, 0f));

        bool shouldShow = !shouldHide;
        if (!force && visualsVisible == shouldShow)
            return;

        visualsVisible = shouldShow;
        SetVisualVisible(shouldShow);
    }

    private void ResolveVisibilityTarget()
    {
        if (visibilityTarget == null && slider != null)
        {
            visibilityTarget = slider.gameObject;
        }

        if (visibilityTarget == gameObject)
        {
            visibilityCanvasGroup = GetComponent<CanvasGroup>();
            if (visibilityCanvasGroup == null)
                visibilityCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        else if (visibilityTarget != null)
        {
            visibilityCanvasGroup = null;
        }
    }

    private void SetVisualVisible(bool visible)
    {
        if (visibilityCanvasGroup != null)
        {
            visibilityCanvasGroup.alpha = visible ? 1f : 0f;
            visibilityCanvasGroup.interactable = visible;
            visibilityCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (visibilityTarget != null && visibilityTarget.activeSelf != visible)
        {
            visibilityTarget.SetActive(visible);
        }
    }

    private void AlignAboveEnemy()
    {
        transform.position = enemyTransform.position + worldOffset;
    }

    private void FaceActiveCamera()
    {
        Transform camTransform = ResolveCameraTransform();
        if (camTransform == null)
            return;

        if (alignToCameraRotation)
        {
            Quaternion target = camTransform.rotation;
            if (flipFacing)
                target *= Quaternion.Euler(0f, 180f, 0f);

            transform.rotation = target;
            return;
        }

        Vector3 toCamera = camTransform.position - transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        Quaternion facing = Quaternion.LookRotation(toCamera, camTransform.up);
        if (flipFacing)
            facing *= Quaternion.Euler(0f, 180f, 0f);

        transform.rotation = facing;
    }

    private Transform ResolveCameraTransform()
    {
        if (CameraManager.Instance != null)
        {
            CinemachineCamera cineCam = CameraManager.Instance.GetActiveCamera();
            if (cineCam != null)
                return cineCam.transform;
        }

        if (fallbackCameraTransform == null || !fallbackCameraTransform.gameObject.activeInHierarchy)
            fallbackCameraTransform = FindFallbackCameraTransform();

        return fallbackCameraTransform;
    }

    private Transform FindFallbackCameraTransform()
    {
        Camera best = null;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == null || !cam.enabled)
                continue;
            if ((cameraLayerMask.value & (1 << cam.gameObject.layer)) == 0)
                continue;

            if (best == null || cam.depth > best.depth)
                best = cam;
        }

        if (best == null)
            best = Camera.main;

        return best != null ? best.transform : null;
    }
}
