/*
internal unsafe void Update(TopGilEngineUpdateType updateSource)
{
    if (isUpdating) return; // Check if an update is already in progress

    isUpdating = true; // Set the flag to indicate an update is in progress

    Framework.RunOnFrameworkThread(() =>
    {
        try
        {
            DebuggerLog.Write($"->Update(updateSource = {updateSource})");

            if (ClientState.LocalPlayer != null)
            {
                uint currCharacterId = ClientState.LocalPlayer.NameId;
                uint currCharacterHomeWorldId = ClientState.LocalPlayer.HomeWorld.Id;
                string currCharacterName = ClientState.LocalPlayer.Name.ToString();
                uint currCharacterGil = InventoryManager.Instance()->GetGil(); // Get the current character's amount of gil

                TopGilCharacter? currentCharacter = PluginConfig.Characters.FirstOrDefault(c => c.Name == currCharacterName);

                // Check if we have registered the current player's character before. If not, add it to our list.
                if (currentCharacter == null)
                {
                    ChatGui.Print($"New character detected: {currCharacterName}. Gil = {currCharacterGil}");
                    DebuggerLog.Write($"New character detected: {currCharacterName}. Gil = {currCharacterGil}");

                    // Add the new character to the list
                    TopGilCharacter newCharacter = new TopGilCharacter
                    {
                        Name = currCharacterName,
                        HomeWorldId = currCharacterHomeWorldId,
                        Gil = currCharacterGil,
                    };

                    PluginConfig.Characters.Add(newCharacter);

                    currentCharacter = newCharacter;
                }

                currentCharacter.HomeWorldId = currCharacterHomeWorldId;
                currentCharacter.Gil = currCharacterGil;
                currentCharacter.LastUpdate = DateTime.Now;

                DebuggerLog.Write($"{currCharacterName} has {currCharacterGil} Gil");
                DebuggerLog.Write($"Updating retainers for {currCharacterName}");

                // Next we'll check how much gil this character has deposited at the retainers
                var retainerManager = RetainerManager.Instance();
                if (retainerManager != null)
                {
                    for (int i = 0; i < retainerManager->Retainers.Length; i++)
                    {
                        var retainer = retainerManager->Retainers[i];

                        DebuggerLog.Write($" * Retainer {i + 1}: {retainer.NameString}. Gil = {retainer.Gil}");

                        if (retainer.NameString == "RETAINER" || retainer.RetainerId == 0)
                        {
                            DebuggerLog.Write("Invalid retainer - skipping.");
                            continue;
                        }

                        // Check if we have registered this retainer before... If not, add it to our list.
                        if (!currentCharacter.Retainers.Any(r => r.RetainerId == retainer.RetainerId))
                        {
                            // Not, going to add it now
                            DebugPrintToChat($"Adding new retainer: {retainer.NameString} ({retainer.RetainerId}). Gil = {retainer.Gil}");
                            DebuggerLog.Write($"- Adding new retainer: {retainer.NameString} ({retainer.RetainerId}). Gil = {retainer.Gil}");

                            // Add the new retainer to the list
                            TopGilRetainer newRetainer = new TopGilRetainer
                            {
                                Name = retainer.NameString,
                                RetainerId = retainer.RetainerId,
                                Gil = retainer.Gil,
                                LastUpdate = DateTime.Now
                            };

                            currentCharacter.Retainers.Add(newRetainer);
                        }
                        else
                        {
                            // Retainer already exists - update gil amount
                            var existingRetainer = currentCharacter.Retainers.First(r => r.RetainerId == retainer.RetainerId);

                            // Check if retainer has been renamed - update accordingly
                            if (retainer.NameString != existingRetainer.Name)
                            {
                                existingRetainer.Name = retainer.NameString;
                                DebugPrintToChat(existingRetainer.Name + " has been renamed to " + retainer.NameString);
                                DebuggerLog.Write(existingRetainer.Name + " has been renamed to " + retainer.NameString + ". Gil = " + retainer.Gil);
                            }

                            // Update the retainers gil amount
                            existingRetainer.Gil = retainer.Gil;
                            existingRetainer.LastUpdate = DateTime.Now;
                        }
                    }
                }
                else
                {
                    DebuggerLog.Write("No retainers found - RetainerManager is null.");
                }

                PluginConfig.Save();
            }
            else
            {
                //ChatGui.Print("Could not retrieve character data.");
                //Log.Debug($"Could not retrieve character data. onLogout = {onLogout}");
            }

            DebuggerLog.Write("<-Update()");
        }
        finally
        {
            isUpdating = false; // Reset the flag to indicate the update is complete
        }
    });
}
*/

