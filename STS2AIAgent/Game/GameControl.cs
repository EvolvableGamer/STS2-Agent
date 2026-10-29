#if STS2_PUBLIC_23811903
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using STS2AIAgent.Server;

namespace STS2AIAgent.Game;

// Game primitives only. No Arena task, participant, profile label, seed selection or run orchestration.
internal static partial class GameControl
{
    private static UnlockState? _requestedUnlocks;
    private static readonly string Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static readonly string InstanceId = Guid.NewGuid().ToString("N");
    public static bool Enabled
    {
        get
        {
            var args = Godot.OS.GetCmdlineArgs();
            var index = Array.IndexOf(args, "--clientId");
            var expected = Environment.GetEnvironmentVariable("STS2_GAME_ISOLATED_CLIENT_ID");
            return expected != null && expected.Length is >= 2 and <= 20 && expected.All(char.IsAsciiDigit)
                && index >= 0 && index + 1 < args.Length && args[index + 1] == expected
                && Environment.GetEnvironmentVariable("STS2_GAME_CONTROL_TOKEN")?.Length == 64;
        }
    }
    public static bool Authorized(string? token)
    {
        var expected = Environment.GetEnvironmentVariable("STS2_GAME_CONTROL_TOKEN");
        return Enabled && token?.Length == 64 && expected != null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token));
    }
    public static void Install()
    {
        if (!Enabled) return;
        var method = ReflectedGameMembers.Method(typeof(SaveManager), "GenerateUnlockStateFromProgress")
            ?? throw new MissingMethodException("SaveManager.GenerateUnlockStateFromProgress");
        new Harmony("sts2.isolated-game-settings").Patch(method,
            postfix: new HarmonyMethod(typeof(GameControl), nameof(ApplyRequestedUnlocks)));
    }
    private static void ApplyRequestedUnlocks(ref UnlockState __result)
    {
        if (Enabled && _requestedUnlocks != null) __result = _requestedUnlocks;
    }
    public static object Catalog() => new {
        act_ids = ModelDb.ActsByIndex.SelectMany(x => x).Select(a => a.Id.Entry).Distinct().OrderBy(x => x).ToArray(),
        character_ids = ModelDb.AllCharacters.Select(c => c.Id.Entry).OrderBy(x => x).ToArray(),
        cards = UnlockState.all.Cards.Count(), relics = UnlockState.all.Relics.Count(), potions = UnlockState.all.Potions.Count()
    };
    public static object Info()
    {
        var state = GameStateService.BuildStatePayload();
        var run = RunManager.Instance.DebugOnlyGetState();
        var screen = ActiveScreenContext.Instance.GetCurrentScreen() as NCustomRunScreen;
        var player = run?.Players.FirstOrDefault();
        var unlocks = player?.UnlockState ?? _requestedUnlocks ?? new UnlockState(SaveManager.Instance.Progress);
        return new {
            protocol = "sts2-game-control/1", instance_id = InstanceId, game_build = "23811903",
            game_version = "v0.107.1", screen = screen != null ? "CUSTOM_RUN" : state.screen, active_run = run != null,
            has_saved_run = SaveManager.Instance.HasRunSave || SaveManager.Instance.HasMultiplayerRunSave,
            seed = run?.Rng.StringSeed ?? screen?.Lobby.Seed,
            character_id = player?.Character.Id.Entry ?? screen?.Lobby.LocalPlayer.character.Id.Entry,
            ascension = run?.AscensionLevel ?? screen?.Lobby.Ascension,
            mode = (run?.GameMode ?? screen?.Lobby.GameMode)?.ToString().ToLowerInvariant() ?? "standard",
            modifiers = (run?.Modifiers ?? screen?.Lobby.Modifiers)?.Select(m => m.Id.Entry).ToArray() ?? Array.Empty<string>(),
            custom_unlocked = SaveManager.Instance.IsEpochRevealed<MegaCrit.Sts2.Core.Timeline.Epochs.CustomAndSeedsEpoch>(),
            fully_unlocked = unlocks.NumberOfRuns == UnlockState.all.NumberOfRuns && unlocks.Cards.Count() == UnlockState.all.Cards.Count(),
            unlocked_cards = unlocks.Cards.Count(), unlocked_characters = unlocks.Characters.Count(),
            discovered_act_ids = SaveManager.Instance.Progress.DiscoveredActs.Select(id => id.Entry).OrderBy(x => x).ToArray()
        };
    }
    public static object Set(JsonElement input)
    {
        if (!Enabled || input.ValueKind != JsonValueKind.Object || !input.TryGetProperty("name", out var n)
            || n.ValueKind != JsonValueKind.String || !input.TryGetProperty("value", out var value))
            throw new ApiException(400, "invalid_setting", "A named game setting is required.");
        if (ActiveScreenContext.Instance.GetCurrentScreen() is not NCustomRunScreen screen || screen.Lobby.Players.Count != 1)
            throw new ApiException(409, "setting_requires_lobby", "Set game conditions in single-player Custom only.");
        var lobby = screen.Lobby;
        switch (n.GetString())
        {
            case "seed":
                var seed = value.GetString() ?? "";
                if (seed.Length is < 1 or > 100 || seed.Any(c => !char.IsAsciiLetterOrDigit(c)))
                    throw new ApiException(400, "invalid_setting", "Expected a bounded alphanumeric seed.");
                lobby.SetSeed(seed); // Custom supports the native SeedChanged callback.
                break;
            case "character":
                var character = ModelDb.AllCharacters.FirstOrDefault(c => c.Id.Entry == value.GetString())
                    ?? throw new ApiException(400, "invalid_setting", "Unknown character.");
                lobby.SetLocalCharacter(character);
                break;
            case "ascension":
                var ascension = value.GetInt32();
                if (ascension != 0) throw new ApiException(400, "unsupported_setting", "This compatibility profile validates A0 only.");
                lobby.SyncAscensionChange(ascension);
                break;
            case "unlock_all":
                _requestedUnlocks = value.GetBoolean() ? UnlockState.all : new UnlockState(SaveManager.Instance.Progress);
                var local = lobby.Players[0]; local.unlockState = _requestedUnlocks.ToSerializable(); lobby.Players[0] = local;
                break;
            case "discover_act":
                var act = ModelDb.ActsByIndex.SelectMany(x => x).FirstOrDefault(a => a.Id.Entry == value.GetString())
                    ?? throw new ApiException(400, "invalid_setting", "Unknown act.");
                SaveManager.Instance.Progress.MarkActAsSeen(act.Id);
                break;
            default: throw new ApiException(400, "unsupported_setting", "Unsupported game setting.");
        }
        return Info();
    }
    public static string? TokenFor(GameStatePayload state) => !Enabled ? null : Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(Nonce + JsonSerializer.Serialize(state.agent_view))));
    public static void ValidateExpected(object? context)
    {
        if (!Enabled || context is not JsonElement e || !e.TryGetProperty("game_state_token", out var token)) return;
        var current = GameStateService.BuildStatePayload().game_state_token;
        if (token.ValueKind != JsonValueKind.String || token.GetString() != current)
            throw new ApiException(409, "stale_observation", "Game state changed before dispatch; no action was executed.");
    }
}
#endif
