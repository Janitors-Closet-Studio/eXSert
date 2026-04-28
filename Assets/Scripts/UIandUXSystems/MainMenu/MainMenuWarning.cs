using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class MainMenuWarning : MonoBehaviour
{
    private enum WarningAction
    {
        None,
        QuitGame,
        DeleteSave,
        OverwriteSave
    }

    [SerializeField] private UnityEngine.InputSystem.InputActionReference confirmAction;
    [SerializeField] private GameObject warningContainer;
    [SerializeField] private SaveSlotsMenu saveSlotsMenu;
    [SerializeField] private MenuListManager menuListManager;

    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [SerializeField] private GameObject quitGameWarningText;
    [SerializeField] private GameObject deleteSaveWarningText;
    [SerializeField] private GameObject overwriteSaveWarningText;
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private FooterManager footerPanel;

    private List<GameObject> warningTextObjects = new List<GameObject>();

    private CanvasGroup warningCanvasGroup;
    private WarningAction pendingAction = WarningAction.None;
    private string pendingDeleteProfileId = string.Empty;

    public void Start()
    {
        warningTextObjects.Add(quitGameWarningText);
        warningTextObjects.Add(deleteSaveWarningText);
        warningTextObjects.Add(overwriteSaveWarningText);
    }


    private void ConfigureWarningDisplay(GameObject textObject, bool keepLastFooterVisible)
    {
        TurnOnOffFooter(keepLastFooterVisible);

        if (textObject == null)
            return;

        foreach (var warningText in warningTextObjects)
        {
            if (warningText != null && warningText != textObject)
                warningText.SetActive(false);
            else
                textObject.SetActive(true);
                
        }
    }

    private void TurnOnOffFooter(bool turnOn)
    {
        if (footerPanel == null || footerPanel.menuFooters == null)
            return;

        foreach (var menuFooter in footerPanel.menuFooters)
        {
            if (menuFooter.menuName == warningContainer)
            {
                menuFooter.keepFooterVisible = turnOn;
                return;
            }
        }

         
    }

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

    private void OnConfirmPressed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!IsWarningVisible())
            return;

        ExecutePendingAction();
        HideWarning();
    }

    public void OnOverwriteSaveButtonPressed()
    {
        if (saveSlotsMenu != null && !saveSlotsMenu.SelectedSlotHasData())
        {
            pendingAction = WarningAction.OverwriteSave;
            pendingDeleteProfileId = string.Empty;
            ExecuteOverwriteSave();
            return;
        }

        pendingAction = WarningAction.OverwriteSave;
        pendingDeleteProfileId = string.Empty;
        ConfigureWarningDisplay(overwriteSaveWarningText, false);
        ShowWarning();
        warningContainer.transform.SetAsLastSibling();
    }

    public void OnQuitButtonPressed()
    {
        pendingAction = WarningAction.QuitGame;
        pendingDeleteProfileId = string.Empty;
        ConfigureWarningDisplay(quitGameWarningText, false);
        ShowWarning();
        warningContainer.transform.SetAsLastSibling();
    }

    public void OnDeleteSaveButtonPressed()
    {
        pendingAction = WarningAction.DeleteSave;
        pendingDeleteProfileId = string.Empty;
        ConfigureWarningDisplay(deleteSaveWarningText, true);
        ShowWarning();
        warningContainer.transform.SetAsLastSibling();
    }

    public void ShowOverwriteSaveWarning()
    {
        if (saveSlotsMenu != null && !saveSlotsMenu.SelectedSlotHasData())
        {
            pendingAction = WarningAction.OverwriteSave;
            pendingDeleteProfileId = string.Empty;
            ExecuteOverwriteSave();
            return;
        }

        if (confirmButton != null) 
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => OnConfirmPressed(new UnityEngine.InputSystem.InputAction.CallbackContext()));
        }
        pendingAction = WarningAction.OverwriteSave;
        pendingDeleteProfileId = string.Empty;
        ConfigureWarningDisplay(overwriteSaveWarningText, true);
        ShowWarning();
        warningContainer.transform.SetAsLastSibling();
    }
    public void ShowQuitWarning()
    {

        if (confirmButton != null) 
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => OnConfirmPressed(new UnityEngine.InputSystem.InputAction.CallbackContext()));
        }

        pendingAction = WarningAction.QuitGame;
        pendingDeleteProfileId = string.Empty;
        ConfigureWarningDisplay(quitGameWarningText, false);
        ShowWarning();
        warningContainer.transform.SetAsLastSibling();
    }

    public void ShowDeleteSaveWarning()
    {
        if (confirmButton != null) 
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => OnConfirmPressed(new UnityEngine.InputSystem.InputAction.CallbackContext()));
        }

        pendingAction = WarningAction.DeleteSave;
        pendingDeleteProfileId = DataPersistenceManager.GetSelectedProfileId();
        ConfigureWarningDisplay(deleteSaveWarningText, true);
        ShowWarning();
        warningContainer.transform.SetAsLastSibling();
    }

    public void ShowDeleteSaveWarningForProfile(string profileId)
    {
        pendingAction = WarningAction.DeleteSave;
        pendingDeleteProfileId = profileId;
        ConfigureWarningDisplay(deleteSaveWarningText, true);
        ShowWarning();
        warningContainer.transform.SetAsLastSibling();
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
            case WarningAction.OverwriteSave:
                ExecuteOverwriteSave();
                break;

            default:
                break;
        }
    }

    private void ExecuteOverwriteSave()
    {
        if (saveSlotsMenu.isInLoadMenu)
            return;

        if (saveSlotsMenu != null)
        {
            saveSlotsMenu.OnDeleteSaveClicked();
            saveSlotsMenu.OnSaveSlotClicked();
            return;
        }

        string profileId = string.IsNullOrWhiteSpace(pendingDeleteProfileId)
            ? DataPersistenceManager.GetSelectedProfileId()
            : pendingDeleteProfileId;

        if (string.IsNullOrWhiteSpace(profileId))
            return;

        DataPersistenceManager.DeleteProfile(profileId);
        saveSlotsMenu.OnSaveSlotClicked();
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
            if (!warningContainer.activeSelf)
                warningContainer.SetActive(true);

           

            if (warningCanvasGroup == null)
            {
                warningCanvasGroup = warningContainer.GetComponent<CanvasGroup>();
                if (warningCanvasGroup == null)
                    warningCanvasGroup = warningContainer.AddComponent<CanvasGroup>();
            }

            warningCanvasGroup.interactable = true;

            if (menuListManager != null)
            {
                menuListManager.AddToMenuList(warningContainer);
            }
            else
            {
                warningCanvasGroup.alpha = 1f;
                warningCanvasGroup.blocksRaycasts = true;
                warningCanvasGroup.interactable = true;
            }
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
            warningContainer.SetActive(false);
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
