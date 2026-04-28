/*
 * Written by Will T, inspired by Brandon's Puzzle Interface script
 * 
 * Abstract class for puzzle parts across different puzzles.
 * Helps organize puzzle-related scripts for PuzzleEncounter script.
 */

using System;
using UnityEngine;

public abstract class PuzzlePart : MonoBehaviour
{
    public bool isCompleted { get; protected set; }
    public event Action<PuzzlePart> PuzzleCompleted;

    protected void NotifyPuzzleCompleted()
    {
        PuzzleCompleted?.Invoke(this);
    }
    
    public abstract void StartPuzzle();
    public abstract void EndPuzzle();
    public abstract void ConsoleInteracted();
}
