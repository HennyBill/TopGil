using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;

using System;
using System.Collections.Generic;
using System.Linq;

namespace TopGil;

internal class CharacterRetainerManager : IDisposable
{
    internal const string UNKNOWN_CHARACTER = "Unknown";

    private GameCharacter? _CurrentIngameCharacter = null;

    private readonly Plugin _plugin;

    public CharacterRetainerManager(Plugin plugin)
    {
        _plugin = plugin;
    }

    private void PrintToChat(string message)
    {
        _plugin.PrintToChat(message);
    }

    internal void ResetCurrentCharacter()
    {
        _CurrentIngameCharacter = null;
    }

    /// <summary>
    /// Sets the current session character <see cref="_CurrentIngameCharacter"/>.
    /// Also, doing some mainentance, like adding new characters, handling renamed characters.
    /// </summary>
	internal void SetCurrentCharacter()
    {
        (string Name, uint HomeWorldId, uint Gil)? dalamudCharacter = DalamudGameHelper.GetCurrentCharacterDetails();

        // Check if we actually have a player character
        if (dalamudCharacter == null)
        {
            string msg = "Cannot retrieve character details!";
            Plugin.Log.Fatal(msg);
            DebuggerLog.Write(msg);
            throw new Exception(msg);
        }

        // First, try get the character from our own storage of existing characters
        GameCharacter? lookupCharacter =
            _plugin.Configuration.Characters.FirstOrDefault(
                c => c.Name == dalamudCharacter.Value.Name &&
                c.HomeWorldId == dalamudCharacter.Value.HomeWorldId
             );

        if (_CurrentIngameCharacter != null && _CurrentIngameCharacter.Equals(lookupCharacter))
        {
            DebuggerLog.Write("Current character is already set - no need to update.");
            return;
        }

        if (lookupCharacter == null)
        {
            #region Add new character -or- rename existing one

            DebuggerLog.Write("Character not found in plugin storage - adding new or renaming existing, lets check...");

            // Before adding this as a new character, check if it's a renamed one
            if (AreYouReallyANewCharacter(out Guid? possibleOldCharacterId, out string? possibleOldCharacterName)) // To test this, close game, rename a character in the plugin-config-file, start game, visit a retainer bell
            {
                // Yes, add the new character with an empty retainer list - retainers will be ...
                // ... added later when player interacts with a retainer bell
                GameCharacter newCharacter = GameCharacter.CreateNew(dalamudCharacter.Value.Name, dalamudCharacter.Value.HomeWorldId, dalamudCharacter.Value.Gil);
                _plugin.Configuration.Characters.Add(newCharacter);

                _CurrentIngameCharacter = newCharacter;

                DebuggerLog.Write($"Added new character: {dalamudCharacter.Value.Name}. WorldId: {dalamudCharacter.Value.HomeWorldId}");
            }
            else
            {
                // Renamed character, find character by the possible unique id, returned above
                // TODO: better notification to user that a character has been renamed (or failed doing so)
                _CurrentIngameCharacter = _plugin.Configuration.Characters.FirstOrDefault(c => c.UniqueId == possibleOldCharacterId);
                if (_CurrentIngameCharacter == null)
                {
                    DebuggerLog.Write("Cannot find character by unique id. This is unexpected!");
                    throw new Exception("Cannot find character by unique id. This is unexpected!");
                }
                string oldName = possibleOldCharacterName;
                _CurrentIngameCharacter.Name = dalamudCharacter.Value.Name; // Update name
                _CurrentIngameCharacter.HomeWorldId = dalamudCharacter.Value.HomeWorldId; // Update home world
                _CurrentIngameCharacter.Gil = dalamudCharacter.Value.Gil;
                _CurrentIngameCharacter.LastVisited = NumberFormatter.FormatDateTimeToString(DateTime.Now);

                DebuggerLog.Write($"Character renamed: {oldName} -> {dalamudCharacter.Value.Name}");
            }
            #endregion
        }
        else
        {
            // Known character exists - set as current
            _CurrentIngameCharacter = lookupCharacter;
            //nope, update will do this _CurrentIngameCharacter.Gil = dalamudCharacter.Value.Gil;
            //nope, update will do this _CurrentIngameCharacter.LastVisited = Helper.FormatDateTimeToString(DateTime.Now);
        }

        //DebugDumpCharacters(extraInfo: "After setting current character.");

        _plugin.Configuration.Save();

        DebuggerLog.Write($"Current character set to: {_CurrentIngameCharacter.Name}");
    }

    internal unsafe void UpdateCurrentCharacter()
    {
        if (_CurrentIngameCharacter == null)
        {
            DebuggerLog.Write("Current character is not set... lets try again.");

            SetCurrentCharacter();
        }
        if (_CurrentIngameCharacter == null)
        {
            DebuggerLog.Write("Current character is still not set... cannot update. Abort!");
            return;
        }

        if (DalamudGameHelper.IsRetainerManagerReady() == false)
        {
            int numRetainers = DalamudGameHelper.GetActualNumberOfRetainers();
            DebuggerLog.Write($"UpdateCurrentCharacter() - RetainerManager is not ready. Number of retainers: {numRetainers}");
            PrintToChat($"{Plugin.PluginName} - RetainerManager is not ready. Number of retainers: {numRetainers}");
            return;
        }

        // --------->
        // Get updated data/gil for the current character - retainers will be updated below
        // <---------
        (string Name, uint HomeWorldId, uint Gil)? dalamudCharacter = DalamudGameHelper.GetCurrentCharacterDetails();
        _CurrentIngameCharacter.Gil = dalamudCharacter.Value.Gil;
        _CurrentIngameCharacter.LastVisited = NumberFormatter.FormatDateTimeToString(DateTime.Now);

        DebuggerLog.Write($"Updating current character: {_CurrentIngameCharacter.Name} has {NumberFormatter.FormatNumber(_CurrentIngameCharacter.Gil)} Gil.");

        // --------->
        // Update gil for each of the current characters' retainers (and some maintenance)
        // <---------
        var retainerManager = RetainerManager.Instance();
        if (retainerManager != null)
        {
            string nowDateTimeStr = NumberFormatter.FormatDateTimeToString(DateTime.Now);

            for (int i = 0; i < retainerManager->Retainers.Length; i++)
            {
                var dalamudRetainer = retainerManager->Retainers[i];

                DebuggerLog.Write($">> UpdateCurrentCharacter() - Dalamud retainer {i + 1} - Name: {dalamudRetainer.NameString}, Id: {dalamudRetainer.RetainerId}");

                // ToDo: dalamudRetainer.Available -- might be better to use rather than the check in the next line - test!
                if (dalamudRetainer.RetainerId == 0 || dalamudRetainer.NameString == "RETAINER")
                {
                    DebuggerLog.Write($"UpdateCurrentCharacter() - Skip filler retainer {i + 1}.");
                    continue;
                }

                // Lookup retainer by id... NOT including name - retainers can be renamed, but they have a unique game id, so they're easy to identify
                GameRetainer? lookupKnownRetainer = _CurrentIngameCharacter.Retainers.FirstOrDefault(r => r.RetainerId == dalamudRetainer.RetainerId);

                if (lookupKnownRetainer != null)
                {
                    DebuggerLog.Write($" * Retainer {i + 1}: {dalamudRetainer.NameString} has {NumberFormatter.FormatNumber(dalamudRetainer.Gil)} Gil.");

                    // Update existing retainer (includes renamed retainers that will have their name updated)
                    lookupKnownRetainer.Name = dalamudRetainer.NameString; // Update name in case it was renamed
                    lookupKnownRetainer.Gil = dalamudRetainer.Gil;
                    lookupKnownRetainer.LastVisited = nowDateTimeStr;
                }
                else
                {
                    // New retainer found - add
                    DebuggerLog.Write($" * New retainer found: {dalamudRetainer.NameString} with Id: {dalamudRetainer.RetainerId} has {NumberFormatter.FormatNumber(dalamudRetainer.Gil)} Gil.");

                    _CurrentIngameCharacter.Retainers.Add(new GameRetainer
                    {
                        Name = dalamudRetainer.NameString,
                        RetainerId = dalamudRetainer.RetainerId,
                        Gil = dalamudRetainer.Gil,
                        OwnerCharacterId = _CurrentIngameCharacter.UniqueId,
                        LastVisited = nowDateTimeStr
                    });
                }
            }

            // Remove old retainers that hasn't been updated or added - the once that doesn't
            // have LastVisited set to string value of "nowDateTimeStr" in the loop above.
            foreach (var retainer in _CurrentIngameCharacter.Retainers.Where(r => r.LastVisited != nowDateTimeStr).ToList())
            {
                DebuggerLog.Write($"Deleting old retainer: {retainer.Name} - {retainer.RetainerId} - LastVisited: {retainer.LastVisited}");
            }
            _CurrentIngameCharacter.Retainers.RemoveAll(r => r.LastVisited != nowDateTimeStr); // Remove all retainers that hasn't been updated
        }
        else
        {
            DebuggerLog.Write("No retainers found - RetainerManager is null.");
        }

        // Save changes
        _plugin.Configuration.Save();
    }

    internal long GetTotalGilCharacter(bool runUpdateFirst = true)
    {
        if (runUpdateFirst)
        {
            UpdateCurrentCharacter();
        }
        if (_CurrentIngameCharacter == null)
        {
            SetCurrentCharacter();
        }
        if (_CurrentIngameCharacter == null)
        {
            DebuggerLog.Write("Error, cannot get total gil for character - current character is not set.");
            return -1;
        }
        return _CurrentIngameCharacter.TotalGil;
    }

    internal long GetTotalGilAll(bool runUpdateFirst = true)
    {
        if (runUpdateFirst)
        {
            UpdateCurrentCharacter();
        }

        long totalGil = 0;

        foreach (var character in _plugin.Configuration.Characters)
        {
            //DebuggerLog.Write($"GetTotalGilCharacter - Character: {character.Name} - Total Gil: {Helper.FormatNumber(character.TotalGil)}");
            totalGil += character.TotalGil;
        }

        return totalGil;
    }

    internal int GetTotalCharacters()
    {
        return _plugin.Configuration.Characters.Count;
    }

    internal int GetTotalRetainers()
    {
        if (_CurrentIngameCharacter == null)
        {
            return 0;
        }

        int totalRetainers = 0;

        foreach (var character in _plugin.Configuration.Characters)
        {
            totalRetainers += character.Retainers.Count;
        }

        return totalRetainers;
    }

    internal string GetCurrentCharacterName()
    {
        if (_CurrentIngameCharacter == null)
        {
            return UNKNOWN_CHARACTER;
        }

        return _CurrentIngameCharacter.Name;
    }

    internal GameCharacter GetCurrentCharacter()
    {
        return _CurrentIngameCharacter;
    }

    internal unsafe List<GameRetainer> GetCurrentCharactersRetainers()
    {
        if (_CurrentIngameCharacter == null)
        {
            return new();
        }

        //var sortedList = new List<GameRetainer>();
        //var retainerManager = RetainerManager.Instance();
        //if (retainerManager != null)
        //{
        //    for (int i = 0; i < retainerManager->Retainers.Length; i++)
        //    {
        //        byte orderValue = retainerManager->DisplayOrder[i];
        //        PrintToChat($"Retainer {i + 1} - Order: {orderValue}");
        //        var dalamudRetainer = retainerManager->Retainers[orderValue];

        //        sortedList.Add(_CurrentIngameCharacter.Retainers.FirstOrDefault(r => r.RetainerId == dalamudRetainer.RetainerId));
        //    }
        //    return sortedList;
        //}

        return _CurrentIngameCharacter.Retainers;
    }

    /// <summary>
    /// Checks if there's a match between the current retainers and at least one we already know about.
    /// If a match is found, we return the unique id of the character that owns/owned this retainer.
    /// </summary>
    /// <param name="possibleOldCharacterId"></param>
    /// <returns>True if this actually is a new character - False if this is an existing renamed character</returns>
    private unsafe bool AreYouReallyANewCharacter(out Guid? possibleOldCharacterId, out string? possibleOldCharacterName)
    {
        DebuggerLog.Write("Checking if this is a new character or a renamed one...");

        List<GameRetainer> pluginRetainers = new();

        // Loop through all ingame retainers reported by the game/Dalamud
        var retainerManager = RetainerManager.Instance();
        for (int i = 0; i < retainerManager->Retainers.Length; i++)
        {
            RetainerManager.Retainer dalamudRetainer = retainerManager->Retainers[i];

            DebuggerLog.Write($" AreYouReallyANewCharacter()  Checking retainer {i + 1}: {dalamudRetainer.NameString}, Id: {dalamudRetainer.RetainerId} against:");

            if (dalamudRetainer.RetainerId == 0 || dalamudRetainer.NameString == "RETAINER")
            {
                DebuggerLog.Write($"AreYouReallyANewCharacter(...) - Skip filler retainer {i + 1}.");
                continue;
            }

            // Try find a retainer match in the plugin storage of existing retainers
            foreach (var storageCharacter in _plugin.Configuration.Characters)
            {
                foreach (var storageRetainer in storageCharacter.Retainers)
                {
                    DebuggerLog.Write($" Is this a match - {storageRetainer.Name}, Id: {storageRetainer.RetainerId}");

                    if (storageRetainer.Name == dalamudRetainer.NameString && storageRetainer.RetainerId == dalamudRetainer.RetainerId)
                    {
                        // We might have a match - this retainer probably belongs to the current logged in character
                        // Get the character details for this match
                        possibleOldCharacterId = storageRetainer.OwnerCharacterId;
                        possibleOldCharacterName = storageCharacter.Name;

                        DebuggerLog.Write($"Character match found for retainer. OwnerID: {possibleOldCharacterId.ToString()}");

                        return false; // Nope, not a new character - got'yah!
                    }
                    else
                    {
                        DebuggerLog.Write($"No match for retainer {dalamudRetainer.NameString} - {dalamudRetainer.RetainerId} <-> {storageRetainer.Name} - {storageRetainer.RetainerId}");
                    }
                }
            }
        }

        DebuggerLog.Write("No matches found - this is a new character!");
        possibleOldCharacterId = null;
        possibleOldCharacterName = null;
        return true; // Yes, I'm a new character!
    }



    private void DebugDumpCharacters(string extraInfo = "")
    {
        if (_plugin.Configuration.DebugEnabled == false)
        {
            return;
        }

        DebuggerLog.Write("Debug dumping all characters... " + extraInfo);

        foreach (var character in _plugin.Configuration.Characters)
        {
            DebuggerLog.Write($"Character: {character.Name} - {character.HomeWorldId} - {NumberFormatter.FormatNumber(character.Gil)} - {character.LastVisited}");

            foreach (var retainer in character.Retainers)
            {
                DebuggerLog.Write($" * Retainer: {retainer.Name} - {retainer.RetainerId} - {NumberFormatter.FormatNumber(retainer.Gil)} - {retainer.LastVisited}");
            }
        }

        DebuggerLog.Write("End of characters dump.");
    }


    #region IDisposable
    // Flag to detect redundant calls
    private bool _disposed = false;

    // Public implementation of Dispose pattern callable by consumers.
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // Protected implementation of Dispose pattern.
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Free any other managed objects here.
            //
        }

        // Free any unmanaged resources here.
        //

        _disposed = true;
    }

    // Destructor
    ~CharacterRetainerManager()
    {
        Dispose(false);
    }
    #endregion
}
