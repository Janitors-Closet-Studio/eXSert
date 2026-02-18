using UnityEngine;
using Singletons;
using System.Collections.Generic;
using System;


public class DiaryManager : Singleton<DiaryManager>
{
    [Header("Debug")]
    [SerializeField] private bool loadDiaryState = true;

    private Dictionary<string, Diaries> diaryMap;

    internal int foundDiaryCount = 0;

    protected override void Awake()
    {

        diaryMap = CreateDiaryMap();

        base.Awake();
    }

     private void OnEnable()
    {
        EventsManager.Instance.diaryEvents.onFoundDiary += FindDiary;
    }
    
    private void OnDisable()
    {
        if (EventsManager.Instance != null && EventsManager.Instance.diaryEvents != null)
            EventsManager.Instance.diaryEvents.onFoundDiary -= FindDiary;

    }

    private void Start()
    {
        foreach(Diaries diary in diaryMap.Values)
        {
            EventsManager.Instance.diaryEvents.DiaryStateChange(diary);
        }
    }

    /// <summary>
    /// Re-broadcasts all diary states. Call this when UI becomes active to populate buttons.
    /// </summary>
    public void RefreshAllDiaries()
    {
        foreach(Diaries diary in diaryMap.Values)
        {
            EventsManager.Instance.diaryEvents.DiaryStateChange(diary);
        }
    }

    //Changes the state of the diary and if it is Found, it will turn isDiaryFound truetrue
    private void FindDiary(string id)
    {
        Diaries diaries = GetDiaryById(id);
        Debug.Log($"FindDiary called for {id}: was isFound={diaries.info.isFound}");
        diaries.info.isFound = true;
        Debug.Log($"FindDiary set isFound=true for {id}, firing DiaryStateChange event");
        EventsManager.Instance.diaryEvents.DiaryStateChange(diaries);
    }

    //This dictionary will hold all the unique log entries and ensure there is no dupes
    private Dictionary<string, Diaries> CreateDiaryMap()
    {
        DiarySO[] allDiaries = Resources.LoadAll<DiarySO>("Diaries");

        Dictionary<string, Diaries> idToDiaryMap = new Dictionary<string, Diaries>();
        foreach (DiarySO diaryInfo in allDiaries)
        {
            if (idToDiaryMap == null)
            {
                Debug.LogWarning("Duplicate ID found when creating diary map: " + diaryInfo.diaryID);
            }
            idToDiaryMap.Add((diaryInfo.diaryID), LoadDiary(diaryInfo));
        }
        return idToDiaryMap;
    }

    //Used to grab the specifc id string in a log
    private Diaries GetDiaryById(string id)
    {
        Diaries diaries = diaryMap[id];
        if (diaries == null)
        {
            Debug.LogError("ID not found in Diary Map: " + id);
        }
        return diaries;
    }

    private void OnApplicationQuit()
    {
        foreach (Diaries diaries in diaryMap.Values)
        {
            SaveDiary(diaries);
        }
    }

    //Temporary save feature for logs
    private void SaveDiary(Diaries diaries)
    {
        try
        {
            DiaryData diaryData = diaries.GetDiaryData();
            string serializedData = JsonUtility.ToJson(diaryData);
            PlayerPrefs.SetString(diaries.info.diaryID, serializedData);
        }
        catch (SystemException e)
        {
            Debug.LogError("Failed to save log with id " + diaries.info.diaryID + ": " + e);
        }
    }

    private Diaries LoadDiary(DiarySO diaryInfo)
    {
        Diaries diary = null;
        try
        {
            if (PlayerPrefs.HasKey(diaryInfo.diaryID) && loadDiaryState)
            {
                string serializedData = PlayerPrefs.GetString(diaryInfo.diaryID);
                DiaryData diaryData = JsonUtility.FromJson<DiaryData>(serializedData);
                diary = new Diaries(diaryInfo);
                diary.info.isFound = diaryData.isFound;
                Debug.Log($"Loaded diary {diaryInfo.diaryID}: isFound={diaryData.isFound}");
            }
            else
            {
                diary = new Diaries(diaryInfo);
                diary.info.isFound = false; // Always start as not found if no saved data
                Debug.Log($"No saved data for diary {diaryInfo.diaryID}, created with isFound=false");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to load diary with id " + diaryInfo.diaryID + ": " + e);
            diary = new Diaries(diaryInfo);
            diary.info.isFound = false;
        }
        return diary;
    }
}
