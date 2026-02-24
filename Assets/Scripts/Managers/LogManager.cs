/*
    This singleton is suppose to manage of the existing log scriptable objects in eXsert.
    It will manage if they're found or a duplicate, and temporarily handles the saving of the logs

    Written by Brandon Wahl
*/

using UnityEngine;
using Singletons;
using System.Collections.Generic;
using System;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LogManager : Singleton<LogManager>
{
    [Header("Debug")]
    [SerializeField] private bool loadLogState = true;
    [SerializeField] private GameObject logUi = null;
    [SerializeField] private GameObject playerHud = null;

    internal List<NavigationLogSO> unreadLogs = new List<NavigationLogSO>();

    private Dictionary<string, Logs> logMap;

    // Button function placeholder
    public void ClearAllLogs()
    {
        NavigationLogSO[] allLogs = Resources.LoadAll<NavigationLogSO>("Logs");

        foreach (NavigationLogSO logInfo in allLogs)
        {
            PlayerPrefs.DeleteKey(logInfo.logID);
        }
    }


    protected override void Awake()
    {
        Debug.Log(unreadLogs.Count);
        logMap = CreateLogMap();

        base.Awake();
    }

    private void OnEnable()
    {
        EventsManager.Instance.logEvents.onFoundLog += FindLog;
    }
    
    private void OnDisable()
    {
        if (EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
            EventsManager.Instance.logEvents.onFoundLog -= FindLog;

    }

    private void Start()
    {
        foreach(Logs log in logMap.Values)
        {
            EventsManager.Instance.logEvents.LogStateChange(log);
        }
    }

   

    /// <summary>
    /// Re-broadcasts all log states. Call this when UI becomes active to populate buttons.
    /// </summary>
    public void RefreshAllLogs()
    {
        foreach(Logs log in logMap.Values)
        {
            EventsManager.Instance.logEvents.LogStateChange(log);
        }
    }

    //Changes the state of the log and if it is Found, it will turn isLogFound true
    private void FindLog(string id)
    {
        Logs logs = GetLogById(id);
        logs.info.isFound = true;
        EventsManager.Instance.logEvents.LogStateChange(logs);
    }

    //This dictionary will hold all the unique log entries and ensure there is no dupes
    private Dictionary<string, Logs> CreateLogMap()
    {
        NavigationLogSO[] allLogs = Resources.LoadAll<NavigationLogSO>("Logs");

        Dictionary<string, Logs> idToLogMap = new Dictionary<string, Logs>();
        foreach (NavigationLogSO logInfo in allLogs)
        {

            idToLogMap.Add(logInfo.logID, LoadLog(logInfo));
        }
        return idToLogMap;
    }

    //Used to grab the specifc id string in a log
    private Logs GetLogById(string id)
    {
        Logs logs = logMap[id];
        return logs;
    }

    private void OnApplicationQuit()
    {
        foreach (Logs log in logMap.Values)
        {
            SaveLog(log);
        }
    }

    //Temporary save feature for logs
    private void SaveLog(Logs log)
    {
        try
        {
            LogData logData = log.GetLogData();
            string serializedData = JsonUtility.ToJson(logData);
            PlayerPrefs.SetString(log.info.logID, serializedData);
        }
        catch (SystemException e)
        {
            Debug.LogError("Failed to save log with id " + log.info.logID + ": " + e);
        }
    }

    private Logs LoadLog(NavigationLogSO logInfo)
    {
        Logs log = null;
        try
        {
            // Check if manually set to true in inspector (takes priority over saved data)
            bool inspectorValueIsTrue = logInfo.isFound;
            
            if (PlayerPrefs.HasKey(logInfo.logID) && loadLogState)
            {
                string serializedData = PlayerPrefs.GetString(logInfo.logID);
                LogData logData = JsonUtility.FromJson<LogData>(serializedData);
                log = new Logs(logInfo);
                
                // If inspector value is true, keep it; otherwise use saved data
                if (inspectorValueIsTrue)
                {
                    log.info.isFound = true;
                }
                else
                {
                    log.info.isFound = logData.isFound;
                }
            }
            else
            {
                log = new Logs(logInfo);
            }
        }
        catch (System.Exception e)
        {
            log = new Logs(logInfo);
        }
        
        return log;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LogManager))]
public class LogManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LogManager logManager = (LogManager)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Clear All Logs"))
        {
            logManager.ClearAllLogs();
        }
    }
}
#endif
