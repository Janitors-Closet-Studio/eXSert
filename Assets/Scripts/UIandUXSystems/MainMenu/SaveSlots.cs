/*
Written by Brandon Wahl

Changes text on the save slots depending on if there is data assigned to that save slot

*/

using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class SaveSlots : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private string profileId = "";

    [Header("Content")]
    [SerializeField] private GameObject noDataContent;
    [SerializeField] private GameObject hasDataContent;
    private Button saveSlotButton;

    [SerializeField] private SaveSlotsMenu saveSlotsMenu;

    private void Awake()
    {
        saveSlotButton = this.GetComponent<Button>();
    }

    //Depending on if the data is null or not, it will show their respective texts
    public void SetData(GameData data)
    {
        if(data == null)
        {
            noDataContent.SetActive(true);
            hasDataContent.SetActive(false);
        }
        else if (data != null)
        {
            noDataContent.SetActive(false);
            hasDataContent.SetActive(true);
        }
        else
        {
            Debug.LogError("SaveSlots: Data is null for " + gameObject.name);
        }
    }

    //Gathers the individual profileId being used
    public string GetProfileId()
    {
        return this.profileId;
    }

    public void SetCurrentSaveSlot()
    {
        saveSlotsMenu.currentSaveSlotSelected = this.gameObject.GetComponent<SaveSlots>();
    }

    //Sets interactability of save slots
    public void SetInteractable(bool interactable)
    {
        if (saveSlotButton != null)
            saveSlotButton.interactable = interactable;
        else
            Debug.LogError("SaveSlots: Button component missing on " + gameObject.name);
    }
}
