using Godot;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.Debug.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;
using MegaCrit.Sts2.Core.Nodes.Screens.Timeline.UnlockScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Timeline;
using STS2AIAgent.Agent;
using STS2AIAgent.Config;
using STS2AIAgent.Localization;
using STS2AIAgent.Multiplayer;
using STS2AIAgent.Server;

namespace STS2AIAgent.Game;

/// <summary>
/// Profile switching, main-menu submenus, modal confirm/dismiss and the debug console.
/// </summary>
/// <remarks>
/// Part of <see cref="GameActionService"/>, split out of <c>GameActionService.cs</c> on
/// 2026-09-17. Every handler here follows the one pattern the whole service follows: validate
/// the screen, act on the game thread, wait for the transition with a deadline. The file
/// boundary is not a design -- which members live here was computed, by asking which ones
/// nothing outside this room references. Anything two rooms both reach stayed in the base file.
/// </remarks>
internal static partial class GameActionService
{
    internal static object ReadArenaUnlockTestChecks()
    {
        var save=SaveManager.Instance;
        var profileOk=save.IsProfileInitialized&&save.CurrentProfileId==1;
        var optInOk=System.Environment.GetEnvironmentVariable("ARENA_ENABLE_TEST_UNLOCK")=="2026102901";
        var current=ActiveScreenContext.Instance.GetCurrentScreen();
        var menuOk=current is NMainMenu or NSingleplayerSubmenu;
        var pathOk=false;
        if(profileOk) {
            var actual=save.GetProfileScopedPath(System.IO.Path.Combine(UserDataPathProvider.SavesDir,"progress.save"));
            var expected=ProjectSettings.GlobalizePath("user://default/2026102901/modded/profile1/saves/progress.save");
            var actualFull=actual.StartsWith("user://",StringComparison.Ordinal)?ProjectSettings.GlobalizePath(actual):actual;
            pathOk=string.Equals(System.IO.Path.GetFullPath(actualFull),System.IO.Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase);
        }
        return new {opt_in_ok=optInOk,profile_ok=profileOk,main_menu_ok=menuOk,path_ok=pathOk};
    }
    internal static object UnlockArenaCustomForTest()
    {
        using var lease=ActionExecutionGate.Shared.Acquire("arena_test_unlock_custom");
        var save=SaveManager.Instance;
        var current=ActiveScreenContext.Instance.GetCurrentScreen();
        if(!save.IsProfileInitialized)throw new ApiException(403,"isolated_profile_required","Isolated profile is not initialized.");
        var path=save.GetProfileScopedPath(System.IO.Path.Combine(UserDataPathProvider.SavesDir,"progress.save"));
        var expectedPath=ProjectSettings.GlobalizePath("user://default/2026102901/modded/profile1/saves/progress.save");
        var fullActual=path.StartsWith("user://",StringComparison.Ordinal)?ProjectSettings.GlobalizePath(path):path;
        var pathMatches=string.Equals(System.IO.Path.GetFullPath(fullActual),System.IO.Path.GetFullPath(expectedPath),StringComparison.OrdinalIgnoreCase);
        if(!ArenaUnlockPolicy.Allows(System.Environment.GetEnvironmentVariable("ARENA_ENABLE_TEST_UNLOCK"),save.CurrentProfileId,pathMatches?"user://default/2026102901/modded/profile1/saves/progress.save":"",current is NMainMenu or NSingleplayerSubmenu))
            throw new ApiException(403,"isolated_profile_required","Explicit isolated test profile opt-in is required.");
        const string epoch="CUSTOM_AND_SEEDS_EPOCH";
        if(save.IsEpochRevealed(epoch))return new {epoch,revealed=true,changed=false};
        var fullPath=fullActual;
        var backup=fullPath+".arena-test-backup";
        if(!System.IO.File.Exists(fullPath))throw new ApiException(409,"progress_missing","Isolated progress save not found.");
        if(System.IO.File.Exists(backup))throw new ApiException(409,"backup_exists","Existing test backup must be preserved.");
        System.IO.File.Copy(fullPath,backup,false);
        save.ObtainEpochOverride(epoch,EpochState.Revealed);
        save.SaveProgressFile();
        using var doc=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(fullPath));
        var persisted=doc.RootElement.GetProperty("epochs").EnumerateArray().Any(e=>e.GetProperty("id").GetString()==epoch&&e.GetProperty("state").GetString()=="revealed");
        if(!save.IsEpochRevealed(epoch)||!persisted)throw new ApiException(503,"save_unverified","Unlock not verified on disk; restore backup and restart.");
        return new {epoch,revealed=true,changed=true,backup="progress.save.arena-test-backup"};
    }
    internal static async Task<ArenaCustomSetup> OpenArenaCustomAsync()
    {
        using var lease=ActionExecutionGate.Shared.Acquire("arena_open_custom");
        var current=ActiveScreenContext.Instance.GetCurrentScreen();
        if(current is NCustomRunScreen)return ReadArenaCustomSetup();
        NSingleplayerSubmenu? submenu=current as NSingleplayerSubmenu;
        if(current is NMainMenu main) {
            submenu=main.SubmenuStack.GetSubmenuType<NSingleplayerSubmenu>();
            if(submenu==null)throw new ApiException(503,"state_unavailable","Singleplayer submenu unavailable.");
            main.SubmenuStack.Push(submenu);
            if(!await WaitForMainMenuSubmenuOpenAsync<NSingleplayerSubmenu>(main,TimeSpan.FromSeconds(5)))
                throw new ApiException(503,"setup_unknown","Singleplayer submenu did not open.");
        }
        if(submenu==null)throw new ApiException(409,"invalid_action","Main menu or singleplayer submenu required.");
        var button=ReflectedGameMembers.Field(typeof(NSingleplayerSubmenu),"_customButton")?.GetValue(submenu) as NButton;
        if(button==null||!GodotObject.IsInstanceValid(button)||!button.IsVisibleInTree()||!button.IsEnabled)
            throw new ApiException(409,"custom_unavailable","Custom mode is unavailable or locked.");
        button.ForceClick();
        var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(10);
        while(DateTime.UtcNow<deadline) {
            await WaitForNextFrameAsync();
            if(ActiveScreenContext.Instance.GetCurrentScreen() is NCustomRunScreen)return ReadArenaCustomSetup();
        }
        throw new ApiException(503,"setup_unknown","Custom screen did not become available.");
    }
    private static IEnumerable<NCharacterSelectButton> ArenaCustomButtons(Node root)
    {
        foreach(var child in root.GetChildren()) {
            if(child is NCharacterSelectButton button)yield return button;
            foreach(var nested in ArenaCustomButtons(child))yield return nested;
        }
    }
    internal static async Task<ArenaCustomSetup> ConfigureArenaCustomAsync(string characterId,int ascension)
    {
        using var lease=ActionExecutionGate.Shared.Acquire("arena_configure_custom");
        if(ActiveScreenContext.Instance.GetCurrentScreen() is not NCustomRunScreen screen)
            throw new ApiException(409,"invalid_action","Custom-run screen required.");
        var initial=ReadArenaCustomSetup();
        if(initial.ascension!=ascension)throw new ApiException(409,"ascension_unavailable","This setup can only use the currently selected ascension.");
        if(initial.character_id==characterId)return initial;
        var button=ArenaCustomButtons(screen).FirstOrDefault(x=>GodotObject.IsInstanceValid(x)&&x.Character.Id.Entry==characterId&&!x.IsLocked&&x.IsEnabled&&x.IsVisibleInTree());
        if(button==null)throw new ApiException(409,"character_unavailable","Requested character is unavailable or locked.");
        button.Select();
        var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(5);
        while(DateTime.UtcNow<deadline) {
            await WaitForNextFrameAsync();
            var actual=ReadArenaCustomSetup();
            if(actual.character_id==characterId&&actual.ascension==ascension)return actual;
        }
        throw new ApiException(503,"setup_unknown","Character selection did not settle.");
    }
    internal static ArenaCustomSetup ReadArenaCustomSetup()
    {
        if (ActiveScreenContext.Instance.GetCurrentScreen() is not NCustomRunScreen screen || screen.Lobby.NetService.Type.IsMultiplayer() || screen.Lobby.GameMode != GameMode.Custom)
            throw new ApiException(409, "invalid_action", "Native singleplayer custom-run screen required.");
        var lobby=screen.Lobby;
        return new ArenaCustomSetup("custom", lobby.Seed, lobby.LocalPlayer.character?.Id.Entry, lobby.Ascension, lobby.Modifiers.Select(x=>x.Id.Entry).ToArray(), SaveManager.Instance.CurrentProfileId);
    }
    internal static async Task<GameStatePayload> StartArenaCustomAsync(string seed)
    {
        using var lease=ActionExecutionGate.Shared.Acquire("arena_start_task");
        if (ActiveScreenContext.Instance.GetCurrentScreen() is not NCustomRunScreen screen || screen.Lobby.GameMode != GameMode.Custom || screen.Lobby.NetService.Type.IsMultiplayer())
            throw new ApiException(409,"invalid_action","Native singleplayer custom-run screen required.");
        var field=ReflectedGameMembers.Field(typeof(NCustomRunScreen),"_confirmButton");
        if(field?.GetValue(screen) is not NButton button || !GodotObject.IsInstanceValid(button) || !button.IsVisibleInTree() || !button.IsEnabled)
            throw new ApiException(409,"start_unavailable","Custom-run start button is unavailable.");
        screen.Lobby.SetSeed(seed);
        if(screen.Lobby.Seed!=seed)throw new ApiException(409,"seed_mismatch","Game did not accept the requested seed.");
        button.ForceClick();
        var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(15);
        while(DateTime.UtcNow<deadline) {
            await WaitForNextFrameAsync();
            var state=GameStateService.BuildStatePayload();
            if(state.run_id==seed && state.run!=null)return state;
        }
        throw new ApiException(503,"start_unknown","Run start did not produce a matching game state before the deadline.");
    }
    internal static string PrepareArenaSeed(string seed)
    {
        using var lease = ActionExecutionGate.Shared.Acquire("arena_prepare_seed");
        var current = ActiveScreenContext.Instance.GetCurrentScreen();
        if (current is not NCustomRunScreen screen || screen.Lobby.NetService.Type.IsMultiplayer() || screen.Lobby.GameMode != GameMode.Custom)
            throw new ApiException(409, "invalid_action", "Seed preparation requires the native singleplayer custom-run screen.");
        screen.Lobby.SetSeed(seed);
        return screen.Lobby.Seed ?? throw new ApiException(503, "seed_unavailable", "Lobby did not return a seed after setting it.");
    }
    private static async Task<ActionResponsePayload> ExecuteCloseMainMenuSubmenuAsync()
    {
        var currentScreen = ActiveScreenContext.Instance.GetCurrentScreen();
        var screen = GameStateService.ResolveScreen(currentScreen);

        if (!GameStateService.CanCloseMainMenuSubmenu(currentScreen))
        {
            throw new ApiException(409, "invalid_action", "Action is not available in the current state.", new
            {
                action = "close_main_menu_submenu",
                screen
            });
        }

        bool stable;
        if (currentScreen is NPatchNotesScreen patchNotes)
        {
            var backButton = ReflectedGameMembers.Field(typeof(NPatchNotesScreen), "_backButton")?.GetValue(patchNotes) as NButton;
            if (backButton != null &&
                GodotObject.IsInstanceValid(backButton) &&
                backButton.IsVisibleInTree() &&
                backButton.IsEnabled)
            {
                backButton.ForceClick();
            }
            else
            {
                ((Node)patchNotes).Call(NPatchNotesScreen.MethodName.Close);
            }

            stable = await WaitForPatchNotesCloseAsync(patchNotes, TimeSpan.FromSeconds(10));
        }
        else if (currentScreen is NCapstoneSubmenuStack capstonePageContainer &&
            GameStateService.GetClosableCapstonePage(capstonePageContainer) is { } capstonePage)
        {
            // The page is left the way its own BackButton leaves it: the game wires every submenu's back
            // button to Stack.Pop(), which closes this page and shows the page below it again. Popping a
            // page above the pause menu therefore cannot resume the run -- the pause menu is still on top.
            var capstoneStack = capstonePageContainer.Stack
                ?? throw new ApiException(503, "state_unavailable", "Capstone submenu stack is unavailable.", new
                {
                    action = "close_main_menu_submenu",
                    screen
                }, retryable: true);

            capstoneStack.Pop();
            stable = await WaitForCapstonePageCloseAsync(capstoneStack, capstonePage, TimeSpan.FromSeconds(10));
        }
        else
        {
            if (currentScreen is not NSubmenu submenu)
            {
                throw new ApiException(409, "invalid_action", "Action is not available in the current state.", new
                {
                    action = "close_main_menu_submenu",
                    screen
                });
            }

            var submenuStack = GameStateService.GetSubmenuStack(submenu)
                ?? throw new ApiException(503, "state_unavailable", "Main menu submenu stack is unavailable.", new
                {
                    action = "close_main_menu_submenu",
                    screen
                }, retryable: true);

            submenuStack.Pop();
            stable = await WaitForMainMenuSubmenuCloseAsync(submenuStack, submenu, TimeSpan.FromSeconds(10));
        }

        return new ActionResponsePayload
        {
            action = "close_main_menu_submenu",
            status = stable ? "completed" : "pending",
            stable = stable,
            message = stable ? "Action completed." : "Action queued but state is still transitioning.",
            state = GameStateService.BuildStatePayload()
        };
    }

    private static async Task<ActionResponsePayload> ExecuteSwitchProfileAsync(ActionRequest request)
    {
        var currentScreen = ActiveScreenContext.Instance.GetCurrentScreen();
        var screen = GameStateService.ResolveScreen(currentScreen);

        if (!GameStateService.CanSwitchProfile(currentScreen))
        {
            throw new ApiException(409, "invalid_action", "Action is not available in the current state.", new
            {
                action = "switch_profile",
                screen
            });
        }

        if (request.option_index is not int profileId || profileId is < 1 or > 3)
        {
            throw new ApiException(400, "invalid_request", "switch_profile requires option_index in the range 1..3.", new
            {
                action = "switch_profile"
            });
        }

        var game = NGame.Instance;
        if (game == null || !GodotObject.IsInstanceValid(game))
        {
            throw new ApiException(503, "state_unavailable", "Game instance is unavailable.", new
            {
                action = "switch_profile",
                screen
            }, retryable: true);
        }

        NMainMenu? initialMainMenu = null;
        if (SaveManager.Instance.CurrentProfileId != profileId)
        {
            initialMainMenu = currentScreen as NMainMenu;
            SaveManager.Instance.SwitchProfileId(profileId);
            var prefsReadResult = SaveManager.Instance.InitPrefsData();
            var progressReadResult = SaveManager.Instance.InitProgressData();
            game.ReloadMainMenu();
            game.CheckShowSaveFileError(
                progressReadResult,
                prefsReadResult,
                new ReadSaveResult<SettingsSave>(new SettingsSave()));
        }

        var stable = await WaitForProfileSwitchAsync(initialMainMenu, profileId, TimeSpan.FromSeconds(15));
        return new ActionResponsePayload
        {
            action = "switch_profile",
            status = stable ? "completed" : "pending",
            stable = stable,
            message = stable ? "Action completed." : "Action queued but state is still transitioning.",
            state = GameStateService.BuildStatePayload()
        };
    }

    private static async Task<bool> WaitForProfileSwitchAsync(
        NMainMenu? initialMainMenu,
        int profileId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await WaitForNextFrameAsync();
            var currentScreen = ActiveScreenContext.Instance.GetCurrentScreen();
            if (SaveManager.Instance.CurrentProfileId == profileId &&
                currentScreen is NMainMenu mainMenu &&
                (initialMainMenu == null || mainMenu != initialMainMenu) &&
                GodotObject.IsInstanceValid(mainMenu) &&
                mainMenu.IsInsideTree() &&
                mainMenu.IsVisibleInTree() &&
                mainMenu.SubmenuStack?.SubmenusOpen != true)
            {
                return true;
            }
        }

        return false;
    }

    private static Task<ActionResponsePayload> ExecuteInjectEventChurnAsync(ActionRequest request)
    {
        if (!AreDebugActionsEnabled())
        {
            throw new ApiException(409, "invalid_action", "inject_event_churn is disabled. Set STS2_ENABLE_DEBUG_ACTIONS=1 for development use.", new
            {
                action = "inject_event_churn"
            });
        }

        if (!EventChurnPolicy.TryResolveCount(request.option_index, out var count, out var error))
        {
            throw new ApiException(400, "invalid_request", error ?? "count is invalid.", new
            {
                action = "inject_event_churn"
            });
        }

        // Deliberately not a state mutation: the releases exist to push a subscriber's bounded queue
        // past its capacity, so a live run can observe the slow-subscriber contract end to end. One
        // state build serves both the events and the response instead of paying for two.
        var state = GameStateService.BuildStatePayload();
        var published = GameEventService.Instance.PublishDebugChurn(state, count);
        return Task.FromResult(new ActionResponsePayload
        {
            action = "inject_event_churn",
            status = "completed",
            stable = true,
            message = $"Published {published} synthetic {EventChurnPolicy.EventType} event(s).",
            state = state
        });
    }

    private static Task<ActionResponsePayload> ExecuteRunConsoleCommandAsync(ActionRequest request)
    {
        if (!AreDebugActionsEnabled())
        {
            throw new ApiException(409, "invalid_action", "run_console_command is disabled. Set STS2_ENABLE_DEBUG_ACTIONS=1 for development use.", new
            {
                action = "run_console_command"
            });
        }

        return ExecuteConsoleCommandCoreAsync(request.command);
    }

    private static bool AreDebugActionsEnabled()
    {
        var raw = ReadEnvironmentVariable("STS2_ENABLE_DEBUG_ACTIONS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();

        return raw.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadEnvironmentVariable(string name)
    {
        var processValue = System.Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return processValue;
        }

        try
        {
            var godotValue = OS.GetEnvironment(name);
            if (!string.IsNullOrWhiteSpace(godotValue))
            {
                return godotValue;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[STS2AIAgent] reading environment variable {name} through Godot failed: {ex}");
        }

        var userValue = System.Environment.GetEnvironmentVariable(name, System.EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(userValue))
        {
            return userValue;
        }

        return System.Environment.GetEnvironmentVariable(name, System.EnvironmentVariableTarget.Machine);
    }

    private static async Task<ActionResponsePayload> ExecuteConfirmModalAsync()
    {
        return await ExecuteModalButtonAsync("confirm_modal", GameStateService.GetModalConfirmButton);
    }

    private static async Task<ActionResponsePayload> ExecuteDismissModalAsync()
    {
        return await ExecuteModalButtonAsync("dismiss_modal", GameStateService.GetModalCancelButton);
    }

    private static async Task<bool> WaitForMainMenuSubmenuCloseAsync(
        NSubmenuStack submenuStack,
        NSubmenu submenu,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await WaitForNextFrameAsync();

            var currentScreen = ActiveScreenContext.Instance.GetCurrentScreen();
            if (!ReferenceEquals(currentScreen, submenu) || !submenuStack.SubmenusOpen)
            {
                return true;
            }
        }

        var finalScreen = ActiveScreenContext.Instance.GetCurrentScreen();
        return !ReferenceEquals(finalScreen, submenu) || !submenuStack.SubmenusOpen;
    }

    /// <summary>
    /// A capstone container page is closed when the container's own stack no longer holds it. The screen
    /// context never changes here: the container stays up with the page below it (the pause menu) and only
    /// closes itself once the stack runs empty, so waiting on the screen would report settled too early.
    /// </summary>
    private static bool IsCapstonePageClosed(NSubmenuStack stack, NSubmenu page)
    {
        return !GodotObject.IsInstanceValid(page) || !ReferenceEquals(stack.Peek(), page);
    }

    private static async Task<bool> WaitForCapstonePageCloseAsync(
        NSubmenuStack stack,
        NSubmenu page,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await WaitForNextFrameAsync();

            if (IsCapstonePageClosed(stack, page))
            {
                return true;
            }
        }

        return IsCapstonePageClosed(stack, page);
    }

    /// <summary>
    /// Patch notes are a main-menu submenu that is not an <see cref="NSubmenu"/>: closing it tween-fades
    /// the screen and then hides it, so "closed" means the screen is hidden or is no longer current.
    /// </summary>
    private static async Task<bool> WaitForPatchNotesCloseAsync(NPatchNotesScreen patchNotes, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await WaitForNextFrameAsync();

            if (IsPatchNotesClosed(patchNotes))
            {
                return true;
            }
        }

        return IsPatchNotesClosed(patchNotes);
    }

    private static bool IsPatchNotesClosed(NPatchNotesScreen patchNotes)
    {
        return !GodotObject.IsInstanceValid(patchNotes) ||
            !patchNotes.IsVisibleInTree() ||
            !ReferenceEquals(ActiveScreenContext.Instance.GetCurrentScreen(), patchNotes);
    }

    private static async Task<ActionResponsePayload> ExecuteModalButtonAsync(
        string actionName,
        Func<IScreenContext?, NButton?> buttonResolver)
    {
        var currentScreen = ActiveScreenContext.Instance.GetCurrentScreen();
        var screen = GameStateService.ResolveScreen(currentScreen);
        var previousModal = GameStateService.GetOpenModal();
        var button = buttonResolver(currentScreen);

        if (previousModal == null)
        {
            throw new ApiException(409, "invalid_action", "Action is not available in the current state.", new
            {
                action = actionName,
                screen
            });
        }

        if (button != null)
        {
            button.ForceClick();
        }
        else if (actionName == "confirm_modal" &&
                 FtueModalPolicy.CloseFtueDirectly(previousModal.GetType().Name, hasUsableConfirmButton: false) &&
                 GameStateService.TryCloseOpenFtue())
        {
        }
        else
        {
            throw new ApiException(409, "invalid_action", "Action is not available in the current state.", new
            {
                action = actionName,
                screen
            });
        }

        var stable = await WaitForModalTransitionAsync(previousModal, TimeSpan.FromSeconds(2));
        if (!stable &&
            actionName == "confirm_modal" &&
            FtueModalPolicy.ForceCloseIfStuck(previousModal.GetType().Name))
        {
            GameStateService.TryCloseOpenFtue();
            stable = await WaitForModalTransitionAsync(previousModal, TimeSpan.FromSeconds(8));
        }

        var message = "Action completed.";
        if (!stable)
        {
            message = FtueModalPolicy.IsMultiPageFtue(previousModal.GetType().Name)
                ? "Tutorial page advanced; the modal is still open. Call confirm_modal again."
                : "Action queued but state is still transitioning.";
        }

        return new ActionResponsePayload
        {
            action = actionName,
            status = stable ? "completed" : "pending",
            stable = stable,
            message = message,
            state = GameStateService.BuildStatePayload()
        };
    }

    private static async Task<bool> WaitForModalTransitionAsync(IScreenContext previousModal, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await WaitForNextFrameAsync();

            var currentModal = GameStateService.GetOpenModal();
            if (currentModal == null || !ReferenceEquals(currentModal, previousModal))
            {
                return true;
            }
        }

        var finalModal = GameStateService.GetOpenModal();
        return finalModal == null || !ReferenceEquals(finalModal, previousModal);
    }
}
