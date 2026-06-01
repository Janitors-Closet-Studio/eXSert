/*
    Scriptable objects for the hidden logs throughout the game.

    Written by Brandon Wahl
*/

using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.UI;
using System.Text;

[Serializable]
[ExecuteInEditMode]
[CreateAssetMenu(fileName = "NavigationLogSO", menuName = "NavigationMenu/Logs", order = 1)]
public class NavigationLogSO : ScriptableObject
{


    [field: SerializeField] public string logID { get; private set; }
    public string logName;
    public string locationFound;

    [TextArea(3, 10)]
    public string logDescription;
    public Image logImage;
    public bool isFound;
    public bool isRead { get; private set; }

    public event Action LogRead;

    //This ensures that the idName cannot be repeated
    private void OnValidate()
    {

#if UNITY_EDITOR
        StringBuilder digits = new StringBuilder();

        foreach (char character in name)
        {
            if (char.IsDigit(character))
                digits.Append(character);
        }

        if (int.TryParse(digits.ToString(), out int logNumber))
            logID = $"ARCHIVE_{logNumber:D3}";
        EditorUtility.SetDirty(this);

#endif


    }

    public void MarkAsFound()
    {
        isFound = true;
        Debug.Log($"Log {logID} marked as found.");
    }

    public void MarkAsRead()
    {
        isFound = true;
        isRead = true;
        LogRead?.Invoke();
        Debug.Log($"Log {logID} marked as read.");
    }

    public void ApplySavedState(bool found, bool read)
    {
        isFound = found;
        isRead = read;
    }

}


