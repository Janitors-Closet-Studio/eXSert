/*
    Written by Brandon

    Temp script to allow the diary state to be saved.
*/

[System.Serializable]
public class DiaryData
{
    public string diaryID;
    public bool isFound;
    public bool isRead;

    public DiaryData(DiarySO info)
    {
        this.diaryID = info.diaryID;
        this.isFound = info.isFound;
        this.isRead = info.isRead; // Set based on isRead value from DiarySO
    }
}

