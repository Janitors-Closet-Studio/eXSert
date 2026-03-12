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
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void HideInteractPrompt()
    {
        if (_interactText != null)
        {
            _interactText.gameObject.SetActive(false);
            if (_interactText.transform.parent != null)
                _interactText.transform.parent.gameObject.SetActive(false);
        }

        if (_interactIcon != null)
            _interactIcon.gameObject.SetActive(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideInteractPrompt();
    }

}
