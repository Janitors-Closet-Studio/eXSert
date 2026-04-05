using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using Unity.VisualScripting;

public class WritingTextUI : MonoBehaviour
{
    private static WritingTextUI instance;
    private static List<Coroutine> activeCoroutines = new List<Coroutine>();
    private List<TextWriterSingle> textWriterSingles;
  
    public static List<AudioClip> keyboardTypingSounds = new List<AudioClip>();
    public List<AudioClip> keyboardTypingSoundsList = new List<AudioClip>();

    private void Awake()
    {
        instance = this;
        textWriterSingles = new List<TextWriterSingle>();

        foreach (AudioClip clip in keyboardTypingSoundsList)
        {
            if (clip != null)
                keyboardTypingSounds.Add(clip);
        }
    }


    public static TextWriterSingle AddWriter_Static(TextMeshProUGUI textComponent, string textToWrite, float timePerCharacter, bool invisibleCharacters, bool removeWriterBeforeAdd = true)
    {
        Debug.Log($"[WritingTextUI] AddWriter_Static called. textComponent: {textComponent}, textToWrite: '{textToWrite}', timePerCharacter: {timePerCharacter}, invisibleCharacters: {invisibleCharacters}, removeWriterBeforeAdd: {removeWriterBeforeAdd}");
        if (removeWriterBeforeAdd)
            instance.RemoveWriter(textComponent);

        return instance.AddWriter(textComponent, textToWrite, timePerCharacter, invisibleCharacters);
    }

    private TextWriterSingle AddWriter(TextMeshProUGUI textComponent, string textToWrite, float timePerCharacter, bool invisibleCharacters)
    {
        var writer = new TextWriterSingle();
        writer.AddWriter(textComponent, textToWrite, timePerCharacter, invisibleCharacters);
        textWriterSingles.Add(writer);
        // Start the coroutine for this writer independently
        Coroutine coroutine = StartCoroutine(writer.WriteTextCoroutine());
        activeCoroutines.Add(coroutine);
        return writer;
    }

    public static void RemoveWriter_Static(TextMeshProUGUI text)
    {
        instance.RemoveWriter(text);
    }

    private void RemoveWriter(TextMeshProUGUI text)
    {
        for (int i = 0; i < textWriterSingles.Count; i++)
        {
            if (textWriterSingles[i].GetText() == text)
            {
                textWriterSingles.RemoveAt(i);
                StopCoroutine(activeCoroutines[i]);
                activeCoroutines.RemoveAt(i);
                i--;
            }
        }
    }
    

    public class TextWriterSingle 
    {
        private TextMeshProUGUI textComponent;
        private string fullText;
        private int characterIndex;
        private float timePerCharacter;
        private float timer;
        private bool invisibleCharacters;
        public bool isWriting;

        public void AddWriter(TextMeshProUGUI textComponent, string textToWrite, float timePerCharacter, bool invisibleCharacters, bool isWriting = true)
        {
            this.textComponent = textComponent;
            this.fullText = textToWrite;
            this.timePerCharacter = timePerCharacter;
            this.invisibleCharacters = invisibleCharacters;
            this.isWriting = isWriting;
            characterIndex = 0;
        }

        public IEnumerator WriteTextCoroutine()
        {

            if (textComponent == null)
            {
                Debug.LogError("[WritingTextUI] WriteTextCoroutine: textComponent is null!");
                yield break;
            }

            Debug.Log($"[WritingTextUI] WriteTextCoroutine started for '{fullText}'");

            while (true)
            {
                timer -= Time.deltaTime;

                if (timer <= 0f && characterIndex < fullText.Length)
                {
                    timer += timePerCharacter;
                    isWriting = true;
                    characterIndex++;
                    string textToShow = fullText.Substring(0, characterIndex);
                    PlayRandomTypingSound();

                    if (invisibleCharacters)
                        textToShow += $"<color=#00000000>{fullText.Substring(characterIndex)}</color>";

                    textComponent.text = textToShow;
                    Debug.Log($"[WritingTextUI] Typing: '{textToShow}'");

                    if (characterIndex >= fullText.Length)
                    {
                        isWriting = false;
                        Debug.Log("[WritingTextUI] Typing complete.");
                        yield break;
                    }
                }
                yield return null;
            }
        }

        private void PlayRandomTypingSound()
        {
            if (keyboardTypingSounds == null || keyboardTypingSounds.Count == 0)
                return;

            int randomIndex = UnityEngine.Random.Range(0, keyboardTypingSounds.Count);
            AudioClip clip = keyboardTypingSounds[randomIndex];
            if (clip != null && SoundManager.Instance != null && SoundManager.Instance.sfxSource != null)
            {
                SoundManager.Instance.sfxSource.PlayOneShot(clip);
            }
        }

        public TextMeshProUGUI GetText()
        {
            return textComponent;
        }

        public bool IsActive(){
            return isWriting;
        }

        public void WriteAllAndDestroy()
        {
            if (textComponent != null)
                textComponent.text = fullText;
            characterIndex = fullText.Length;
            WritingTextUI.RemoveWriter_Static(textComponent);
        }
    }
}
