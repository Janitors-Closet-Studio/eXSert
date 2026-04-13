using UnityEngine;
using Singletons;
using System;
using System.Collections.Generic;
using System.Collections;

public class ObjectiveManager : Singleton<ObjectiveManager>
{
    #region Inspector Setup
    [SerializeField]
    private float completionFadeDuration = 2f;
    #endregion
    public static event Action<Objective> OnObjectiveChanged;
    public static event Action<List<Objective>> OnSubObjectivesUpdated;
    public static event Action<Notice> OnNoticeQueued;

    private static Objective currentObjective;
    private static readonly List<Objective> subObjectives = new();
    private static readonly Queue<Notice> noticeList = new();

    public static void SetMainObjective(string id, string text)
    {
        currentObjective = new Objective(id, text);
        OnObjectiveChanged?.Invoke(currentObjective);
    }

    public static void SetMainObjective(Objective objective)
    {
        currentObjective = objective;
        OnObjectiveChanged?.Invoke(currentObjective);
    }

    public static void ClearMainObjective()
    {
        currentObjective = null;
        OnObjectiveChanged?.Invoke(null);
    }


    // --- Sub Objectives ---


    public void AddSubObjective(string id, string text)
    {
        if (subObjectives.Exists(obj => obj.ID == id))
        {
            Debug.LogWarning($"[Objective Manager] Sub-objective with ID '{id}' already exists. Skipping addition.");
            return;
        }

        var newSubObjective = new Objective(id, text);
        subObjectives.Add(newSubObjective);
        OnSubObjectivesUpdated?.Invoke(subObjectives);
    }

    public void CompleteSubObjective(string id)
    {
        var subObjective = subObjectives.Find(obj => obj.ID == id);
        if (subObjective == null)
        {
            Debug.LogWarning($"[Objective Manager] Sub-objective with ID '{id}' not found. Cannot complete.");
            return;
        }

        subObjective.IsCompleted = true;
        OnSubObjectivesUpdated?.Invoke(subObjectives);

        StartCoroutine(RemoveAfterDelay());

        IEnumerator RemoveAfterDelay()
        {
            yield return new WaitForSeconds(completionFadeDuration);
            subObjectives.Remove(subObjective);
            OnSubObjectivesUpdated?.Invoke(subObjectives);
        }
    }

    public static void RemoveSubObjective(string id)
    {
        subObjectives.RemoveAll(obj => obj.ID == id);
        OnSubObjectivesUpdated?.Invoke(subObjectives);
    }


    // --- Notices ---


    /*
     * Notices are done by Brandon. 
     * If they wanted to be handled by the same central system then move them here.
     * Until then, this will be empty for now.
     */
}
