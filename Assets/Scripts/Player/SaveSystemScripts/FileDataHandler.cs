/*
Written by Brandon Wahl

Handles data that is save and loaded. When respective functions are called, this script will save or read data from a json file

*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;

public class FileDataHandler
{
    private const string EditorSaveFolderName = "EditorSaves";
    private const string BuildSaveFolderName = "BuildSaves";

    //These two variables make up the file path
    private string dataDirPath = "";

    private string dataFileName = "";

    private string scopedDataDirPath = "";

    //Defines the two above variables
    public FileDataHandler(string dataDirPath, string dataFileName)
    {
        this.dataDirPath = dataDirPath;
        this.dataFileName = string.IsNullOrWhiteSpace(dataFileName) ? "save.game" : dataFileName.Trim();
        this.scopedDataDirPath = ResolveScopedDataDirPath();
    }

    private string ResolveScopedDataDirPath()
    {
        string scopeFolder = Application.isEditor ? EditorSaveFolderName : BuildSaveFolderName;
        return Path.Combine(dataDirPath, scopeFolder);
    }

    private string GetCanonicalProfileFilePath(string profileId)
    {
        return Path.Combine(scopedDataDirPath, profileId, dataFileName);
    }

    private string GetScopedLegacyProfileFilePath(string profileId)
    {
        return Path.Combine(scopedDataDirPath, profileId);
    }

    private string GetRootNestedProfileFilePath(string profileId)
    {
        return Path.Combine(dataDirPath, profileId, dataFileName);
    }

    private string GetRootLegacyProfileFilePath(string profileId)
    {
        return Path.Combine(dataDirPath, profileId);
    }

    private IEnumerable<string> GetCandidateProfileFilePaths(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            yield break;

        HashSet<string> yieldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] candidatePaths =
        {
            GetCanonicalProfileFilePath(profileId),
            GetScopedLegacyProfileFilePath(profileId),
            GetRootNestedProfileFilePath(profileId),
            GetRootLegacyProfileFilePath(profileId)
        };

        foreach (string candidatePath in candidatePaths)
        {
            if (yieldedPaths.Add(candidatePath))
                yield return candidatePath;
        }
    }

    private string ResolveWritableProfileFilePath(string profileId)
    {
        string scopedLegacyPath = GetScopedLegacyProfileFilePath(profileId);
        if (File.Exists(scopedLegacyPath))
            return scopedLegacyPath;

        string canonicalPath = GetCanonicalProfileFilePath(profileId);
        if (File.Exists(canonicalPath))
            return canonicalPath;

        return canonicalPath;
    }

    private static bool IsLegacyProfileFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && !fileName.StartsWith(".", StringComparison.Ordinal)
            && string.IsNullOrEmpty(Path.GetExtension(fileName));
    }

    private IEnumerable<string> EnumerateScopedNestedProfileIds()
    {
        if (!Directory.Exists(scopedDataDirPath))
            yield break;

        foreach (string directoryPath in Directory.EnumerateDirectories(scopedDataDirPath))
        {
            string profileId = Path.GetFileName(directoryPath);
            if (File.Exists(Path.Combine(directoryPath, dataFileName)))
                yield return profileId;
        }
    }

    private IEnumerable<string> EnumerateScopedLegacyProfileIds()
    {
        if (!Directory.Exists(scopedDataDirPath))
            yield break;

        foreach (string filePath in Directory.EnumerateFiles(scopedDataDirPath))
        {
            string fileName = Path.GetFileName(filePath);
            if (IsLegacyProfileFileName(fileName))
                yield return fileName;
        }
    }

    private IEnumerable<string> EnumerateRootNestedProfileIds()
    {
        if (!Directory.Exists(dataDirPath))
            yield break;

        foreach (string directoryPath in Directory.EnumerateDirectories(dataDirPath))
        {
            string profileId = Path.GetFileName(directoryPath);
            if (string.Equals(profileId, EditorSaveFolderName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(profileId, BuildSaveFolderName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(profileId, "Unity", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.Exists(Path.Combine(directoryPath, dataFileName)))
                yield return profileId;
        }
    }

    private IEnumerable<string> EnumerateRootLegacyProfileIds()
    {
        if (!Directory.Exists(dataDirPath))
            yield break;

        foreach (string filePath in Directory.EnumerateFiles(dataDirPath))
        {
            string fileName = Path.GetFileName(filePath);
            if (IsLegacyProfileFileName(fileName))
                yield return fileName;
        }
    }

    private IEnumerable<string> EnumerateKnownProfileIds()
    {
        HashSet<string> seenProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string>[] profileIdSources =
        {
            EnumerateScopedNestedProfileIds(),
            EnumerateScopedLegacyProfileIds(),
            EnumerateRootNestedProfileIds(),
            EnumerateRootLegacyProfileIds()
        };

        foreach (IEnumerable<string> profileIdSource in profileIdSources)
        {
            foreach (string profileId in profileIdSource)
            {
                if (seenProfileIds.Add(profileId))
                    yield return profileId;
            }
        }
    }

    private static GameData TryReadGameData(string fullPath)
    {
        try
        {
            string dataToLoad = "";

            using (FileStream stream = new FileStream(fullPath, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    dataToLoad = reader.ReadToEnd();
                }
            }

            return JsonUtility.FromJson<GameData>(dataToLoad);
        }
        catch (Exception e)
        {
            Debug.LogError("Error occured when trying to load date to file: " + fullPath + "\n" + e);
            return null;
        }
    }

    public void DeleteProfile(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) return;

        foreach (string fullPath in GetCandidateProfileFilePaths(profileId))
        {
            try
            {
                if (!File.Exists(fullPath))
                    continue;

                File.Delete(fullPath);

                string canonicalPath = GetCanonicalProfileFilePath(profileId);
                if (string.Equals(fullPath, canonicalPath, StringComparison.OrdinalIgnoreCase))
                {
                    string profileDirectory = Path.GetDirectoryName(canonicalPath);
                    if (Directory.Exists(profileDirectory)
                        && !Directory.EnumerateFileSystemEntries(profileDirectory).GetEnumerator().MoveNext())
                    {
                        Directory.Delete(profileDirectory);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error occured when trying to delete save file: " + fullPath + "\n" + e);
            }
        }
    }

    //This function properly loads the saved data
    public GameData Load(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        foreach (string fullPath in GetCandidateProfileFilePaths(profileId))
        {
            if (!File.Exists(fullPath))
                continue;

            GameData loadedData = TryReadGameData(fullPath);
            if (loadedData != null)
                return loadedData;
        }

        return null;
    }

    public void Save(GameData data, string profileId)
    {
        //If there is no profileId, the save will not be loaded
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        string fullPath = ResolveWritableProfileFilePath(profileId);

        try
        {
            //Creates a directory with the fullPath
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            //string variable that will convert the data that needs to be saved to Json
            string dataToStore = JsonUtility.ToJson(data, true);

            //Using FileStream, a variable is assigned to create a new file with the fullPath
            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
            {
                //Writes the data that is being saved and assigns it to dataToStore
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(dataToStore);
                }
            }
        }
        //If the file cant be saved it will return this error
        catch (Exception e)
        {
            Debug.LogError("Error occured when trying to save date to file: " + fullPath + "\n" + e);
        }
    }

    //Gathers each profile that already exists and is responsible with loaded the data into the main menu
    public Dictionary<string, GameData> LoadAllProfiles()
    {
        Dictionary<string, GameData> profileDictionary = new Dictionary<string, GameData>(StringComparer.OrdinalIgnoreCase);

        foreach (string profileId in EnumerateKnownProfileIds())
        {
            //Assigns local var profileData to data connected with a profileId
            GameData profileData = Load(profileId);

            //If the profile data exists, it will be added to the dictionary above
            if (profileData != null && !profileDictionary.ContainsKey(profileId))
            {
                profileDictionary.Add(profileId, profileData);
            }
        }

        return profileDictionary;
    }

    //Gathers which profile was used last for QOL
    public string GetMostRecentUpdatedProfile()
    {
        string mostRecentProfileId = null;

        Dictionary<string, GameData> profilesGameData = LoadAllProfiles();

        //Goes through each profile in the dictionary above to gather which Id was used last
        foreach (KeyValuePair<string, GameData> pair in profilesGameData)
        {
            string profileId = pair.Key;
            GameData gameData = pair.Value;

            if (gameData == null)
            {
                continue;
            }

            if (mostRecentProfileId == null)
            {
                mostRecentProfileId = profileId;
            }
            else
            {
                //Compares the previously most recently saved data to the newest data thats been saved. If the new time is greater, then that data will now be assigned to the variable mostRecentProfileId
                DateTime mostRecentDateTime = DateTime.FromBinary(profilesGameData[mostRecentProfileId].lastUpdated);
                DateTime newDateTime = DateTime.FromBinary(gameData.lastUpdated);

                if (newDateTime > mostRecentDateTime)
                {
                    mostRecentProfileId = profileId;
                }
            }
        }
        return mostRecentProfileId;
    }
}
