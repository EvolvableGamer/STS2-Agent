#if STS2_PUBLIC_23811903
using System.Text.Json;
using Godot;
using Environment = System.Environment;
using FileAccess = System.IO.FileAccess;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Timeline;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using STS2AIAgent.Server;

namespace STS2AIAgent.Game;

internal static partial class GameControl
{
    private static GameStartCommandStore Commands => new(ProjectSettings.GlobalizePath(SaveManager.Instance.GetProfileScopedPath("game-control-commands")));
    public static object UnlockCustom()
    {
        if (!Enabled || Environment.GetEnvironmentVariable("STS2_GAME_ALLOW_EPOCH_UNLOCK") != "1" ||
            ActiveScreenContext.Instance.GetCurrentScreen() is not NMainMenu)
            throw new ApiException(403, "isolated_unlock_required", "Explicit isolated menu unlock required.");
        if (SaveManager.Instance.IsEpochRevealed<CustomAndSeedsEpoch>()) return Info();
        var path = Path.GetFullPath(ProjectSettings.GlobalizePath(SaveManager.Instance.GetProfileScopedPath("saves/progress.save")));
        var clientId = Environment.GetEnvironmentVariable("STS2_GAME_ISOLATED_CLIENT_ID")!;
        if (!path.Split(Path.DirectorySeparatorChar).Contains(clientId) || !File.Exists(path))
            throw new ApiException(403, "isolated_save_required", "Existing isolated progress file required.");
        // Never replace a prior backup. A failed backup prevents any native mutation.
        File.Copy(path, path + ".before-custom", overwrite: false);
        using (var backup = new FileStream(path + ".before-custom", FileMode.Open, FileAccess.ReadWrite)) backup.Flush(true);
        SaveManager.Instance.ObtainEpochOverride(EpochModel.GetId<CustomAndSeedsEpoch>(), EpochState.Revealed);
        SaveManager.Instance.SaveProgressFile();
        return Info();
    }
    public static async Task<object> OpenCustom()
    {
        if (!Enabled) throw new ApiException(403, "game_control_required", "Isolated control required.");
        if (ActiveScreenContext.Instance.GetCurrentScreen() is NCustomRunScreen) return Info();
        if (ActiveScreenContext.Instance.GetCurrentScreen() is not NMainMenu menu ||
            !SaveManager.Instance.IsEpochRevealed<CustomAndSeedsEpoch>())
            throw new ApiException(409, "custom_menu_required", "Unlocked Custom from main menu required.");
        var screen = menu.SubmenuStack.GetSubmenuType<NCustomRunScreen>()
            ?? throw new ApiException(503, "state_unavailable", "Native Custom screen unavailable.");
        screen.InitializeSingleplayer();
        menu.SubmenuStack.Push(screen);
        for (var i = 0; i < 300 && ActiveScreenContext.Instance.GetCurrentScreen() is not NCustomRunScreen; i++)
            await GameThread.WaitForNextFrameAsync();
        if (ActiveScreenContext.Instance.GetCurrentScreen() is not NCustomRunScreen)
            throw new ApiException(409, "custom_transition_unknown", "Custom menu transition incomplete.");
        return Info();
    }
    public static object QueryStart(string id) => Commands.Read(id, InstanceId) ?? (object)new { outcome = "unknown", instance_id = InstanceId };
    public static async Task<object> StartCustom(JsonElement input)
    {
        var id = input.GetProperty("command_id").GetString() ?? "";
        var store = Commands;
        var prior = store.Read(id, InstanceId);
        if (prior != null) return prior;
        // Existing intent is never re-dispatched, including after a process restart.
        if (store.HasIntent(id)) return new { outcome = "unknown", instance_id = InstanceId };
        if (!Enabled || ActiveScreenContext.Instance.GetCurrentScreen() is not NCustomRunScreen screen ||
            screen.Lobby.Players.Count != 1 || screen.Lobby.Modifiers.Count != 0)
            throw new ApiException(409, "custom_lobby_required", "Single-player unmodified Custom required.");
        var expectedSeed = screen.Lobby.Seed;
        var expectedCharacter = screen.Lobby.LocalPlayer.character.Id.Entry;
        var expectedAscension = screen.Lobby.Ascension;
        var button = screen.GetNodeOrNull<NConfirmButton>("ConfirmButton");
        if (button?.IsEnabled != true) throw new ApiException(409, "custom_not_ready", "Custom confirm button not ready.");
        if (!store.Begin(id, InstanceId)) return new { outcome = "unknown", instance_id = InstanceId };
        button.EmitSignal(NClickableControl.SignalName.Released, button);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await GameThread.WaitForNextFrameAsync();
            var actual = JsonSerializer.SerializeToElement(Info());
            if (actual.GetProperty("active_run").GetBoolean() && actual.GetProperty("mode").GetString() == "custom" &&
                actual.GetProperty("seed").GetString() == expectedSeed && actual.GetProperty("character_id").GetString() == expectedCharacter &&
                actual.GetProperty("ascension").GetInt32() == expectedAscension && actual.GetProperty("modifiers").GetArrayLength() == 0 &&
                GameStateService.BuildStatePayload().screen is "EVENT" or "MAP" or "COMBAT")
            {
                var result = new { outcome = "executed", instance_id = InstanceId, actual_conditions = actual };
                store.Complete(id, result);
                return result;
            }
        }
        return new { outcome = "unknown", instance_id = InstanceId };
    }
}
#endif
