using UnityEngine;
using System;

[Serializable]
public class Objective
{
    public string ID; // A key unique to the message to help identify it
    public string DisplayText;
    public bool IsCompleted;

    public Objective(string id, string text)
    {
        ID = id;
        DisplayText = text;
        IsCompleted = false;
    }
}

[Serializable]
public class Notice
{
    public string Message;
    public float Duration; // How long the notice should be displayed before fading out
}
