using FFXIVClientStructs.FFXIV.Client.Game;

using System;

namespace TopGil;

internal static class DalamudGameHelper
{
    /// <summary>
    /// Gets the currently logged in characters' basic info - name, world and amount of gil in the bag.
    /// </summary>
    internal static unsafe (string Name, uint HomeWorldId, uint Gil)? GetCurrentCharacter()
    {
        if (Plugin.ClientState.LocalPlayer == null)
        {
            DebuggerLog.Write("GetCurrentCharacter: LocalPlayer is null");
            return null;
        }

        //DebuggerLog.Write($"Info GetCurrentCharacter: LocalPlayer {Plugin.ClientState.LocalPlayer.Name.ToString()}, with NameId '{Plugin.ClientState.LocalPlayer.NameId.ToString()}'");

        return (Plugin.ClientState.LocalPlayer.Name.ToString(),
                Plugin.ClientState.LocalPlayer.HomeWorld.Id,
                InventoryManager.Instance()->GetGil());
    }

}
