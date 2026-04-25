using UnityEngine;

public static class MenuSelectionSuppression
{
    private static int suppressUntilFrame = -1;

    public static void SuppressForFrames(int frames = 2)
    {
        int duration = Mathf.Max(1, frames);
        suppressUntilFrame = Mathf.Max(suppressUntilFrame, Time.frameCount + duration);
    }

    public static bool IsSuppressed => Time.frameCount <= suppressUntilFrame;
}