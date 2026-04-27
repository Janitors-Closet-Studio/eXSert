using UnityEngine;
using UnityEngine.InputSystem;
public class MainMenuWarning : MonoBehaviour
{
    [SerializeField] private InputActionReference confirmAction;

    private void OnEnable()
    {
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

}
