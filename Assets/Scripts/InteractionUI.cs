using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Singletons;
using UnityEngine.SceneManagement;
public class InteractionUI : Singleton<InteractionUI>
{

    [Header("Global Interaction UI")]
    public TMP_Text _interactText;
    public Image _interactIcon;
    public TMP_Text _collectText;
    public TMP_Text _collectBottomText;
    public TMP_Text _hintNameText;
    public TMP_Text _hintDescriptionText;
    public GameObject hintUI;
    public GameObject collectUI;

    public static InteractionUI TryGetExisting()
    {
        if (isApplicationQuitting)
            return null;

        InteractionUI[] interactionUIs = FindObjectsByType<InteractionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return interactionUIs.Length > 0 ? interactionUIs[0] : null;
    }

    protected override void Awake()
    {
        base.Awake();
        HideInteractPrompt();
        HideCollectUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        Debug.Log("turning off interaction UI and unsubscribing from scene loaded event\n" + System.Environment.StackTrace);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void HideInteractPrompt()
    {
        if (_interactText != null)
        {
            _interactText.gameObject.SetActive(false);
            if (_interactText.transform != null)
                _interactText.transform.gameObject.SetActive(false);
        }

        if (_interactIcon != null)
            _interactIcon.gameObject.SetActive(false);
    }

    public void HideCollectUI()
    {
        if (collectUI != null)
            collectUI.SetActive(false);
        if (_collectText != null)            
            _collectText.gameObject.SetActive(false);
        if (_collectBottomText != null)           
             _collectBottomText.gameObject.SetActive(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideInteractPrompt();
    }

}
