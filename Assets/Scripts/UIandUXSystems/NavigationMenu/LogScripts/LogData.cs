/*
    Temp file to store log data

    Written by Brandon Wahl
*/

[System.Serializable]
public class LogData
{
    public string logID;
    public bool isFound;
    public bool isRead;

    public LogData(NavigationLogSO info)
    {
        this.logID = info.logID;
        this.isFound = info.isFound;
        this.isRead = info.isRead; // Set based on isRead value from NavigationLogSO
    }
}
