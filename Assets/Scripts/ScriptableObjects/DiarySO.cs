using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "DiarySO", menuName = "NavigationMenu/Diaries", order = 1)]
public class DiarySO : ScriptableObject
{
    [field: SerializeField] public string diaryID { get; private set; }

    [field: SerializeField] public string diaryTitle { get; private set; }

    [TextArea(3, 10)]
    public string diaryDescription;

    public Image diaryImage;
    public bool isFound;
    public bool isRead;
    //This ensures that the idName cannot be repeated
    private void OnValidate()
    {

    #if UNITY_EDITOR
        string idName = this.name.Replace("Diary", "");
        diaryID = "#00" + idName;
        if (string.IsNullOrWhiteSpace(diaryTitle))
            diaryTitle = this.name;
        EditorUtility.SetDirty(this);

    #endif


    }
}
