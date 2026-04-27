using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Singletons;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;


public class InteractionUI : Singleton<InteractionUI>
    
{
    // Tracks which interactable is currently responsible for the prompt
    public MonoBehaviour currentInteractable;

    [Header("Global Interaction UI")]
    public TMP_Text _interactText;
    public Image _interactIcon;
    public TMP_Text _collectText;
    public TMP_Text _collectBottomText;
    public TMP_Text _hintNameText;
    public TMP_Text _hintDescriptionText;
    public GameObject hintUI;
    public GameObject collectUI;

    internal List<string> collectablesToFind = new List<string>();

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
        HideNotice();

    }

    private void HideNotice()
    {
        if (_collectText != null)
            _collectText.text = "";

        if (_collectBottomText != null)
            _collectBottomText.text = "";

        if (collectUI != null)
            collectUI.SetActive(false);
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

    public void WriteTextToCollectUI(string text, string bottomText)
    {
        if (_collectText != null)
            _collectText.text = text;

        if (_collectBottomText != null)
            _collectBottomText.text = bottomText;

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

        currentInteractable = null;

        
    }

    public void ShowInteractIconImmediate()
    {
        if (_interactIcon == null)
            return;

        if (_interactIcon.transform.parent != null)
            _interactIcon.transform.parent.gameObject.SetActive(true);

        _interactIcon.enabled = true;
        _interactIcon.gameObject.SetActive(true);

        KeybindIconSwapper keybindIconSwapper = _interactIcon.GetComponent<KeybindIconSwapper>();
        if (keybindIconSwapper != null)
            keybindIconSwapper.RefreshIcon();
    }

    public void ShowInteractPromptImmediate()
    {
        if (_interactText != null)
        {
            _interactText.gameObject.SetActive(true);
            if (_interactText.transform.parent != null)
                _interactText.transform.parent.gameObject.SetActive(true);
        }

        ShowInteractIconImmediate();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideInteractPrompt();
    }

    
}