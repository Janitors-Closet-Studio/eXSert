using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

internal class ObjectiveText : MonoBehaviour
{
    public static event System.Action ObjectiveTypingCompleted;
    public static bool IsCurrentObjectiveTyping { get; private set; }

    [SerializeField, CriticalReference]
    private TextMeshProUGUI HUDText;

    [SerializeField]
    private float typingSpeed = 0.03f;

    private Objective currentMessage;
    private string currentMessageString => currentMessage?.DisplayText ?? "";
    private bool isSubscribed;
    private Coroutine typingMonitorRoutine;
    private int typingRequestId;

    [Header("Debug")]
    [Tooltip("Enable verbose ObjectiveText debug logs.")]
    [SerializeField] private bool debugLogging = false;

    private void OnEnable()
    {
        ObjectiveManager.OnObjectiveChanged += UpdateText;
        SubscribeToPlayerInput();
        InputSystem.onActionChange += HandleActionChange;
    }

    private void OnDisable()
    {
        ObjectiveManager.OnObjectiveChanged -= UpdateText;
        UnsubscribeFromPlayerInput();
        InputSystem.onActionChange -= HandleActionChange;

        if (typingMonitorRoutine != null)
        {
            StopCoroutine(typingMonitorRoutine);
            typingMonitorRoutine = null;
        }

        IsCurrentObjectiveTyping = false;
    }

    private void UpdateText(Objective newObjective)
    {
        if (debugLogging) Debug.Log($"[HUDTextHandler] Setting new message: {newObjective}");
        currentMessage = newObjective;

        RefreshCurrentText();
    }

    private void HandleActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.BoundControlsChanged)
            return;

        RefreshCurrentText();
    }

    private void HandleControlsChanged(PlayerInput _)
    {
        RefreshCurrentText();
    }

    private void SubscribeToPlayerInput()
    {
        if (isSubscribed || InputReader.PlayerInput == null)
            return;

        InputReader.PlayerInput.onControlsChanged += HandleControlsChanged;
        isSubscribed = true;
    }

    private void UnsubscribeFromPlayerInput()
    {
        if (!isSubscribed)
            return;

        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.onControlsChanged -= HandleControlsChanged;

        isSubscribed = false;
    }

    private void RefreshCurrentText()
    {
        if (HUDText == null)
        {
            IsCurrentObjectiveTyping = false;
            return;
        }

        string formattedText = KeybindRichTextFormatter.Format(HUDText, currentMessageString);
        WritingTextUI.TextWriterSingle writer = WritingTextUI.AddWriter_Static(HUDText, formattedText, typingSpeed, false);

        if (typingMonitorRoutine != null)
            StopCoroutine(typingMonitorRoutine);

        typingRequestId++;
        IsCurrentObjectiveTyping = !string.IsNullOrEmpty(formattedText) && writer != null && writer.IsActive();

        if (IsCurrentObjectiveTyping)
            typingMonitorRoutine = StartCoroutine(WaitForTypingToFinish(writer, typingRequestId));
    }

    private IEnumerator WaitForTypingToFinish(WritingTextUI.TextWriterSingle writer, int requestId)
    {
        while (writer != null && writer.IsActive())
            yield return null;

        if (requestId != typingRequestId)
            yield break;

        typingMonitorRoutine = null;
        IsCurrentObjectiveTyping = false;
        ObjectiveTypingCompleted?.Invoke();
    }

    // Probably remove below. I'm keeping it for now in case it is actually important

    public void FadeOutText(float delay)
    {
        StartCoroutine(FadeOutObjectiveText(delay));
    }

    private IEnumerator FadeOutObjectiveText(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (HUDText != null)
        {
            float alpha = 1f;
            while (alpha > 0f)
            {
                alpha -= Time.deltaTime / 0.5f; // Fade out over 0.5 seconds
                HUDText.color = new Color(HUDText.color.r, HUDText.color.g, HUDText.color.b, alpha);
                yield return null;
            }
            HUDText.text = ""; // Clear text after fade out
        }
    }
}
