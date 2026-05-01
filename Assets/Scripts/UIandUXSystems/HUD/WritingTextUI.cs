using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UI.Loading;

public class WritingTextUI : MonoBehaviour
{
    private static WritingTextUI instance;
    private static List<Coroutine> activeCoroutines = new List<Coroutine>();
    private List<TextWriterSingle> textWriterSingles;

    [Header("Debug")]
    [Tooltip("Enable verbose WritingTextUI debug logs.")]
    [SerializeField] private bool debugLogging = false;
    internal static bool DebugLogging = false;
  
    public static List<AudioClip> keyboardTypingSounds = new List<AudioClip>();
    public List<AudioClip> keyboardTypingSoundsList = new List<AudioClip>();

    private void Awake()
    {
        instance = this;
        DebugLogging = debugLogging;
        textWriterSingles = new List<TextWriterSingle>();

        foreach (AudioClip clip in keyboardTypingSoundsList)
        {
            if (clip != null)
                keyboardTypingSounds.Add(clip);
        }
    }


    public static TextWriterSingle AddWriter_Static(TextMeshProUGUI textComponent, string textToWrite, float timePerCharacter, bool invisibleCharacters, bool removeWriterBeforeAdd = true)
    {
        if (DebugLogging) Debug.Log($"[WritingTextUI] AddWriter_Static called. textComponent: {textComponent}, textToWrite: '{textToWrite}', timePerCharacter: {timePerCharacter}, invisibleCharacters: {invisibleCharacters}, removeWriterBeforeAdd: {removeWriterBeforeAdd}");
        if (instance == null)
        {
            Debug.LogWarning("[WritingTextUI] AddWriter_Static called before WritingTextUI was initialized.");
            return null;
        }

        if (removeWriterBeforeAdd)
            instance.RemoveWriter(textComponent);

        return instance.AddWriter(textComponent, textToWrite, timePerCharacter, invisibleCharacters);
    }

    private TextWriterSingle AddWriter(TextMeshProUGUI textComponent, string textToWrite, float timePerCharacter, bool invisibleCharacters)
    {
        if (textComponent != null)
            textComponent.richText = true;

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
        if (instance == null)
        {
            Debug.LogWarning("[WritingTextUI] RemoveWriter_Static called before WritingTextUI was initialized.");
            return;
        }

        instance.RemoveWriter(text);
    }

    private void RemoveWriter(TextMeshProUGUI text)
    {
        if (textWriterSingles == null || text == null)
            return;

        for (int i = 0; i < textWriterSingles.Count; i++)
        {
            if (textWriterSingles[i].GetText() == text)
            {
                // Stop and remove the coroutine if it exists
                if (i < activeCoroutines.Count && activeCoroutines[i] != null)
                {
                    StopCoroutine(activeCoroutines[i]);
                    activeCoroutines.RemoveAt(i);
                }
                else if (i < activeCoroutines.Count)
                {
                    activeCoroutines.RemoveAt(i);
                }
                textWriterSingles.RemoveAt(i);
                i--;
            }
        }
    }
    

    public class TextWriterSingle 
    {
        private TextMeshProUGUI textComponent;
        private string fullText;
        private int rawTextIndex;
        private float timePerCharacter;
        private float timer;
        private bool invisibleCharacters;
        public bool isWriting;

        public void AddWriter(TextMeshProUGUI textComponent, string textToWrite, float timePerCharacter, bool invisibleCharacters, bool isWriting = true)
        {
            this.textComponent = textComponent;
            if (this.textComponent != null)
                this.textComponent.richText = true;

            this.fullText = textToWrite;
            this.timePerCharacter = timePerCharacter;
            this.invisibleCharacters = invisibleCharacters;
            this.isWriting = isWriting;
            rawTextIndex = 0;
        }

        public IEnumerator WriteTextCoroutine()
        {

            if (textComponent == null)
            {
                Debug.LogError("[WritingTextUI] WriteTextCoroutine: textComponent is null!");
                yield break;
            }

            while (Time.timeScale == 0f)
                yield return null;

            if (WritingTextUI.DebugLogging) Debug.Log($"[WritingTextUI] WriteTextCoroutine started for '{fullText}'");

            while (true)
            {
                timer -= Time.deltaTime;

                if (timer <= 0f && rawTextIndex < fullText.Length)
                {
                    timer += timePerCharacter;
                    isWriting = true;
                    int nextVisibleIndex = GetNextVisibleIndex(fullText, rawTextIndex);
                    if (nextVisibleIndex <= rawTextIndex)
                        nextVisibleIndex = rawTextIndex + 1;

                    rawTextIndex = nextVisibleIndex;
                    string textToShow = BuildVisibleText(fullText, rawTextIndex, invisibleCharacters);
                    PlayRandomTypingSound();

                    textComponent.text = textToShow;
                    if (WritingTextUI.DebugLogging) Debug.Log($"[WritingTextUI] Typing: '{textToShow}'");

                    if (rawTextIndex >= fullText.Length)
                    {
                        isWriting = false;
                        if (WritingTextUI.DebugLogging) Debug.Log("[WritingTextUI] Typing complete.");
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

            if (LoadingScreenController.IsLoading)
                return;

            if (CutsceneManager.IsCutscenePlaying)
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
            {
                textComponent.richText = true;
                textComponent.text = fullText;
            }

            rawTextIndex = fullText.Length;
            WritingTextUI.RemoveWriter_Static(textComponent);
        }

        private static int GetNextVisibleIndex(string text, int startIndex)
        {
            int index = startIndex;

            while (index < text.Length)
            {
                if (text[index] == '<')
                {
                    int tagEnd = text.IndexOf('>', index);
                    if (tagEnd < 0)
                        return Mathf.Min(index + 1, text.Length);

                    string tag = text.Substring(index, tagEnd - index + 1);
                    index = tagEnd + 1;

                    if (IsVisibleTag(tag))
                        return index;

                    continue;
                }

                return index + 1;
            }

            return index;
        }

        private static string BuildVisibleText(string fullText, int visibleRawIndex, bool invisibleCharacters)
        {
            if (!invisibleCharacters || visibleRawIndex >= fullText.Length)
                return fullText.Substring(0, visibleRawIndex);

            StringBuilder builder = new();
            builder.Append(fullText, 0, visibleRawIndex);
            builder.Append("<color=#00000000>");
            builder.Append(fullText.Substring(visibleRawIndex));
            builder.Append("</color>");
            return builder.ToString();
        }

        private static bool IsVisibleTag(string tag)
        {
            return tag.StartsWith("<sprite", StringComparison.OrdinalIgnoreCase);
        }
    }
}
