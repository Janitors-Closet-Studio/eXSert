using UnityEngine;
using System;

[Serializable]
public class Objective
{
    public string DisplayText;

    public Objective(string text)
    {
        DisplayText = text;
    }

    public override string ToString() => DisplayText;
}

[Serializable]
public class SubObjective
{
    public string ID;
    public string DisplayText;
    public bool IsCompleted;

    public SubObjective(string id, string text)
    {
        ID = id;
        DisplayText = text;
        IsCompleted = false;
    }

    public override string ToString() => DisplayText;
}

[Serializable]
public class Notice
{
    public string Message;
    public float Duration; // How long the notice should be displayed before fading out
}
