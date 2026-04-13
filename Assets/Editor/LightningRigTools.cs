using UnityEditor;
using UnityEngine;

public static class LightningRigTools
{
    [MenuItem("Tools/VFX/Duplicate Selected Lightning Rig", priority = 2100)]
    private static void DuplicateSelectedLightningRig()
    {
        GameObject source = Selection.activeGameObject;
        if (source == null)
        {
            Debug.LogWarning("Select a lightning rig GameObject first.");
            return;
        }

        Transform parent = source.transform.parent;
        GameObject clone = Object.Instantiate(source, parent);
        clone.name = GenerateUniqueName(parent, source.name + " Copy");
        clone.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
        clone.transform.localScale = source.transform.localScale;

        Undo.RegisterCreatedObjectUndo(clone, "Duplicate Lightning Rig");
        Selection.activeGameObject = clone;
    }

    [MenuItem("Tools/VFX/Duplicate Selected Lightning Rig", validate = true)]
    private static bool CanDuplicateSelectedLightningRig()
    {
        return Selection.activeGameObject != null;
    }

    private static string GenerateUniqueName(Transform parent, string baseName)
    {
        string candidate = baseName;
        int index = 2;

        while (HasSiblingWithName(parent, candidate))
        {
            candidate = baseName + " " + index;
            index++;
        }

        return candidate;
    }

    private static bool HasSiblingWithName(Transform parent, string name)
    {
        if (parent == null)
        {
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject rootObject in rootObjects)
            {
                if (rootObject.name == name)
                    return true;
            }

            return false;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name == name)
                return true;
        }

        return false;
    }
}