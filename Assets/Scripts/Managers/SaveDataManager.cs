/*
    Written By Brandon Wahl

    This script grabs any variable that needs to be saved/loaded and does each respective task
*/
using Singletons;
using Utilities.Combat;

public class SaveDataManager : Singleton<SaveDataManager>, IDataPersistenceManager
{
    public void LoadData(GameData data)
    {
        // PlayerHealthBarManager is the authoritative HP owner.
        // Leave CombatManager out of save/load so it cannot overwrite profile health.
    }

    public void SaveData(GameData data)
    {
        // PlayerHealthBarManager saves health directly.
    }
}
