using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public class Hint : MonoBehaviour
{
    public static bool isHintActive = false; // Static flag to track if the hint UI is active

    [SerializeField] private InputActionReference hintToggleAction;
    [SerializeField] private string hintMenuName = "Hint";
    [SerializeField] private string hintDescription = "This is a hint.";
    private MenuListManager menuListManager;

    private void Start()
    {
        menuListManager = InteractionUI.Instance.GetComponentInParent<MenuListManager>();
        if (menuListManager == null)
        {
            Debug.LogError("HintUI: No MenuListManager found in the scene.");
        }
    }


    private void OnEnable()
    {
        InputReader.PlayerInput.SwitchCurrentActionMap("UI");
        // Switch to Menu/UI action map
        hintToggleAction.action.performed += ToggleHintUI;
        Time.timeScale = 0f; // Pause the game when the hint UI is enabled
        isHintActive = true;
    }

    private void OnDisable()
    {
        hintToggleAction.action.performed -= ToggleHintUI;
        // Switch back to Gameplay action map
        InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");
        Time.timeScale = 1f; // Resume the game when the hint UI is disabled
        isHintActive = false;
    }

    public void EnableHintUI()
    {
        if (InteractionUI.Instance.hintUI != null)
            menuListManager.AddToMenuList(InteractionUI.Instance.hintUI);

        InteractionUI.Instance._hintNameText.text = hintMenuName;
        InteractionUI.Instance._hintDescriptionText.text = hintDescription;
    }

    private void ToggleHintUI(InputAction.CallbackContext context)
    {
        if (menuListManager != null && InteractionUI.Instance != null && InteractionUI.Instance.hintUI != null)
            menuListManager.GoBackToPreviousMenu();
    }

}
