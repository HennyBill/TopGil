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
        _CharacterRetainerManager = new CharacterRetainerManager(this);

        if (Configuration.DebugEnabled)
        {
            PrintToChat($"{PluginName} debug mode is enabled. Type {CommandDebugToggle} to toggle debug-mode on/off.");
            PrintToChat($"TopGil logfile: {DebuggerLog.GetFullLogFileName()}");
        }


        // Register command handlers for the slash commands
        CommandManager.AddHandler(CommandTopGil, new CommandInfo(OnTopGilCommand)
        {
            HelpMessage = "Prints how much Gil you own.",
            ShowInHelp = true,
        });

        CommandManager.AddHandler(CommandUserRequestUpdate, new CommandInfo(OnUserRequestUpdateCommand)
        {
            HelpMessage = "Force update for current character.",
            ShowInHelp = false,
        });

        CommandManager.AddHandler(CommandDebugToggle, new CommandInfo((command, args) =>
        {
            Configuration.DebugEnabled = !Configuration.DebugEnabled;
            Configuration.Save();
            PrintToChat($"Debug mode is now {(Configuration.DebugEnabled ? "enabled" : "disabled")}.");
        })
        {
            HelpMessage = "Toggle debug mode.",
            ShowInHelp = false,
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

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandTopGil);
        CommandManager.RemoveHandler(CommandUserRequestUpdate);
        CommandManager.RemoveHandler(CommandDebugToggle);
        CommandManager.RemoveHandler(CommandDevModeToggle);

        // Unsubscribe from events
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        Condition.ConditionChange -= ConditionChange;

        //ConfigWindow.Dispose();
        //MainWindow.Dispose();

        _CharacterRetainerManager.Dispose();
    }



    internal class ConditionChangeStateData
    {
        internal bool IsSummoningBellClicked { get; set; } = false;
        internal long TotalGilStatusBefore { get; set; } = 0;
        internal long TotalGilStatusAfter { get; set; } = 0;
        internal long TotalGilStatusDiff { get { return TotalGilStatusAfter - TotalGilStatusBefore; } }
    }

    private ConditionChangeStateData conditionChangeHelper = new ConditionChangeStateData();



    /// <summary>
    /// Dalamud event - Condition change.
    /// This is used to detect if the player is at the retainer summoning bell.
    /// </summary>
    private void ConditionChange(ConditionFlag flag, bool value)
    {
        if (_isDevShowConditionChangesOn)
        {
            PrintToChat($"Condition change: {flag} - {value}");
            if (Targets.Target != null)
            {
                PrintToChat($" -->Target: {Targets.Target.Name}");
            }
        }

        // "Company Chest" is also considered a summoning bell that triggers the state ConditionFlag.OccupiedSummoningBell == True.
        // Using the player target to determine if the player is at the summoning bell or not.
        if (flag == ConditionFlag.OccupiedSummoningBell)
        {
            if (value) // Player clicked the summoning bell
            {
                conditionChangeHelper.IsSummoningBellClicked = IsTargetSummoningBell(); // Check if the player is actually at the summoning bell

                if (!conditionChangeHelper.IsSummoningBellClicked)
                {
                    DebuggerLog.Write($"Player clicked on the company chest - not the summoning bell.");
                    return; // Player probably clicked on the company chest
                }

                // Set current character
                _CharacterRetainerManager.SetCurrentCharacter();


                conditionChangeHelper.TotalGilStatusBefore = _CharacterRetainerManager.GetTotalGilCharacter(runUpdateFirst: false);
                DebuggerLog.Write($"Total Gil before: {Helper.FormatNumber(conditionChangeHelper.TotalGilStatusBefore)}");


                // Debug research, is Dalamud RetainerManager ready?
                bool isRMready = DalamudGameHelper.IsRetainerManagerReady();
                //PrintToChat($"RetainerManager ready: {isRMready}");
                DebuggerLog.Write($"Summoning bell enter - Is RetainerManager ready: {isRMready}");
            }
            else // Player left the summoning bell
            {
                if (!conditionChangeHelper.IsSummoningBellClicked)
                {
                    // Player probably clicked on the company chest
                    // Update player gil status, in case the player has withdrawn or deposited gil from the company chest.
                    _CharacterRetainerManager.UpdateCurrentCharacter();
                    return;
                }

                conditionChangeHelper.IsSummoningBellClicked = false;

                // --------->
                // Save the current character/retainer gil-balance
                // <---------
                uint savedCharacterGil = _CharacterRetainerManager.GetCurrentCharacter().Gil;
                List<GameRetainer> savedRetainersData = DeepCopyRetainers(_CharacterRetainerManager.GetCurrentCharacter().Retainers);
                if (DebuggerLog.IsDebugEnabled)
                {
                    foreach (GameRetainer retainer in savedRetainersData)
                    {
                        DebuggerLog.Write($"[Gil-check before] {retainer.Name} has {Helper.FormatNumber(retainer.Gil)} gil.");
                    }
                }

                // --------->
                // Update the current characters' retainers
                // <---------
                _CharacterRetainerManager.UpdateCurrentCharacter();



                // NEW: Check how much gil the current character has earned since the last visit at the summoning bell (not counting retainer sales)
                long xyz = (long)_CharacterRetainerManager.GetCurrentCharacter().Gil - savedCharacterGil;
                DebuggerLog.Write($"[Character only] {_CharacterRetainerManager.GetCurrentCharacter().Name}: {_CharacterRetainerManager.GetCurrentCharacter().Gil} - {savedCharacterGil} = {(long)_CharacterRetainerManager.GetCurrentCharacter().Gil - savedCharacterGil}");
                if (xyz > 0)
                {
                    //PrintToChat($"{_CharacterRetainerManager.GetCurrentCharacter().Name} earned {Helper.FormatNumber(savedCharacterGil - _CharacterRetainerManager.GetCurrentCharacter().Gil)} gil since the last bell visit.");
                    SeString characterEarnSinceLastVisit = new SeStringBuilder()
                        .AddUiForeground($"{_CharacterRetainerManager.GetCurrentCharacter().Name} ", (ushort)PluginColorValues.Blue)
                        .AddUiForeground("earned ", (ushort)PluginColorValues.Green)
                        .AddUiForeground($"{Helper.FormatNumber(xyz)} ", (ushort)PluginColorValues.Orange)
                        .AddUiForeground("gil since the last bell visit ", (ushort)PluginColorValues.Green)
                        .BuiltString;
                    PrintToChat(characterEarnSinceLastVisit);
                }



                conditionChangeHelper.TotalGilStatusAfter = _CharacterRetainerManager.GetTotalGilCharacter(runUpdateFirst: false);
                DebuggerLog.Write($"Total Gil after : {Helper.FormatNumber(conditionChangeHelper.TotalGilStatusAfter)}");

                string? earnedGilText = $"Earned {Helper.FormatNumber(conditionChangeHelper.TotalGilStatusDiff)} gil since the last bell visit";
                SeString earningsSinceLastVisit = new SeStringBuilder()
                    .AddUiForeground("Earned ", (ushort)PluginColorValues.Green)
                    .AddUiForeground($"{Helper.FormatNumber(conditionChangeHelper.TotalGilStatusDiff)} ", (ushort)PluginColorValues.Orange)
                    .AddUiForeground("gil since the last bell visit ", (ushort)PluginColorValues.Green)
                    .BuiltString;

                int numCharacters = _CharacterRetainerManager.GetTotalCharacters();

                if (conditionChangeHelper.TotalGilStatusDiff > 0)
                {
                    if (numCharacters > 1)
                    {
                        earnedGilText = earnedGilText + " on any characters.";

                        earningsSinceLastVisit.Append(
                            new SeStringBuilder()
                            .AddUiForeground($"on any {numCharacters} characters.", (ushort)PluginColorValues.Green)
                            .BuiltString);
                    }
                }
                else
                {
                    earnedGilText = null;
                    earningsSinceLastVisit = null;
                }


                // Print current characters' retainers that has earned any gil since last visit. savedCRData compare to Configuration.Characters
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
                    DebuggerLog.Write($"[Gil-check after] {retainer.Name} earned {Helper.FormatNumber(gilDiff)} gil since last visit. Now: {retainer.Gil}. Before: {savedRetainer.Gil}");

                    if (gilDiff > 0)
                    {
                        //PrintToChat($"{retainer.Name} earned {Helper.FormatNumber(gilDiff)} gil since last visit.");
                        SeString seStringRetainerEarn = new SeStringBuilder()
                            .AddUiForeground($"{retainer.Name} ", (ushort)PluginColorValues.Yellow)
                            .AddUiForeground($"earned ", (ushort)PluginColorValues.Green)
                            .AddUiForeground($"{Helper.FormatNumber(gilDiff)} ", (ushort)PluginColorValues.Orange)
                            .AddUiForeground($"gil since last visit.", (ushort)PluginColorValues.Green)
                            .BuiltString;
                        PrintToChat(seStringRetainerEarn);

                        DebuggerLog.Write($"[Gil-check after] {retainer.Name} earned {Helper.FormatNumber(gilDiff)} gil since last visit.");
                    }
                    else
                    {
                        DebuggerLog.Write($"[Gil-check after] {retainer.Name} did not earn any gil since last visit. Has {Helper.FormatNumber(retainer.Gil)} gil.");
                    }
                }

                // Print gil summary
                PrintGilSummary(earningsSinceLastVisit /*earnedGilText*/);
            }
        }
    }

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

    // Checks if the current character target is a summoning bell?
    private bool IsTargetSummoningBell()
    {
        return Targets.Target != null && Targets.Target.Name.ToString().Equals("Summoning Bell");
    }

    private void PrintGilSummary(SeString? optionalExtraText = null)
    {
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
                .AddUiForeground($"{Helper.FormatNumber(totalGil)}", (ushort)PluginColorValues.Orange)
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
            PrintToChat($"{gameCharacters.Name}: {Helper.FormatNumber(gameCharacters.Gil)}");
            DebuggerLog.Write($"[PrintGilSummary] {gameCharacters.Name}: {Helper.FormatNumber(gameCharacters.Gil)}");

            foreach (GameRetainer retainer in gameCharacters.Retainers)
            {
                doubleCheck += retainer.Gil;
                PrintToChat($" | {retainer.Name}: {Helper.FormatNumber(retainer.Gil)}");
                DebuggerLog.Write($"[PrintGilSummary]  | {retainer.Name}: {Helper.FormatNumber(retainer.Gil)}");
            }
        }

        DebuggerLog.Write($"[PrintGilSummary] Double check total: {Helper.FormatNumber(doubleCheck)}");
    }

    //private List<GameCharacter> SaveCharacterRetainerData()
    //{
    //    DebuggerLog.Write($"[SaveCharacterRetainerData] Saving character and retainer data.");

    //    List<GameCharacter> savedData = new List<GameCharacter>();

    //    foreach (GameCharacter gameCharacters in Configuration.Characters)
    //    {
    //        GameCharacter newCharacter = GameCharacter.CreateNew(gameCharacters.Name, gameCharacters.HomeWorldId, gameCharacters.Gil);
    //        savedData.Add( newCharacter );

    //        foreach (GameRetainer retainer in gameCharacters.Retainers)
    //        {
    //            GameRetainer newRetainer = new GameRetainer { Gil = retainer.Gil, Name = retainer.Name, OwnerCharacterId = newCharacter.UniqueId, RetainerId = retainer.RetainerId };
    //            newCharacter.Retainers.Add(newRetainer);
    //        }
    //    }

    //    return savedData;
    //}
    // ------------------------------------------------------------------------------------------------------


    /// <summary>
    /// Dalamud event - Login to game
    /// ... is this event being invoked correctly by Dalamud? Seems a bit wonky :(
    /// ... ClientState.LocalPlayer is sometimes null here, but not always!
    /// </summary>
    private void OnLogin()
    {
        //currentPlayerCharacterName = GetCurrentCharacterName();
        //DebuggerLog.Write($"Player logged in: {currentPlayerCharacterName}");
    }

    /// <summary>
    /// Dalamud event - Logout to game menu
    /// </summary>
    private void OnLogout(int type, int code)
    {
        //DebuggerLog.Write($"Player logged out: {currentPlayerCharacterName}");

        _CharacterRetainerManager.ResetCurrentCharacter();
    }


    // ------------------------------------------------------------------------------------------------------


    private void OnUserRequestUpdateCommand(string command, string args)
    {
        PrintToChat($"Player requested update for current character {_CharacterRetainerManager.GetCurrentCharacterName()}");

        // Update the current character
        Framework.RunOnTick(() =>
        {
            _CharacterRetainerManager.UpdateCurrentCharacter();
        });
    }

    // Chat command: /topgil (optional arguments: details)
    private void OnTopGilCommand(string command, string args)
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

    // ------------------------------------------------------------------------------------------------------

    private bool _isDevShowConditionChangesOn = false;

    // Chat command: /tgdev [command]
    private void OnDevModeToggleCommand(string command, string args)
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

    // ------------------------------------------------------------------------------------------------------

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();

    // ------------------------------------------------------------------------------------------------------



    // ------------------------------------------------------------------------------------------------------


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

    /// <summary>
    /// 540 = Orange
    /// 541 = Purple
    /// 542 = Blue
    /// 543 = Blue darker
    /// 43 = Green lighter
    /// 44 = Green darker
    /// </summary>
    /// <param name="success"></param>
    /// <returns></returns>
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
}


