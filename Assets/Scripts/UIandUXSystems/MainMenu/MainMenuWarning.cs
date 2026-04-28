using UnityEngine;
using UnityEngine.InputSystem;
public class MainMenuWarning : MonoBehaviour
{
    [SerializeField] private InputActionReference confirmAction;
    [SerializeField] private GameObject warningContainer;
    private CanvasGroup warningCanvasGroup;

    private void OnEnable()
    {
        if (warningContainer != null && warningCanvasGroup == null)
            warningCanvasGroup = warningContainer.GetComponent<CanvasGroup>();
        if (warningContainer != null && warningCanvasGroup == null)
            warningCanvasGroup = warningContainer.AddComponent<CanvasGroup>();

        if (confirmAction != null)
        {
            confirmAction.action.performed += OnConfirmPressed;
        }
    }

    private void OnDisable()
    {
        if (confirmAction != null)
        {
            confirmAction.action.performed -= OnConfirmPressed;
        }
    }

    private void OnConfirmPressed(InputAction.CallbackContext context)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif

        Application.Quit();
    }

    public void OnQuitButtonPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowWarning()
    {
        if (warningContainer != null)
        {
            if (warningCanvasGroup == null)
            {
                warningCanvasGroup = warningContainer.GetComponent<CanvasGroup>();
                if (warningCanvasGroup == null)
                    warningCanvasGroup = warningContainer.AddComponent<CanvasGroup>();
            }
            warningCanvasGroup.alpha = 1f;
            warningCanvasGroup.blocksRaycasts = true;
        }
    }

    public void HideWarning()
    {
        if (warningContainer != null)
        {
            if (warningCanvasGroup == null)
            {
                warningCanvasGroup = warningContainer.GetComponent<CanvasGroup>();
                if (warningCanvasGroup == null)
                    warningCanvasGroup = warningContainer.AddComponent<CanvasGroup>();
            }
            warningCanvasGroup.alpha = 0f;
            warningCanvasGroup.blocksRaycasts = false;
        }
    }

}
