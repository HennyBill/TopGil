using Dalamud.Game.ClientState.Conditions;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace TopGil;

internal static class DalamudGameHelper
{
    /// <summary>
    /// Returns the currently logged in character details via Dalamud plugin ClientState.
    /// Null, if the character details are not available yet.
    /// </summary>
    internal static unsafe (string Name, uint HomeWorldId, uint Gil)? GetCurrentCharacterDetails()
    {
        if (Plugin.ClientState.LocalPlayer == null)
        {
            DebuggerLog.Write("Plugin.ClientState.LocalPlayer is null. Cannot retrieve character details yet?");
            return null;
        }

        return (Plugin.ClientState.LocalPlayer.Name.ToString(),
                Plugin.ClientState.LocalPlayer.HomeWorld.RowId, // In Api10 this was Id
                InventoryManager.Instance()->GetGil());
    }

    internal static bool IsLoggedIn()
    {

        return Plugin.ClientState.IsLoggedIn;
    }

    internal static bool IsInDuty()
    {
        return Plugin.Condition[ConditionFlag.BoundByDuty];
    }

    internal static bool IsInPvP()
    {
        return Plugin.ClientState.IsPvPExcludingDen;
    }

    internal static unsafe bool IsRetainerManagerReady()
    {
        var retainerManager = RetainerManager.Instance();
        if (retainerManager == null)
        {
            DebuggerLog.Write("RetainerManager is null - cannot check retainers.");
            return false;
        }

        if (retainerManager->Retainers == null)
        {
            DebuggerLog.Write("RetainerManager.Retainers is null - cannot check retainers.");
            return false;
        }

        return true;
    }

    internal static unsafe int GetActualNumberOfRetainers()
    {
        int result = 0;

        if (IsRetainerManagerReady())
        {
            var retainerManager = RetainerManager.Instance();

            for (int i = 0; i < retainerManager->Retainers.Length; i++)
            {
                RetainerManager.Retainer retainer = retainerManager->Retainers[i];

                if (retainer.RetainerId == 0 || retainer.NameString == "RETAINER")
                {
                    continue;
                }

                result++;
            }
        }

        return result;
    }

    internal static string? GetCurrentTargetName()
    {
        if (Plugin.Targets.Target != null)
        {
            return Plugin.Targets.Target.Name.ToString();
        }
        return null;
    }
}
