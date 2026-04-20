using UnityEngine;
using UnityEngine.UI;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
        StringBuilder digits = new StringBuilder();

        foreach (char character in name)
        {
            if (char.IsDigit(character))
                digits.Append(character);
        }

        if (int.TryParse(digits.ToString(), out int diaryNumber))
            diaryID = $"ENTRY #{diaryNumber:D2}";

        if (string.IsNullOrWhiteSpace(diaryTitle))
            diaryTitle = name;

        EditorUtility.SetDirty(this);
#endif
    }
}
