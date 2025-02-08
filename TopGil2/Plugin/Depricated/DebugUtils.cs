using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace TopGil;

public static class DebugUtilsOld
{
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    /// <summary>
    /// Print a message to the chat window - if this is a debug build.
    /// </summary>
    public static void Print(string message)
    {
#if DEBUG
        ChatGui.Print(message);
#endif
    }
}

