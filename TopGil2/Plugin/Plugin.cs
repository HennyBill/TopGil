using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using System.Collections.Generic;
using System.IO;

using TopGil.Windows;

namespace TopGil;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    //[PluginService] public static ITargetManager Targets { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;


    internal const string PluginName = "TopGil"; // "GilManiac" "GilGold", "Gilculator"

    private string currentPlayerCharacterName = "Unknown";
    private readonly GilTrackerManager GilTracker;


    private const string CommandSettings = "/tgsettings";
    private const string CommandTopGil = "/topgil";
    private const string CommandUserRequestUpdate = "/tgupdate";
    private const string CommandDebugToggle = "/tgdebug";
    private const string CommandDevModeToggle = "/tgdev";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new(PluginName);
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }


    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        DebuggerLog.Init(Configuration);

        this.GilTracker = new GilTrackerManager(Configuration, ClientState, ChatGui, Log, Framework);

        if (Configuration.DebugEnabled)
        {
            PrintToChat($"{PluginName} debug mode is enabled. Type {CommandDebugToggle} to toggle debug-mode on/off.");
            PrintToChat($"TopGil logfile: {DebuggerLog.GetLogFilePath()}");
        }


        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);


        // Register command handlers for the slash commands
        CommandManager.AddHandler(CommandSettings, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Open settings.",
            ShowInHelp = true,
        });

        CommandManager.AddHandler(CommandTopGil, new CommandInfo(OnTopGilCommand)
        {
            HelpMessage = "Prints how much Gil you own. Optional arguments: details",
            ShowInHelp = true,
        });

        CommandManager.AddHandler(CommandUserRequestUpdate, new CommandInfo(OnUserRequestUpdateCommand)
        {
            HelpMessage = "Force update for current character.",
            ShowInHelp = true,
        });

        CommandManager.AddHandler(CommandDebugToggle, new CommandInfo((command, args) =>
        {
            Configuration.DebugEnabled = !Configuration.DebugEnabled;
            Configuration.Save();
            PrintToChat($"Debug mode is now {(Configuration.DebugEnabled ? "enabled" : "disabled")}.");
        })
        {
            HelpMessage = "Toggle debug mode.",
            ShowInHelp = true,
        });

        CommandManager.AddHandler(CommandDevModeToggle, new CommandInfo(OnDevModeToggleCommand)
        {
            HelpMessage = "Developer mode options: timeron | timeroff",
            ShowInHelp = false,
        });


        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;


        // Subscribe to Dalamud events
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;
        Condition.ConditionChange += ConditionChange;
    }

    private bool IsLoggedIn()
    {
        return ClientState.IsLoggedIn;
    }

    private bool IsInDuty()
    {
        return Condition[ConditionFlag.BoundByDuty];
    }

    private bool IsInPvP()
    {
        if (ClientState.LocalPlayer == null)
            return false;
        return ClientState.IsPvPExcludingDen;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandSettings);
        CommandManager.RemoveHandler(CommandTopGil);
        CommandManager.RemoveHandler(CommandUserRequestUpdate);
        CommandManager.RemoveHandler(CommandDebugToggle);
        CommandManager.RemoveHandler(CommandDevModeToggle);

        // Unsubscribe from events
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        Condition.ConditionChange -= ConditionChange;
    }


    private void OnSettingsCommand(string command, string args)
    {
        //ToggleMainUI();
    }

    // Chat command: /topgil (optional arguments: details)
    private void OnTopGilCommand(string command, string args)
    {
        string argsLower = args.ToLowerInvariant();

        if (argsLower.Length == 0 || argsLower.EqualsIgnoreCaseAny("help", "?"))
        {
            PrintToChat($"{PluginName} - Command usage: {CommandTopGil} [argument]");
            PrintToChat($" Arguments:");
            PrintToChat($" ? | help    - this help");
            PrintToChat($" today       - show daily Gil report");
            return;
        }
        if (argsLower.EqualsIgnoreCase("today"))
        {
            PrintToChat($"{PluginName} - Daily Gil report for {currentPlayerCharacterName}");
            GilTracker.PrintGilReportForToday();
            return;
        }
    }

    private void OnUserRequestUpdateCommand(string command, string args)
    {
        PrintToChat($"{PluginName} - Update for current character {currentPlayerCharacterName}");

        //Framework.RunOnTick(() =>
        //{
        //    this.GilTracker.UpdateGilCurrentCharacter(UpdateSourceType.UserRequest);
        //});

        this.GilTracker.DoUpdate(UpdateSourceType.UserRequest);

        //Framework.RunOnTick(() =>
        //{
        //    List<GilCharacterWithRetainers>? result = topGilEngine.Update(UpdateSourceType.UserRequest);
        //    if (result != null)
        //    {
        //        Configuration.Characters = result;
        //        Configuration.Save();
        //        return;
        //    }
        //});
    }

    // Chat command: /tgdev timeron | timeroff
    private void OnDevModeToggleCommand(string command, string args)
    {
        // Toggle the background timer - a developer mode command
        // TODO: remove this when we got a stable prober way to update the gil
        /*
        string argsLower = args.ToLowerInvariant();
        if (argsLower == "timeron")
        {
            if (backgroundTimer.IsRunning)
            {
                PrintToChat("Background timer is already running.");
                return;
            }
            backgroundTimer.Start();
            PrintToChat("Background timer started.");
        }
        else if (argsLower == "timeroff")
        {
            if (!backgroundTimer.IsRunning)
            {
                PrintToChat("Background timer is already stopped.");
                return;
            }
            backgroundTimer.Stop();
            PrintToChat("Background timer stopped.");
        }
        else
        {
            PrintToChat("Invalid argument.");
        }
        */
    }

    // ------------------------------------------------------------------------------------------------------

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();

    // ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Dalamud event - Condition change
    /// </summary>
    private void ConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.OccupiedSummoningBell)
        {
            if (value)
            {
                DebuggerLog.Write($"{currentPlayerCharacterName} at Summoning Bell");

                // Lets update the current characters name, it might not have been updated correctly in the OnLogin event
                currentPlayerCharacterName = GetCurrentCharacterName();
            }
            else
            {
                // Update when player leaves the summoning bell, update the gil for the current character
                DebuggerLog.Write($"{currentPlayerCharacterName} left Summoning Bell");

                // TODO: try to this to run faster - split up the method and run separately
                //Framework.RunOnTick(() =>
                //{
                    this.GilTracker.DoUpdate(UpdateSourceType.AutoSummoningBell);
                //});
            }
        }
        else
        {
            // TODO: remove this later, just for debugging and exploring Dalamud's ConditionFlag
            //DebuggerLog.Write($"ConditionChange: {flag} = {value}");
        }
    }

    /// <summary>
    /// Dalamud event - Login to game
    /// ... is this event being invoked correctly by Dalamud? Seems a bit wonky :(
    /// ... ClientState.LocalPlayer is sometimes null here, but not always!
    /// </summary>
    private void OnLogin()
    {
        currentPlayerCharacterName = GetCurrentCharacterName();
        GilTracker.OnLogin();
        DebuggerLog.Write($"Player logged in: {currentPlayerCharacterName}");
    }

    /// <summary>
    /// Dalamud event - Logout to game menu
    /// </summary>
    private void OnLogout()
    {
        GilTracker.OnLogout();
        DebuggerLog.Write($"Player logged out: {currentPlayerCharacterName}");
    }

    // ------------------------------------------------------------------------------------------------------

    private string GetCurrentCharacterName()
    {
        string unknownCharacter = "Unknown";

        // Check if we actually have a player character name already (not null and not "Unknown")
        if (ClientState.LocalPlayer == null)
        {
            return unknownCharacter;
        }
        //if (ClientState.LocalPlayer.Name.ToString().Equals(unknownCharacter))
        //{
        //    // Lets hope we can grab the name this time
        //    return ClientState.LocalPlayer.Name.ToString();
        //}

        return ClientState.LocalPlayer.Name.ToString();
    }

    // ------------------------------------------------------------------------------------------------------


    private void PrintToChat(string message)
    {
        ChatGui.Print(message);
    }

    public void DebugPrintToChat(string message)
    {
        if (Configuration.DebugEnabled)
        {
            ChatGui.Print(message);
            DebuggerLog.Write(message);
        }
    }
}


