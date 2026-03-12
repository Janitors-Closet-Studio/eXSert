using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "Cutscene", menuName = "Scriptable Objects/Cutscene")]
public class Cutscene : ScriptableObject
{
    public VideoClip videoClip;
    public string videoName => videoClip != null ? videoClip.name : "No Video Clip Assigned";
    public static implicit operator VideoClip(Cutscene cutscene) => cutscene.videoClip;

    public Cutscene(VideoClip videoClip)
    {
        this.videoClip = videoClip;
    }

    public static Cutscene GetCutscene(string name)
    {
        Cutscene cutscene = Resources.Load<Cutscene>($"{name}");
        if (cutscene == null)
            Debug.LogError($"Unable to find cutscene with name {name}. Ensure one is created in Cutscenes/Resources/Cutscenes or that name is spelled correctly");
        return cutscene;
    }
}
