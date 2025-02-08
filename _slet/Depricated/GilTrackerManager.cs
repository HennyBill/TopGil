using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopGil;

internal enum UpdateSourceType
{
    OnLogin,            // Depriecated
    OnLogout,           // Depriecated
    UserRequest,        // User requested an update
    AutoSummoningBell,  // Auto update when summoning bell is used
    AutoTimer,          // Depriecated
    ShowTopGil          // User requested to show the top gil - updatge once before showing totals.
}

internal class GilTrackerManager : IDisposable
{
    private GilStorageManager StorageManager { get; set; } = null!;

    private Configuration PluginConfig { get; set; } = null!;
    private IClientState ClientState { get; set; } = null!;
    private IChatGui ChatGui { get; set; } = null!;
    private IPluginLog Log { get; set; } = null!;
    private IFramework Framework { get; set; } = null!;


    internal GilTrackerManager(Configuration configuration, IClientState clientState, IChatGui chatGui, IPluginLog log, IFramework framework)
    {
        // TODO: cleanup if the new global access technique is working
        PluginConfig = configuration;
        ClientState = clientState;
        ChatGui = chatGui;
        Log = log;
        Framework = framework;

        StorageManager = new GilStorageManager();
    }

    internal void OnLogin()
    {
        // Invoked when the player logs in
    }

    internal void OnLogout()
    {
        // Invoked when the player logs out
    }

    internal void DoUpdate(UpdateSourceType updateSource)
    {
        DebuggerLog.Write($"--> Update(updateSource = {updateSource})");

        long elapsedMilliseconds = TimeHelper.MeasureExecutionTime(async () =>
        {
            GilCharacter? existingCharacter = null;

            //_ = Framework.RunOnTick(() =>
            //{
                //DoUpdateStep1();
            //});

            //_ = Framework.RunOnTick(() =>
            //{
                //existingCharacter = DoUpdateStep2();
            //});

            //_ = Framework.RunOnTick(() =>
            //{
                //DoUpdateStep3(existingCharacter);
            //});


            Task.Run(() =>
            {
                DoUpdateStep1();
                existingCharacter = DoUpdateStep2();
                DoUpdateStep3(existingCharacter);
            });
        });
        DebuggerLog.Write($"[Update] Update overall took {elapsedMilliseconds} ms");

        DebuggerLog.Write("<-- Update()");
    }

    private void DoUpdateStep1()
    {
        DebuggerLog.Write("--> DoUpdateStep1()");

        /// *** Check for daily aggregation ***
        long elapsedMilliseconds = TimeHelper.MeasureExecutionTime(() =>
        {
            if (IsNextDayLogic()) // Check via last update timestamp
            {
                // *** New day - reset the daily gil etc. ***
                DebuggerLog.Write("*** Update, a new day is here ***");
                this.StorageManager.DoDailyAggregation();
            }
        });
        DebuggerLog.Write($"[DoUpdateStep1] Daily Aggregation took {elapsedMilliseconds} ms");
    }

    private GilCharacter? DoUpdateStep2()
    {
        DebuggerLog.Write("--> DoUpdateStep2()");

        GilCharacter? existingCharacter = null;

        long elapsedMilliseconds = TimeHelper.MeasureExecutionTime(() =>
        {
            existingCharacter = GetCharacter();
        });
        DebuggerLog.Write($"[DoUpdateStep2] Get character took {elapsedMilliseconds} ms");

        return existingCharacter;
    }

    private unsafe void DoUpdateStep3(GilCharacter? existingCharacter)
    {
        DebuggerLog.Write("--> DoUpdateStep3()");

        long elapsedMilliseconds = TimeHelper.MeasureExecutionTime(() =>
        {
            this.StorageManager.SaveGilRecord(existingCharacter.UniqueId, 0 /* 0 = Character */, existingCharacter.Gil);

//-            DebuggerLog.Write($"Character: {existingCharacter.Name} (WorldId: {existingCharacter.HomeWorldId}). Gil: {MiscHelpers.FormatNumber(existingCharacter.Gil)}");

            // ----------------

            var retainerManager = RetainerManager.Instance();
            if (retainerManager != null)
            {
                // Loop through all the characters' retainers
                for (int i = 0; i < retainerManager->Retainers.Length; i++)
                {
                    // Get next retainer
                    RetainerManager.Retainer retainer = retainerManager->Retainers[i];
                    GilRetainer thisRetainer = ConvertToGilRetainer(retainer, existingCharacter.UniqueId);

//-                    DebuggerLog.Write($" * Retainer {i + 1}: {thisRetainer.Name}. Gil = {MiscHelpers.FormatNumber(thisRetainer.Gil)}");

                    if (thisRetainer.Name == "RETAINER" || thisRetainer.RetainerId == 0)
                    {
                        DebuggerLog.Write("Invalid retainer - skipping.");
                        continue;
                    }

                    // *** Update the retainer ***
                    this.StorageManager.AddOrUpdateRetainer(thisRetainer);
                    this.StorageManager.SaveGilRecord(existingCharacter.UniqueId, thisRetainer.RetainerId, thisRetainer.Gil);
                }
            }
            else
            {
                // TODO: handle this better
                DebuggerLog.Write("No retainers found - Dalamud RetainerManager is null.");
            }

            // *** Punch the last update timestamp ***
            StorageManager.UpdateLastUpdateTimestamp();
        });
        DebuggerLog.Write($"[DoUpdateStep3] Maintain retainers took {elapsedMilliseconds} ms");
    }


    /// <summary>
    /// Update the amount of gil the current character and retainers have.
    /// This method is supposed to be called when the current character exits the retainer summoning bell and
    /// on user request via chat a command.
    /// Data is stored in the local database.
    /// Each day, the daily gil recorded is aggregated and stored in a table suitable for generating various reports.
    /// </summary>
    internal unsafe void UpdateGilCurrentCharacter(UpdateSourceType updateSource)
    {
        //DebuggerLog.Write($"--> Update(updateSource = {updateSource})");

        ///// *** Check for daily aggregation ***
        //long elapsedMilliseconds = TimeHelper.MeasureExecutionTime(() =>
        //{
        //    if (IsNextDayLogic()) // Check via last update timestamp
        //    {
        //        // *** New day - reset the daily gil etc. ***
        //        DebuggerLog.Write("*** Update, a new day is here ***");
        //        this.StorageManager.DoDailyAggregation();
        //    }
        //});
        //DebuggerLog.Write($"[UpdateGilCurrentCharacter] DoDailyAggregation() took {elapsedMilliseconds} ms");

        //GilCharacter? existingCharacter = null;

        //long elapsedMilliseconds = TimeHelper.MeasureExecutionTime(() =>
        //{
        //    existingCharacter = GetCharacter();
        //});
        //DebuggerLog.Write($"[UpdateGilCurrentCharacter] GetCharacter() took {elapsedMilliseconds} ms");

        //long elapsedMilliseconds = TimeHelper.MeasureExecutionTime(() =>
        //{
        //    this.StorageManager.SaveGilRecord(existingCharacter.UniqueId, 0 /* 0 = Character */, existingCharacter.Gil);

        //    DebuggerLog.Write($"Character: {existingCharacter.Name} (WorldId: {existingCharacter.HomeWorldId}). Gil: {MiscHelpers.FormatNumber(existingCharacter.Gil)}");

        //    // ----------------

        //    var retainerManager = RetainerManager.Instance();
        //    if (retainerManager != null)
        //    {
        //        // Loop through all the characters' retainers
        //        for (int i = 0; i < retainerManager->Retainers.Length; i++)
        //        {
        //            // Get next retainer
        //            RetainerManager.Retainer retainer = retainerManager->Retainers[i];
        //            GilRetainer thisRetainer = ConvertToGilRetainer(retainer, existingCharacter.UniqueId);

        //            DebuggerLog.Write($" * Retainer {i + 1}: {thisRetainer.Name}. Gil = {MiscHelpers.FormatNumber(thisRetainer.Gil)}");

        //            if (thisRetainer.Name == "RETAINER" || thisRetainer.RetainerId == 0)
        //            {
        //                DebuggerLog.Write("Invalid retainer - skipping.");
        //                continue;
        //            }

        //            // *** Update the retainer ***
        //            {
        //                //GilRetainer? existingRetainer = this.StorageManager.LoadRetainerByNameAndOwner(thisRetainer.Name, thisRetainer.OwnerCharacterId);

        //                this.StorageManager.AddOrUpdateRetainer(thisRetainer);
        //                this.StorageManager.SaveGilRecord(existingCharacter.UniqueId, thisRetainer.RetainerId, thisRetainer.Gil);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // TODO: handle this better
        //        DebuggerLog.Write("No retainers found - Dalamud RetainerManager is null.");
        //    }

        //    // *** Punch the last update timestamp ***
        //    StorageManager.UpdateLastUpdateTimestamp();
        //});
        //DebuggerLog.Write($"[UpdateGilCurrentCharacter] Maintain retainers took {elapsedMilliseconds} ms");

        //DebuggerLog.Write("<-- Update()");
    }

    private unsafe GilCharacter? GetCharacter()
    {
        GilCharacter? existingCharacter = null;

        // Get the current character logged in
        (string Name, uint HomeWorldId, uint Gil)? pluginSessionCharacter = DalamudGameHelper.GetCurrentCharacterDetails();

        if (pluginSessionCharacter == null)
        {
            // TODO: handle this better
            DebuggerLog.Write("Can't fetch the current character - bail out - What is wrong with you Dalamud?");
            return null;
        }

        // *** Update the character ***
        // Try load the character from the database
        existingCharacter = this.StorageManager.LoadCharacterByNameAndWorldId(pluginSessionCharacter.Value.Name, pluginSessionCharacter.Value.HomeWorldId);
        if (existingCharacter != null)
        {
            // Update the existing character
            existingCharacter.Gil = pluginSessionCharacter.Value.Gil; // Set the characters current gil amount
            this.StorageManager.UpdateCharacter(existingCharacter);
        }
        else
        {
            // Add the new character.... but first check if it could be renamed character -
            // this ain't easy tho, because the game doesn't provide a unique identifier for our characters (at least to my knowledge)
            // So we're using some fuzzy-retainer-magic-match logic here....
            Guid? possibleOldCharacter = null;
            if (AreYouReallyANewCharacter(out possibleOldCharacter)) // Check if character could have been renamed
            {
                GilCharacter newCharacter = new GilCharacter
                {
                    // *** This is important: a new character needs a unique identifier ***
                    UniqueId = Guid.NewGuid(),
                    // Character's name
                    Name = pluginSessionCharacter.Value.Name,
                    // Character's home world ID
                    HomeWorldId = pluginSessionCharacter.Value.HomeWorldId,
                    // Get the current character's amount of gil
                    Gil = pluginSessionCharacter.Value.Gil
                };

                this.StorageManager.AddCharacter(newCharacter);
                existingCharacter = newCharacter;
            }
            else
            {
                if (possibleOldCharacter != null)
                {
                    // This is probably a renamed character - update the characters' name in our backend storage
                    existingCharacter = this.StorageManager.LoadCharacterByUniqueId(possibleOldCharacter.Value);
                    if (existingCharacter != null)
                    {
                        DebuggerLog.Write(
                            $"Existing character '{existingCharacter.Name}' (WorldId: {existingCharacter.HomeWorldId}) " +
                            $"has been renamed to '{pluginSessionCharacter.Value.Name}' (WorldId: {pluginSessionCharacter.Value.HomeWorldId})");
                        // TODO: write to game chat too - might be nice for the user to know

                        existingCharacter.Name = pluginSessionCharacter.Value.Name;
                        existingCharacter.HomeWorldId = pluginSessionCharacter.Value.HomeWorldId;
                        this.StorageManager.UpdateCharacter(existingCharacter);
                    }
                    else
                    {
                        // This went haywire - bail out
                        DebuggerLog.Write("Error, renamed character logic mismatch. Possible character match wasn't found.");
                        return null;
                    }
                }
                else
                {
                    // This went haywire - bail out
                    DebuggerLog.Write("Error, funky renamed character logic went haywire. Abort.");
                    return null;
                }
            }
        }

        return existingCharacter;
    }

    // Convert Dalamud retainer to our retainer
    private GilRetainer ConvertToGilRetainer(RetainerManager.Retainer retainer, Guid ownerCharacterId)
    {
        return new GilRetainer
        {
            RetainerId = retainer.RetainerId,
            Name = retainer.NameString,
            Gil = retainer.Gil,
            OwnerCharacterId = ownerCharacterId
        };
    }

    /// <summary>
    /// Checks if the character is a renamed character.
    /// Using a funky logic to determine if the character is a renamed character.
    /// Returns true if the character is a new character, false if it is a renamed character.
    /// </summary>
    private unsafe bool AreYouReallyANewCharacter(out Guid? possibleOldCharacterId)
    {
        List<GilRetainer> pluginRetainers = new();

        var retainerManager = RetainerManager.Instance();
        for (int i = 0; i < retainerManager->Retainers.Length; i++)
        {
            RetainerManager.Retainer retainer = retainerManager->Retainers[i];
            GilRetainer thisRetainer = ConvertToGilRetainer(retainer, Guid.Empty);
            pluginRetainers.Add(thisRetainer);
        }


        List<GilRetainer> storedRetainers = this.StorageManager.GetAllRetainers();



        foreach (GilRetainer storedRetainer in storedRetainers)
        {
            foreach (GilRetainer pluginRetainer in pluginRetainers)
            {
                if (storedRetainer.Name == pluginRetainer.Name && storedRetainer.RetainerId == pluginRetainer.RetainerId)
                {
                    // We might have a match - this retainer probably belongs to the current logged in character
                    // Get the character details for this match
                    possibleOldCharacterId = storedRetainer.OwnerCharacterId;
                    return false;
                }
            }
        }

        possibleOldCharacterId = null;
        return true;
    }



    /// <summary>
    /// Returns true if it is a new day (since last updated timestamp), false if it is still the same day.
    /// This will be the first time after midnight the player engages with a retainer summoning bell.
    /// </summary>
    private bool IsNextDayLogic()
    {
        DateTime? lastUpdated = this.StorageManager.GetLastUpdateTimestamp();
        if (lastUpdated != null)
        {
            if (lastUpdated.Value.Date == DateTime.Now.Date)
            {
                // Still same day as last updated date
                return false;
            }
            // If lastUpdated is after today, then we have a problem - somebody has tampered with the system clock - wonky crooked clock
            if (lastUpdated.Value.Date > DateTime.Now.Date)
            {
                //TODO: handle this better, there is something bizarre going on
                DebuggerLog.Write("Error, last updated timestamp is after today.");
                throw new InvalidOperationException("LastUpdateTimestamp - System clock sync issue - bail out.");
            }

            return true;
        }
        else
        {
            DebuggerLog.Write("Error, last updated timestamp is not set.");
            throw new InvalidOperationException("LastUpdateTimestamp - timestamp not set.");
        }
    }

    internal void PrintGilReportForToday()
    {
        GilCharacter? currentCharacter = GetCharacter();
        if (currentCharacter != null)
        {
            long gainLossToday = this.StorageManager.CalculateGilIncomeSinceMidnight(currentCharacter.UniqueId);
            if (gainLossToday > 0)
            {
                PrintToChat($"Today's gil gain: {MiscHelpers.FormatNumber(gainLossToday)}");
            }
            else if (gainLossToday < 0)
            {
                PrintToChat($"Today's gil loss: {MiscHelpers.FormatNumber(gainLossToday)}");
            }
            else
            {
                PrintToChat("Today's gil gain/loss: 0");
            }
        }
    }

    private void PrintToChat(string message)
    {
        ChatGui.Print(message);
    }

    #region *** IDisposable ***

    private bool disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources here.

                DebuggerLog.Write("GilTracker is disposing managed resources.");

                StorageManager?.Dispose();
            }

            // Dispose unmanaged resources here.

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~GilTrackerManager()
    {
        Dispose(disposing: false);
    }

    #endregion
}
