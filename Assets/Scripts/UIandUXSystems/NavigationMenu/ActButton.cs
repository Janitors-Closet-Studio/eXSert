using UnityEngine;

/*
    Written by Brandon Wahl

    This script will handle the functionality of the act buttons in the navigation menu
    In the future, it will send players back to previous completed acts
*/

public class ActButton : MonoBehaviour
{
    [SerializeField] private int actNumber = 0; //0-4 

    [SerializeField] private GameObject sceneTriggerBox = null;

    public void OnActButtonClick()
    {
        // Get the current profileId
        string profileId = DataPersistenceManager.GetSelectedProfileId();
        if (string.IsNullOrEmpty(profileId))
            profileId = "default";

        // Check if this act is completed for the current profile
        bool isCompleted = ActsManager.Instance.GetFarthestUnlockedActName(profileId) != null;
        // Optionally, check for exact act number completion:
        // bool isCompleted = ActsManager.Instance.IsActCompleted(profileId, actNumber);

        if (isCompleted)
        {
            TeleportPlayerToAct();
        }
    }


    private void TeleportPlayerToAct()
    {
        if(sceneTriggerBox != null)
        {
            var player = GameObject.FindGameObjectWithTag("Player"); // Finds player
            if (player != null)
                player.transform.position = sceneTriggerBox.transform.position;
            
        }
    }
    
}
