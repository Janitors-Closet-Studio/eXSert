/*
    Scriptable objects for the hidden logs throughout the game.

    Written by Brandon Wahl
*/

using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.UI;

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
        string idName = this.name.Replace("Log", "");
        logID = "ENTRY #00" + idName;
        EditorUtility.SetDirty(this);

#endif


    }

    public void MarkAsFound()
    {
        isFound = true;
        LogRead?.Invoke();
        Debug.Log($"Log {logID} marked as found.");
    }

}


