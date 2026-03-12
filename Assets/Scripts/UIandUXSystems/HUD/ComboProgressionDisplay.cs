using UnityEngine;

public class ComboProgressionDisplay : MonoBehaviour
{
    [SerializeField] private GameObject comboProgressionUI;
    void Start()
    {
        if(comboProgressionUI == null)
        {
            Debug.LogWarning("The combo progression display is currently empty");
        }
    }

    public void ManageUIVisibility()
    {
        if (SettingsManager.Instance.comboProgression)
        {
            comboProgressionUI.SetActive(true);
        }
        else
        {
            comboProgressionUI.SetActive(false);
        }
    }
}
