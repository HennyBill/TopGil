using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static FFXIVClientStructs.FFXIV.Client.Game.RetainerManager;

namespace TopGil;


internal class GilUpdateEngine : IDisposable
{
    private IClientState ClientState { get; set; } = null!;
    private IChatGui ChatGui { get; set; } = null!;
    private IPluginLog Log { get; set; } = null!;
    private IFramework Framework { get; set; } = null!;

    private Configuration PluginConfig { get; set; } = null!;

    private bool isUpdating = false;

    public GilUpdateEngine(Configuration config, IClientState clientState, IChatGui chatGui, IPluginLog log, IFramework framework)
    {
        PluginConfig = config;
        ClientState = clientState;
        ChatGui = chatGui;
        Log = log;
        Framework = framework;
    }

    /// <summary>
    /// Gets the current characters' and its retainers' gil.
    /// </summary>
    internal unsafe GilCharacterWithRetainers? GetCurrentCharacterGil(UpdateSourceType updateSource)
    {
        if (this.isUpdating) return null; // Check if an update is already in progress

        this.isUpdating = true; // Set the flag to indicate an update is in progress

        GilCharacterWithRetainers? result = null;

        try
        {
            DebuggerLog.Write($"->Update(updateSource = {updateSource})");


            DebuggerLog.Write("<-Update()");
            return result;
        }
        finally
        {
            this.isUpdating = false; // Reset the flag to indicate the update is complete
        }

        return result;
    }

    private unsafe GilCharacterWithRetainers UpdateCharacterData()
    {
        //uint currCharacterId = ClientState.LocalPlayer.NameId;
        uint currCharacterHomeWorldId = ClientState.LocalPlayer.HomeWorld.Id;
        string currCharacterName = ClientState.LocalPlayer.Name.ToString();
        uint currCharacterGil = InventoryManager.Instance()->GetGil(); // Get the current character's amount of gil

        GilCharacterWithRetainers currentCharacter = new GilCharacterWithRetainers(currCharacterName, currCharacterHomeWorldId, currCharacterGil);
        //TopGilCharacter? currentCharacter = PluginConfig.Characters.FirstOrDefault(c => c.Name == currCharacterName);

        //if (currentCharacter == null)
        //{
        //    AddNewCharacter(updatedCharacters, currCharacterName, currCharacterHomeWorldId, currCharacterGil);
        //}
        //else
        //{
        //    UpdateExistingCharacter(currentCharacter, currCharacterHomeWorldId, currCharacterGil);
        //}

        UpdateRetainers(currentCharacter);

        return currentCharacter;
    }

    //private void AddNewCharacter(List<TopGilCharacter> updatedCharacters, string name, uint homeWorldId, uint gil)
    //{
    //    ChatGui.Print($"New character detected: {name}. Gil = {FormatNumber(gil)}");
    //    DebuggerLog.Write($"New character detected: {name}. Gil = {FormatNumber(gil)}");

    //    TopGilCharacter newCharacter = new TopGilCharacter
    //    {
    //        Name = name,
    //        HomeWorldId = homeWorldId,
    //        Gil = gil,
    //    };

    //    updatedCharacters.Add(newCharacter);
    //}

    //private void UpdateExistingCharacter(TopGilCharacter character, uint homeWorldId, uint gil)
    //{
    //    character.HomeWorldId = homeWorldId;
    //    character.Gil = gil;
    //    character.LastUpdate = DateTime.Now;

    //    DebuggerLog.Write($"{character.Name} has {FormatNumber(gil)} Gil");
    //}

    private unsafe void UpdateRetainers(GilCharacterWithRetainers character)
    {
        DebuggerLog.Write($"Updating retainers for {character.Name}");

        var retainerManager = RetainerManager.Instance();
        if (retainerManager != null)
        {
            for (int i = 0; i < retainerManager->Retainers.Length; i++)
            {
                var retainer = retainerManager->Retainers[i];

                DebuggerLog.Write($" * Retainer {i + 1}: {retainer.NameString}. Gil = {FormatNumber(retainer.Gil)}");

                if (retainer.NameString == "RETAINER" || retainer.RetainerId == 0)
                {
                    DebuggerLog.Write("Invalid retainer - skipping.");
                    continue;
                }

                UpdateRetainer(character, retainer);
            }
        }
        else
        {
            DebuggerLog.Write("No retainers found - RetainerManager is null.");
        }
    }

    private void UpdateRetainer(GilCharacterWithRetainers character, Retainer retainer)
    {
        var existingRetainer = character.Retainers.FirstOrDefault(r => r.RetainerId == retainer.RetainerId);

        if (existingRetainer == null)
        {
            AddNewRetainer(character, retainer);
        }
        else
        {
            UpdateExistingRetainer(existingRetainer, retainer);
        }
    }

    private void AddNewRetainer(GilCharacterWithRetainers character, Retainer retainer)
    {
        DebugPrintToChat($"Adding new retainer: {retainer.NameString} ({retainer.RetainerId}). Gil = {FormatNumber(retainer.Gil)}");
        DebuggerLog.Write($"- Adding new retainer: {retainer.NameString} ({retainer.RetainerId}). Gil = {FormatNumber(retainer.Gil)}");

        GilRetainer newRetainer = new GilRetainer
        {
            Name = retainer.NameString,
            RetainerId = retainer.RetainerId,
            Gil = retainer.Gil,
            LastUpdate = DateTime.Now
        };

        character.Retainers.Add(newRetainer);
    }

    private void UpdateExistingRetainer(GilRetainer existingRetainer, Retainer retainer)
    {
        if (retainer.NameString != existingRetainer.Name)
        {
            DebugPrintToChat($"{existingRetainer.Name} has been renamed to {retainer.NameString}");
            DebuggerLog.Write($"{existingRetainer.Name} has been renamed to {retainer.NameString}. Gil = {FormatNumber(retainer.Gil)}");
            existingRetainer.Name = retainer.NameString;
        }

        existingRetainer.Gil = retainer.Gil;
        existingRetainer.LastUpdate = DateTime.Now;
    }

    // ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// TODO: move this to a separate class - it not part of the update engine anymore
    /// </summary>
    internal void PrintGilReport(bool showDetails = false)
    {
        DebuggerLog.Write("->PrintTopGil()");

        // Update before printing
        List<GilCharacterWithRetainers>? result = this.Update(UpdateSourceType.ShowTopGil);
        if (result == null)
        {
            Configuration.Characters = result;
            Configuration.Save();
            return;
        }

        uint totalGil = 0;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Top Gil:");

        // For each of our characters, print their gil and their retainers' gil
        foreach (var character in PluginConfig.Characters.OrderByDescending(c => c.Gil))
        {
            DebuggerLog.Write($"{character.Name} has {FormatNumber(character.Gil)} Gil. Last update on {FormatDateTime(character.LastUpdate)}");

            totalGil += character.Gil;
            sb.AppendLine($"{character.Name}: {FormatNumber(character.Gil)} gil.");

            foreach (var retainer in character.Retainers.OrderByDescending(r => r.Gil))
            {
                DebuggerLog.Write($" * {retainer.Name} has {FormatNumber(retainer.Gil)} Gil. Last update on {FormatDateTime(retainer.LastUpdate)}");

                totalGil += retainer.Gil;
                sb.AppendLine($"\t{retainer.Name}: {FormatNumber(retainer.Gil)} gil.");
            }

            sb.AppendLine();
        }
        // Remove last empty line
        sb.Remove(sb.Length - 2, 2);

        if (showDetails)
        {
            PrintToChat(sb.ToString());
        }
        PrintToChat($"Total top gil: {FormatNumber(totalGil)}");

        DebuggerLog.Write($"Total top gil: {FormatNumber(totalGil)}");
        DebuggerLog.Write("<-PrintTopGil()");
    }

    // ------------------------------------------------------------------------------------------------------

    private string FormatNumber(uint number)
    {
        return number.ToString("N0");  // .Replace(",", ".")
    }

    private string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void PrintToChat(string message)
    {
        ChatGui.Print(message);
    }

    public void DebugPrintToChat(string message)
    {
        if (PluginConfig.DebugEnabled)
        {
            ChatGui.Print(message);
            DebuggerLog.Write(message);
        }
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

    ~GilUpdateEngine()
    {
        Dispose(disposing: false);
    }

    #endregion
}
