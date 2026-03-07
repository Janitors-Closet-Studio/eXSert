using System.Collections;
using UnityEngine;

public class MusicFader : MonoBehaviour
{

    [SerializeField] private float fadeDuration = 2f;

    public void FadeOutMusic()
    {
        if (SoundManager.Instance == null)
            return;

        SoundManager.Instance.FadeOutMusic(fadeDuration);
    }
}
