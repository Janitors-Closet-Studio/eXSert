using UnityEngine;
using UnityEngine.InputSystem;

public class MainMenuWarning : MonoBehaviour
{
    private enum WarningAction
    {
        None,
        QuitGame,
        DeleteSave
    }

    [SerializeField] private InputActionReference confirmAction;
    [SerializeField] private GameObject warningContainer;
    [SerializeField] private SaveSlotsMenu saveSlotsMenu;

    private CanvasGroup warningCanvasGroup;
    private WarningAction pendingAction = WarningAction.None;
    private string pendingDeleteProfileId = string.Empty;

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
        if (!IsWarningVisible())
            return;

        ExecutePendingAction();
        HideWarning();
    }

    public void OnQuitButtonPressed()
    {
        pendingAction = WarningAction.QuitGame;
        pendingDeleteProfileId = string.Empty;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnDeleteSaveButtonPressed()
    {
        pendingAction = WarningAction.DeleteSave;
        pendingDeleteProfileId = string.Empty;

        ShowWarning();
    }

    public void ShowQuitWarning()
    {
        pendingAction = WarningAction.QuitGame;
        pendingDeleteProfileId = string.Empty;

        ShowWarning();
    }

    public void ShowDeleteSaveWarning()
    {
        pendingAction = WarningAction.DeleteSave;
        pendingDeleteProfileId = DataPersistenceManager.GetSelectedProfileId();

        ShowWarning();
    }

    public void ShowDeleteSaveWarningForProfile(string profileId)
    {
        pendingAction = WarningAction.DeleteSave;
        pendingDeleteProfileId = profileId;

        ShowWarning();
    }

    private void ExecutePendingAction()
    {
        switch (pendingAction)
        {
            case WarningAction.DeleteSave:
                ExecuteDeleteSave();
                break;

            case WarningAction.QuitGame:
                QuitApplication();
                break;

            default:
                break;
        }
    }

    private void ExecuteDeleteSave()
    {
        if (saveSlotsMenu != null)
        {
            saveSlotsMenu.OnDeleteSaveClicked();
            return;
        }

        string profileId = string.IsNullOrWhiteSpace(pendingDeleteProfileId)
            ? DataPersistenceManager.GetSelectedProfileId()
            : pendingDeleteProfileId;

        if (string.IsNullOrWhiteSpace(profileId))
            return;

        DataPersistenceManager.DeleteProfile(profileId);
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowWarning()
    {
        if (pendingAction == WarningAction.None)
            pendingAction = WarningAction.QuitGame;

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
            warningCanvasGroup.interactable = true;
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
            warningCanvasGroup.interactable = false;
        }

        pendingAction = WarningAction.None;
        pendingDeleteProfileId = string.Empty;
    }

    private bool IsWarningVisible()
    {
        return warningContainer != null
            && warningContainer.activeInHierarchy
            && warningCanvasGroup != null
            && warningCanvasGroup.alpha > 0.001f
            && warningCanvasGroup.blocksRaycasts;
    }

}
