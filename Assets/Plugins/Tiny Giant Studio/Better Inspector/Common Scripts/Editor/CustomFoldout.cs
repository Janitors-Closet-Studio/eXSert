using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.BetterInspector
{
    public static class CustomFoldout
    {
        public static void SetupFoldout(GroupBox container, string toggleName = "FoldoutToggle",
            string usabilityToggle = "UsabilityToggle")
        {
            Toggle toggle = container.Q<Toggle>(toggleName);
            GroupBox content = container.Q<GroupBox>("Content");
            Toggle foldoutContentUsableToggle = container.Q<Toggle>(usabilityToggle);

            SwitchContent(content, toggle.value, toggle, foldoutContentUsableToggle, true);

            BindToggle(toggle, content);

            foldoutContentUsableToggle?.RegisterValueChangedCallback(ev =>
            {
                if (ev.newValue)
                    content.style.opacity = 1;
                else
                    content.style.opacity = 0.5f;
            });
        }


        static void BindToggle(Toggle toggle, GroupBox content)
        {
            toggle.RegisterValueChangedCallback(ev => { SwitchContent(content, ev.newValue, toggle); });
        }

        /// <summary>
        /// Note to self. This is called twice at the start. Once false, then true
        /// </summary>
        /// <param name="content"></param>
        /// <param name="toggleStatus"></param>
        /// <param name="toggle"></param>
        /// <param name="foldoutContentUsableToggle"></param>
        /// <param name="instant"></param>
        public static void SwitchContent(GroupBox content, bool toggleStatus, Toggle toggle = null,
            Toggle foldoutContentUsableToggle = null, bool instant = false)
        {
            if (toggleStatus)
            {
                if (foldoutContentUsableToggle != null)
                    RevealContent(content, foldoutContentUsableToggle.value);
                else
                    RevealContent(content);
            }
            else
            {
                if (!instant)
                    content.schedule.Execute(() => TurnOffContent(content, toggle)).ExecuteLater(50);
                else
                    TurnOffContent(content, toggle);

                content.style.translate = new(new Translate(0, -40));
                content.style.opacity = 0;
                content.style.scale = new(new Scale(new Vector3(1, 0, 1)));
            }
        }

        static void TurnOffContent(GroupBox content, Toggle toggle)
        {
            if (toggle is { value: true }) return;
            content.style.display = DisplayStyle.None;
        }


        static void RevealContent(GroupBox content, bool usableContent = true)
        {
            content.style.display = DisplayStyle.Flex;
            content.style.translate = new(new Translate(0, 0));
            if (usableContent)
                content.style.opacity = 1;
            else
                content.style.opacity = 0.5f;
            content.style.scale = new(new Scale(new Vector3(1, 1, 1)));
        }
    }
}