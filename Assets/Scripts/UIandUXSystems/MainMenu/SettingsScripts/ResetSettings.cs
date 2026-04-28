using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
public class ResetSettings : MonoBehaviour
{
    public static bool WarningOpen;
    [SerializeField] private InputActionReference _resetAction;
    [SerializeField] private InputActionReference activateWarning;
    [SerializeField] private InputActionReference closeWarning;

    [Header("Settings Containers")]
    [SerializeField] private GameObject settingsContainer;
    [SerializeField] private GeneralSettings generalSettingsContainer;
    [SerializeField] private GraphicsSettings graphicsSettingsContainer;
    [SerializeField] private AudioSettings audioSettingsContainer;

    [SerializeField] private FadeMenus fadeMenus;

    [SerializeField] private GameObject warningContainer;

    private System.Action<InputAction.CallbackContext> _resetPerformedHandler;
    private System.Action<InputAction.CallbackContext> _openWarningHandler;
    private System.Action<InputAction.CallbackContext> _closeWarningHandler;
    private Selectable lastSelected;
    private Coroutine _warningSelectionLockRoutine;
    private CanvasGroup _warningCanvasGroup;
    private bool _isClosingWarning;

    private void OnEnable()
    {
        _resetPerformedHandler ??= _ => ResetAllSettings();
        _openWarningHandler ??= _ => OpenWarning();
        _closeWarningHandler ??= _ => CloseWarning();

        CanvasGroup warningCanvas = GetOrCreateWarningCanvasGroup();
        if (warningCanvas != null && !WarningOpen)
        {
            warningCanvas.alpha = 0f;
            warningCanvas.blocksRaycasts = false;
            warningCanvas.interactable = false;
        }
    
        if (closeWarning != null && closeWarning.action != null)
            closeWarning.action.performed += _closeWarningHandler;
        else
            Debug.LogWarning("Close Warning action reference is not set. The warning cannot be closed with a button press.");

        if (_resetAction != null && _resetAction.action != null)
            _resetAction.action.performed += _resetPerformedHandler;
        if (activateWarning != null && activateWarning.action != null)
            activateWarning.action.performed += _openWarningHandler;
    }

    private void OnDisable()
    {
        if (_resetAction != null && _resetAction.action != null)
            _resetAction.action.performed -= _resetPerformedHandler;
        if (activateWarning != null && activateWarning.action != null)
            activateWarning.action.performed -= _openWarningHandler;
        if (closeWarning != null && closeWarning.action != null)
            closeWarning.action.performed -= _closeWarningHandler;

        if (_warningSelectionLockRoutine != null)
        {
            StopCoroutine(_warningSelectionLockRoutine);
            _warningSelectionLockRoutine = null;
        }

        WarningOpen = false;
        _isClosingWarning = false;
    }

    public void OpenWarning()
    {
        if (WarningOpen)
            return;

        if (EventSystem.current == null)
            return;

        EnsureWarningHierarchyActive();
        GetOrCreateWarningCanvasGroup();

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != null)
        {
            bool selectedInsideWarning = warningContainer != null && currentSelected.transform.IsChildOf(warningContainer.transform);
            if (!selectedInsideWarning)
                lastSelected = currentSelected.GetComponent<Selectable>();
        }

        WarningOpen = true;

        // Force fade-in to start from transparent so first activation always animates.
        if (_warningCanvasGroup != null)
            _warningCanvasGroup.alpha = 0f;

        StartCoroutine(FadeWarningCanvas(1f, .25f));

        EventSystem.current.SetSelectedGameObject(null);
        Selectable selectableInWarning = FindSelectableInWarning();
        if (selectableInWarning != null)
            EventSystem.current.SetSelectedGameObject(selectableInWarning.gameObject);

        if (_warningSelectionLockRoutine != null)
            StopCoroutine(_warningSelectionLockRoutine);
        _warningSelectionLockRoutine = StartCoroutine(KeepSelectionInsideWarning());
    }

    private Selectable FindSelectableInWarning()
    {
        if (warningContainer == null)
            return null;

        return warningContainer.GetComponentInChildren<Selectable>(true);
    }

    public void ResetAllSettings()
    {
        if (!settingsContainer.activeInHierarchy)
            return;

        if (!WarningOpen)
            return;

        if (generalSettingsContainer != null)
            generalSettingsContainer.ResetButton();
        if (graphicsSettingsContainer != null)
            graphicsSettingsContainer.ResetButton();
        if (audioSettingsContainer != null)
            audioSettingsContainer.ResetButton();

        StartCoroutine(CloseWarningAndRestoreSelection());
    }

    public void CloseWarning()
    {
        if (!WarningOpen || _isClosingWarning)
            return;

        StartCoroutine(CloseWarningAndRestoreSelection());
    }

    private IEnumerator KeepSelectionInsideWarning()
    {
        while (WarningOpen)
        {
            if (EventSystem.current == null)
                yield break;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            bool valid = selected != null
                && warningContainer != null
                && selected.transform.IsChildOf(warningContainer.transform)
                && selected.activeInHierarchy;

            if (!valid)
            {
                Selectable selectableInWarning = FindSelectableInWarning();
                if (selectableInWarning != null)
                    EventSystem.current.SetSelectedGameObject(selectableInWarning.gameObject);
            }

            yield return null;
        }
    }

    private IEnumerator CloseWarningAndRestoreSelection()
    {
        if (!WarningOpen || _isClosingWarning)
            yield break;

        _isClosingWarning = true;
        WarningOpen = false;

        if (_warningSelectionLockRoutine != null)
        {
            StopCoroutine(_warningSelectionLockRoutine);
            _warningSelectionLockRoutine = null;
        }

        yield return StartCoroutine(FadeWarningCanvas(0f, .25f));

        if (EventSystem.current != null)
        {
            Selectable target = (lastSelected != null && lastSelected.gameObject.activeInHierarchy && lastSelected.IsInteractable())
                ? lastSelected
                : settingsContainer != null ? settingsContainer.GetComponentInChildren<Selectable>(true) : null;

            if (target != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        _isClosingWarning = false;
    }

    private void EnsureWarningHierarchyActive()
    {
        if (warningContainer == null)
            return;

        if (!warningContainer.activeSelf)
            warningContainer.SetActive(true);

        // Keep warning children active so they do not disappear between opens.
        Transform[] descendants = warningContainer.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            GameObject child = descendants[i].gameObject;
            if (!child.activeSelf)
                child.SetActive(true);
        }
    }

    private CanvasGroup GetOrCreateWarningCanvasGroup()
    {
        if (warningContainer == null)
            return null;

        if (_warningCanvasGroup == null)
            _warningCanvasGroup = warningContainer.GetComponent<CanvasGroup>();
        if (_warningCanvasGroup == null)
            _warningCanvasGroup = warningContainer.AddComponent<CanvasGroup>();

        return _warningCanvasGroup;
    }

    private IEnumerator FadeWarningCanvas(float targetAlpha, float duration)
    {
        CanvasGroup canvasGroup = GetOrCreateWarningCanvasGroup();
        if (canvasGroup == null)
            yield break;

        if (targetAlpha > 0.5f)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0.5f)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

}
