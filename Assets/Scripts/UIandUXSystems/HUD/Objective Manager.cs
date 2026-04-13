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
    public static event Action<List<SubObjective>> OnSubObjectivesUpdated;
    public static event Action<Notice> OnNoticeQueued;

    private static Objective currentObjective;
    private static readonly List<SubObjective> subObjectives = new();
    private static readonly Queue<Notice> noticeList = new();

    public static void SetMainObjective(string text)
    {
        currentObjective = new Objective(text);
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

    /// <summary>
    /// Adds a new sub-objective to the list and notifies listeners. If an objective with the same ID already exists, it will not be added again.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="text"></param>
    public static void AddSubObjective(string id, string text)
    {
        if (subObjectives.Exists(obj => obj.ID == id))
        {
            Debug.LogWarning($"[Objective Manager] Sub-objective with ID '{id}' already exists. Skipping addition.");
            return;
        }

        var newSubObjective = new SubObjective(id, text);
        subObjectives.Add(newSubObjective);
        OnSubObjectivesUpdated?.Invoke(subObjectives);
    }

    /// <summary>
    /// Sets the sub-objective with the given ID as completed, then starts a coroutine to remove it after a delay. If no sub-objective with the given ID is found, a warning is logged and no action is taken.
    /// </summary>
    /// <param name="id"></param>
    public static void CompleteSubObjective(string id)
    {
        var subObjective = subObjectives.Find(obj => obj.ID == id);
        if (subObjective == null)
        {
            Debug.LogWarning($"[Objective Manager] Sub-objective with ID '{id}' not found. Cannot complete.");
            return;
        }

        subObjective.IsCompleted = true;
        OnSubObjectivesUpdated?.Invoke(subObjectives);

        Instance.StartCoroutine(RemoveAfterDelay());

        IEnumerator RemoveAfterDelay()
        {
            yield return new WaitForSeconds(Instance.completionFadeDuration);
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
