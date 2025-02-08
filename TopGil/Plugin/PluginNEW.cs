using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using System.Collections.Generic;

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
    [PluginService] public static ITargetManager Targets { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;

    internal const string PluginName = "TopGil";
    private CharacterRetainerManager _CharacterRetainerManager = null!;

    internal const string CommandTopGil = "/topgil";
    internal const string CommandUserRequestUpdate = "/tgupdate";
    internal const string CommandDebugToggle = "/tgdebug";
    internal const string CommandDevModeToggle = "/tgdev";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new(PluginName);
    public ConfigWindow ConfigWindow { get; init; }
    public MainWindow MainWindow { get; init; }

    private readonly CommandHandler _commandHandler;
    private readonly EventHandler _eventHandler;
    private readonly UIHandler _uiHandler;

    private bool _isDevShowConditionChangesOn = false;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        DebuggerLog.Init(Configuration);
        _CharacterRetainerManager = new CharacterRetainerManager(this);

        if (Configuration.DebugEnabled)
        {
            PrintToChat($"{PluginName} debug mode is enabled. Type {CommandDebugToggle} to toggle debug-mode on/off.");
            PrintToChat($"TopGil logfile: {DebuggerLog.GetFullLogFileName()}");
        }

        _commandHandler = new CommandHandler(this, CommandManager);
        _eventHandler = new EventHandler(this, ClientState, Condition);
        _uiHandler = new UIHandler(this, PluginInterface);
    }

    public void Dispose()
    {
        _commandHandler.Dispose();
        _eventHandler.Dispose();
        _uiHandler.Dispose();
        _CharacterRetainerManager.Dispose();
    }

    public void OnLogin()
    {
        // Handle login event
    }

    public void OnLogout(int type, int code)
    {
        _CharacterRetainerManager.ResetCurrentCharacter();
    }

    public void ConditionChange(ConditionFlag flag, bool value)
    {
        if (_isDevShowConditionChangesOn)
        {
            PrintToChat($"Condition change: {flag} - {value}");
            if (Targets.Target != null)
            {
                PrintToChat($" -->Target: {Targets.Target.Name}");
            }
        }

        if (flag == ConditionFlag.OccupiedSummoningBell)
        {
            HandleSummoningBellCondition(value);
        }
    }

    /// <summary>
    /// Handle the condition change for the summoning bell.
    /// </summary>
    /// <param name="value"></param>
    private void HandleSummoningBellCondition(bool value)
    {
        if (value)
        {
            conditionChangeHelper.IsSummoningBellClicked = IsTargetSummoningBell();

            if (!conditionChangeHelper.IsSummoningBellClicked)
            {
                DebuggerLog.Write($"Player clicked on the company chest - not the summoning bell.");
                return;
            }

            _CharacterRetainerManager.SetCurrentCharacter();
            conditionChangeHelper.TotalGilStatusBefore = _CharacterRetainerManager.GetTotalGilCharacter(runUpdateFirst: false);
            DebuggerLog.Write($"Total Gil before: {NumberFormatter.FormatNumber(conditionChangeHelper.TotalGilStatusBefore)}");

            bool isRMready = DalamudGameHelper.IsRetainerManagerReady();
            DebuggerLog.Write($"Summoning bell enter - Is RetainerManager ready: {isRMready}");
        }
        else
        {
            if (!conditionChangeHelper.IsSummoningBellClicked)
            {
                return;
            }

            conditionChangeHelper.IsSummoningBellClicked = false;
            HandleSummoningBellExit();
        }
    }

    /// <summary>
    /// Handle the exit from the summoning bell.
    /// </summary>
    private void HandleSummoningBellExit()
    {
        uint savedCharacterGil = _CharacterRetainerManager.GetCurrentCharacter().Gil;
        List<GameRetainer> savedRetainersData = DeepCopyRetainers(_CharacterRetainerManager.GetCurrentCharacter().Retainers);

        if (DebuggerLog.IsDebugEnabled)
        {
            foreach (GameRetainer retainer in savedRetainersData)
            {
                DebuggerLog.Write($"[Gil-check before] {retainer.Name} has {NumberFormatter.FormatNumber(retainer.Gil)} gil.");
            }
        }

        _CharacterRetainerManager.UpdateCurrentCharacter();

        long characterEarnings = (long)_CharacterRetainerManager.GetCurrentCharacter().Gil - savedCharacterGil;
        DebuggerLog.Write($"[Character only] {_CharacterRetainerManager.GetCurrentCharacter().Name}: {_CharacterRetainerManager.GetCurrentCharacter().Gil} - {savedCharacterGil} = {characterEarnings}");

        if (characterEarnings > 0)
        {
            SeString characterEarnSinceLastVisit = new SeStringBuilder()
                .AddUiForeground($"{_CharacterRetainerManager.GetCurrentCharacter().Name} ", (ushort)PluginColorValues.Blue)
                .AddUiForeground("earned ", (ushort)PluginColorValues.Green)
                .AddUiForeground($"{NumberFormatter.FormatNumber(characterEarnings)} ", (ushort)PluginColorValues.Orange)
                .AddUiForeground("gil since the last bell visit ", (ushort)PluginColorValues.Green)
                .BuiltString;
            PrintToChat(characterEarnSinceLastVisit);
        }

        conditionChangeHelper.TotalGilStatusAfter = _CharacterRetainerManager.GetTotalGilCharacter(runUpdateFirst: false);
        DebuggerLog.Write($"Total Gil after : {NumberFormatter.FormatNumber(conditionChangeHelper.TotalGilStatusAfter)}");

        int numCharacters = _CharacterRetainerManager.GetTotalCharacters();

        SeString earningsSinceLastVisit = null;

        if (conditionChangeHelper.TotalGilStatusDiff > 0)
        {
            earningsSinceLastVisit = new SeStringBuilder()
                .AddUiForeground("Earned ", (ushort)PluginColorValues.Green)
                .AddUiForeground($"{NumberFormatter.FormatNumber(conditionChangeHelper.TotalGilStatusDiff)} ", (ushort)PluginColorValues.Orange)
                .AddUiForeground("gil since the last bell visit ", (ushort)PluginColorValues.Green)
                .BuiltString;

            if (numCharacters > 1)
            {
                earningsSinceLastVisit.Append(
                new SeStringBuilder()
                .AddUiForeground($"on any {numCharacters} characters.", (ushort)PluginColorValues.Green)
                .BuiltString);
            }
        }

        List<GameRetainer> retainerList = _CharacterRetainerManager.GetCurrentCharactersRetainers();

        foreach (GameRetainer retainer in retainerList)
        {
            GameRetainer? savedRetainer = savedRetainersData.Find(r => r.RetainerId == retainer.RetainerId);
            if (savedRetainer == null)
            {
                DebuggerLog.Write($"[Gil-check after] Retainer {retainer.Name} not found in saved data!!!!!");
                continue;
            }

            long gilDiff = retainer.Gil - savedRetainer.Gil;
            DebuggerLog.Write($"[Gil-check after] {retainer.Name} earned {NumberFormatter.FormatNumber(gilDiff)} gil since last visit. Now: {retainer.Gil}. Before: {savedRetainer.Gil}");

            if (gilDiff > 0)
            {
                SeString seStringRetainerEarn = new SeStringBuilder()
                    .AddUiForeground($"{retainer.Name} ", (ushort)PluginColorValues.Yellow)
                    .AddUiForeground($"earned ", (ushort)PluginColorValues.Green)
                    .AddUiForeground($"{NumberFormatter.FormatNumber(gilDiff)} ", (ushort)PluginColorValues.Orange)
                    .AddUiForeground($"gil since last visit.", (ushort)PluginColorValues.Green)
                    .BuiltString;
                PrintToChat(seStringRetainerEarn);
            }
        }

        PrintGilSummary(earningsSinceLastVisit);
    }

    private void PrintGilSummary(SeString? optionalExtraText = null)
    {
        DebuggerLog.Write($"[PrintGilSummary] optional-Text: '{optionalExtraText}'");

        // TODO: Move these settings to configuration
        bool PrintOnExitRetainerBell = true; // Show total gil when leaving the retainer bell
        bool PrintShowDetails = false;

        // If there's an extra text to show, then we're gonna bypass the PrintOnExitRetainerBell setting, but PrintShowDetails will still be respected.
        if (PrintOnExitRetainerBell || optionalExtraText != null)
        {
            long totalGil = _CharacterRetainerManager.GetTotalGilAll(runUpdateFirst: false);

            //PrintToChat($">>> Total Gil on account: {Helper.FormatNumber(totalGil)}");
            SeString totalGilString = new SeStringBuilder()
                .AddUiForeground($"{PluginName} ", (ushort)PluginColorValues.Red)
                .AddUiForeground($"Total gil on account: ", (ushort)PluginColorValues.Purple)
                .AddUiForeground($"{NumberFormatter.FormatNumber(totalGil)}", (ushort)PluginColorValues.Orange)
                .BuiltString;
            PrintToChat(totalGilString);

            if (optionalExtraText != null)
            {
                PrintToChat(optionalExtraText);
            }

            if (PrintShowDetails)
            {
                PrintAllCharactersAndRetainers();
            }
        }
    }

    private void PrintAllCharactersAndRetainers()
    {
        long doubleCheck = 0;

        PrintToChat($"(On {_CharacterRetainerManager.GetTotalCharacters()} characters and {_CharacterRetainerManager.GetTotalRetainers()} retainers)");
        foreach (GameCharacter gameCharacters in Configuration.Characters)
        {
            doubleCheck += gameCharacters.Gil;
            PrintToChat($"{gameCharacters.Name}: {NumberFormatter.FormatNumber(gameCharacters.Gil)}");
            DebuggerLog.Write($"[PrintGilSummary] {gameCharacters.Name}: {NumberFormatter.FormatNumber(gameCharacters.Gil)}");

            foreach (GameRetainer retainer in gameCharacters.Retainers)
            {
                doubleCheck += retainer.Gil;
                PrintToChat($" | {retainer.Name}: {NumberFormatter.FormatNumber(retainer.Gil)}");
                DebuggerLog.Write($"[PrintGilSummary]  | {retainer.Name}: {NumberFormatter.FormatNumber(retainer.Gil)}");
            }
        }

        DebuggerLog.Write($"[PrintGilSummary] Double check total: {NumberFormatter.FormatNumber(doubleCheck)}");
    }

    public void OnUserRequestUpdateCommand(string command, string args)
    {
        PrintToChat($"Player requested update for current character {_CharacterRetainerManager.GetCurrentCharacterName()}");

        // Update the current character
        Framework.RunOnTick(() =>
        {
            _CharacterRetainerManager.UpdateCurrentCharacter();
        });
    }

    public void OnTopGilCommand(string command, string args)
    {
        string argsLower = args.ToLowerInvariant();

        if (argsLower.Length == 0 || argsLower.EqualsIgnoreCaseAny("help", "?"))
        {
            PrintToChat($"{PluginName} - Command usage: {CommandTopGil} [argument]");
            PrintToChat($"Arguments:");
            PrintToChat($"  ? | help (this help)");
            PrintToChat($"  show (show Gil summary)");
            PrintToChat($"  list (list all characters)");
            PrintToChat($"  blacklist \"name\" (blacklist a character - don't include in Gil summary)");
            PrintToChat($"  unblacklist \"name\" (unblacklist a character)");
            PrintToChat($"  reset all (reset entire list of characters and retainers - NO WARNING!)");
            return;
        }
        if (argsLower.EqualsIgnoreCase("show"))
        {
            PrintGilSummary();
            return;
        }
        if (argsLower.EqualsIgnoreCase("list"))
        {
            PrintToChat("Characters:");
            Configuration.Characters.ForEach(character =>
            {
                PrintToChat($"{character.Name} ({character.HomeWorldId})");
            });
            return;
        }
        if (argsLower.EqualsIgnoreCase("blacklist"))
        {
            // blacklist character with name inclosed in double quotation mark
            PrintToChat("Not implemented yet.");
            return;
        }
        if (argsLower.EqualsIgnoreCase("unblacklist"))
        {
            PrintToChat("Not implemented yet.");
            return;
        }
        if (argsLower.EqualsIgnoreCase("reset all"))
        {
            Configuration.Characters.Clear();
            Configuration.Save();
            PrintToChat("Character and retainer list has been reset.");
            return;
        }

        PrintToChat("Invalid argument.");
    }

    public void OnDevModeToggleCommand(string command, string args)
    {
        string argsLower = args.ToLowerInvariant();
        if (argsLower == "conditionon")
        {
            if (_isDevShowConditionChangesOn)
            {
                PrintToChat("Show ConditionChange are already shown.");
                return;
            }
            _isDevShowConditionChangesOn = true;
            PrintToChat("Show ConditionChange are now on.");
            return;
        }
        if (argsLower == "conditionoff")
        {
            if (!_isDevShowConditionChangesOn)
            {
                PrintToChat("Show ConditionChange are already off.");
                return;
            }
            _isDevShowConditionChangesOn = false;
            PrintToChat("Show ConditionChange are now off.");
            return;
        }

        PrintToChat("Arguments are:");
        PrintToChat("  conditionon (show condition changes)");
        PrintToChat("  conditionoff (hide condition changes)");
    }

    public void PrintToChat(string message)
    {
        ChatGui.Print(message);
    }

    public void PrintToChat(SeString message)
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

    public static SeString SuccessMessage(string success)
    {
        return new SeStringBuilder()
               .AddUiForeground("[Submarine Tracker] ", (ushort)PluginColorValues.Orange)
               .AddUiForeground($"{success}", (ushort)PluginColorValues.Red)
               .BuiltString;
    }

    public static void PrintSomeColorKeys()
    {
        // Ushort: 0-65535
        for (ushort i = 0; i < 55; i++)
        {
            ChatGui.Print(
                new SeStringBuilder().AddUiForeground($"Color {i}", i).BuiltString
            );
        }
    }

    internal enum PluginColorValues
    {
        Orange = 28,
        Purple = 48,
        Red = 16,
        Yellow = 25,
        Blue = 37,
        Green = 45
    }

    internal class ConditionChangeStateData
    {
        internal bool IsSummoningBellClicked { get; set; } = false;
        internal long TotalGilStatusBefore { get; set; } = 0;
        internal long TotalGilStatusAfter { get; set; } = 0;
        internal long TotalGilStatusDiff => TotalGilStatusAfter - TotalGilStatusBefore;
    }

    private ConditionChangeStateData conditionChangeHelper = new ConditionChangeStateData();

    private List<GameRetainer> DeepCopyRetainers(List<GameRetainer> originalRetainers)
    {
        List<GameRetainer> copiedRetainers = new List<GameRetainer>();

        foreach (var retainer in originalRetainers)
        {
            copiedRetainers.Add(new GameRetainer
            {
                Name = retainer.Name,
                RetainerId = retainer.RetainerId,
                Gil = retainer.Gil,
                OwnerCharacterId = retainer.OwnerCharacterId,
                LastVisited = retainer.LastVisited
            });
        }

        return copiedRetainers;
    }

    private bool IsTargetSummoningBell()
    {
        return Targets.Target != null && Targets.Target.Name.ToString().Equals("Summoning Bell");
    }
}



/// <summary>
/// Handles all the commands for the plugin.
/// </summary>
public class CommandHandler
{
    private readonly Plugin _plugin;
    private readonly ICommandManager _commandManager;

    public CommandHandler(Plugin plugin, ICommandManager commandManager)
    {
        _plugin = plugin;
        _commandManager = commandManager;
        RegisterCommands();
    }

    private void RegisterCommands()
    {
        _commandManager.AddHandler(Plugin.CommandTopGil, new CommandInfo(OnTopGilCommand)
        {
            HelpMessage = "Prints how much Gil you own.",
            ShowInHelp = true,
        });

        _commandManager.AddHandler(Plugin.CommandUserRequestUpdate, new CommandInfo(OnUserRequestUpdateCommand)
        {
            HelpMessage = "Force update for current character.",
            ShowInHelp = false,
        });

        _commandManager.AddHandler(Plugin.CommandDebugToggle, new CommandInfo(OnDebugToggleCommand)
        {
            HelpMessage = "Toggle debug mode.",
            ShowInHelp = false,
        });

        _commandManager.AddHandler(Plugin.CommandDevModeToggle, new CommandInfo(OnDevModeToggleCommand)
        {
            HelpMessage = "Developer mode options: timeron | timeroff",
            ShowInHelp = false,
        });
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler(Plugin.CommandTopGil);
        _commandManager.RemoveHandler(Plugin.CommandUserRequestUpdate);
        _commandManager.RemoveHandler(Plugin.CommandDebugToggle);
        _commandManager.RemoveHandler(Plugin.CommandDevModeToggle);
    }

    private void OnTopGilCommand(string command, string args)
    {
        _plugin.OnTopGilCommand(command, args);
    }

    private void OnUserRequestUpdateCommand(string command, string args)
    {
        _plugin.OnUserRequestUpdateCommand(command, args);
    }

    private void OnDebugToggleCommand(string command, string args)
    {
        _plugin.Configuration.DebugEnabled = !_plugin.Configuration.DebugEnabled;
        _plugin.Configuration.Save();
        _plugin.PrintToChat($"Debug mode is now {(_plugin.Configuration.DebugEnabled ? "enabled" : "disabled")}.");
    }

    private void OnDevModeToggleCommand(string command, string args)
    {
        _plugin.OnDevModeToggleCommand(command, args);
    }
}

/// <summary>
/// Handles all the events for the plugin.
/// </summary>
public class EventHandler
{
    private readonly Plugin _plugin;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;

    public EventHandler(Plugin plugin, IClientState clientState, ICondition condition)
    {
        _plugin = plugin;
        _clientState = clientState;
        _condition = condition;
        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        _clientState.Login += OnLogin;
        _clientState.Logout += OnLogout;
        _condition.ConditionChange += OnConditionChange;
    }

    public void Dispose()
    {
        _clientState.Login -= OnLogin;
        _clientState.Logout -= OnLogout;
        _condition.ConditionChange -= OnConditionChange;
    }

    private void OnLogin()
    {
        _plugin.OnLogin();
    }

    private void OnLogout(int type, int code)
    {
        _plugin.OnLogout(type, code);
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        _plugin.ConditionChange(flag, value);
    }
}

/// <summary>
/// Handles all the UI for the plugin.
/// </summary>
public class UIHandler
{
    private readonly Plugin _plugin;
    private readonly IDalamudPluginInterface _pluginInterface;
    public readonly WindowSystem WindowSystem;

    public UIHandler(Plugin plugin, IDalamudPluginInterface pluginInterface)
    {
        _plugin = plugin;
        _pluginInterface = pluginInterface;
        WindowSystem = new WindowSystem(Plugin.PluginName);
        _pluginInterface.UiBuilder.Draw += DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        _pluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        _pluginInterface.UiBuilder.Draw -= DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        _pluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
    }

    private void DrawUI() => WindowSystem.Draw();

    private void ToggleConfigUI() => _plugin.ConfigWindow.Toggle();

    private void ToggleMainUI() => _plugin.MainWindow.Toggle();
}


