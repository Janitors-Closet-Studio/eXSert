using UnityEngine;
using TMPro;
using System.ComponentModel;
using Unity.VisualScripting;
using System.Collections;



#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShowIfPause : PropertyAttribute {}
public class ShowIfNavigation : PropertyAttribute {}

public class FooterManager : MonoBehaviour
{
    public enum footerTypes { Pause, Navigation, Settings} 

    public footerTypes currentFooterType;

    [ShowIfNavigation]
    [SerializeField] private TMP_Text footerText;

    [ShowIfNavigation]
    [SerializeField] internal GameObject logHolderUI;

    [ShowIfNavigation]
    [SerializeField] internal GameObject diaryHolderUI;

    [ShowIfNavigation]
    [SerializeField] internal GameObject mainNavigationMenuHolderUI;

    [ShowIfNavigation]
    [SerializeField] internal GameObject IndividualLogUI;

    [ShowIfNavigation]
    [SerializeField] internal GameObject IndividualDiaryUI;

    [ShowIfNavigation]
    [SerializeField] internal GameObject overlayUI;

    [ShowIfNavigation]
    [SerializeField] internal GameObject ActsUI;

    [ShowIfPause]
    [SerializeField] internal GameObject settingsUI;

    [ShowIfPause]
    [SerializeField] internal GameObject controlsUI;

    [ShowIfPause]
    [SerializeField] internal GameObject pauseUI;

    public void CheckForFooterUpdate()
    {
        if (logHolderUI.activeSelf)
        {
            footerText.text = "Read Log Entries";
        }
        else if (diaryHolderUI.activeSelf)
        {
            footerText.text = "Read Diary Entries";
        }
        else if (mainNavigationMenuHolderUI.activeSelf)
        {
            footerText.text = "Explore Menu Options";
        }
        else if (IndividualLogUI.activeSelf)
        {
            footerText.text = "Return to Logs";
        }
        else if (IndividualDiaryUI.activeSelf)
        {
            footerText.text = "Return to Diary";
        }
        else if (ActsUI.activeSelf)
        {
            footerText.text = "Explore Previous Acts";
        }

    }

    private IEnumerator UpdateFooterMessage()
    {
        yield return new WaitForSeconds(0.1f); // Adjust the delay as needed
        CheckForFooterUpdate();
    }

    void OnEnable()
    {
        StartCoroutine(UpdateFooterMessage());
    }

}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ShowIfPause))]
public class ShowIfPauseDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        FooterManager footerManager = (FooterManager)property.serializedObject.targetObject;
        bool show = false;
        if (footerManager != null)
        {
            show = footerManager.currentFooterType == FooterManager.footerTypes.Pause;
        }

        if (show)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        FooterManager footerManager = (FooterManager)property.serializedObject.targetObject;
        if (footerManager != null && footerManager.currentFooterType == FooterManager.footerTypes.Pause)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        return 0f;
    }
}

[CustomPropertyDrawer(typeof(ShowIfNavigation))]
public class ShowIfNavigationDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        FooterManager footerManager = (FooterManager)property.serializedObject.targetObject;
        bool show = false;
        if (footerManager != null)
        {
            show = footerManager.currentFooterType == FooterManager.footerTypes.Navigation;
        }

        if (show)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        FooterManager footerManager = (FooterManager)property.serializedObject.targetObject;
        if (footerManager != null && footerManager.currentFooterType == FooterManager.footerTypes.Navigation)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        return 0f;
    }
}
#endif
