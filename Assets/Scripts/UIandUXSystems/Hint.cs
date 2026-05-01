using Managers.TimeLord;
using UnityEngine;
using UnityEngine.InputSystem;

public class Hint : MonoBehaviour
{
    public static bool isHintActive = false; // Static flag to track if the hint UI is active

    [SerializeField]
    private InputActionReference hintToggleAction;

    [SerializeField]
    private GameObject assignedHintUI;

    [SerializeField]
    private string hintMenuName = "Hint";

    [SerializeField]
    private string hintDescription = "This is a hint.";

    [SerializeField]
    [Min(0f)]
    private float uiActivationDelay = 0.15f;

    private string gameplayInputBlockOwnerId;
    private string pauseOwnerId;
    private Coroutine delayedUiActivationRoutine;

    private void Awake()
    {
        if (assignedHintUI != null)
            assignedHintUI.SetActive(false);

        // Hint should only become active when HintInteractions.OpenHint enables it.
        enabled = false;
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(gameplayInputBlockOwnerId))
            gameplayInputBlockOwnerId = InputReader.RequestGameplayInputBlock(
                $"Hint_{GetInstanceID()}");

        if (string.IsNullOrEmpty(pauseOwnerId))
            pauseOwnerId = PauseCoordinator.RequestPause($"Hint_{GetInstanceID()}");

        InputReader.inputBusy = true;
        isHintActive = true;
        EnableHintUI();

        delayedUiActivationRoutine = StartCoroutine(ActivateUiModeAfterDelay());
    }

    private void OnDisable()
    {
        if (delayedUiActivationRoutine != null)
        {
            StopCoroutine(delayedUiActivationRoutine);
            delayedUiActivationRoutine = null;
        }

        if (hintToggleAction != null)
            hintToggleAction.action.performed -= ToggleHintUI;

        CloseAssignedHintUI();

        if (!string.IsNullOrEmpty(gameplayInputBlockOwnerId))
        {
            InputReader.ReleaseGameplayInputBlock(gameplayInputBlockOwnerId);
            gameplayInputBlockOwnerId = null;
        }

        if (!string.IsNullOrEmpty(pauseOwnerId))
        {
            PauseCoordinator.ReleaseTimeScale(pauseOwnerId);
            pauseOwnerId = null;
        }

        InputReader.inputBusy = false;

        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");

        isHintActive = false;
    }

    private System.Collections.IEnumerator ActivateUiModeAfterDelay()
    {
        if (uiActivationDelay > 0f)
            yield return new WaitForSecondsRealtime(uiActivationDelay);

        if (!isActiveAndEnabled)
            yield break;

        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.SwitchCurrentActionMap("UI");

        if (hintToggleAction != null)
        {
            if (!hintToggleAction.action.enabled)
                hintToggleAction.action.Enable();

            hintToggleAction.action.performed -= ToggleHintUI;
            hintToggleAction.action.performed += ToggleHintUI;
        }

        delayedUiActivationRoutine = null;
    }

    public bool OpenHint()
    {
        if (assignedHintUI == null)
        {
            Debug.LogWarning($"[Hint] No assigned hint UI set for {gameObject.name}.");
            return false;
        }

        if (!enabled)
            enabled = true;
        else
            EnableHintUI();

        return true;
    }

    public void EnableHintUI()
    {
        if (assignedHintUI == null)
            return;

        assignedHintUI.SetActive(true);

        if (InteractionUI.Instance == null)
            return;

        if (InteractionUI.Instance._hintNameText != null)
            InteractionUI.Instance._hintNameText.text = hintMenuName;

        if (InteractionUI.Instance._hintDescriptionText != null)
            InteractionUI.Instance._hintDescriptionText.text = hintDescription;
    }

    private void ToggleHintUI(InputAction.CallbackContext context)
    {
        CloseHint();
    }

    public void CloseHint()
    {
        CloseAssignedHintUI();

        if (enabled)
            enabled = false;
    }

    private void CloseAssignedHintUI()
    {
        if (assignedHintUI != null && assignedHintUI.activeSelf)
            assignedHintUI.SetActive(false);
    }
}
