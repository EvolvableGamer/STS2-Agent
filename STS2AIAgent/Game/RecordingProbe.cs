#if STS2_RECORDING_PROBE
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using Godot;
using FileAccess = System.IO.FileAccess;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using STS2AIAgent.Server;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace STS2AIAgent.Game;

// Private diagnostic only: no API, no competition facts, no coverage claim.
internal static class RecordingProbe
{
    private sealed record Frame(long Id, string Method, string Origin, string[] Stack, string[] Args);
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<object, Frame> Actions = new();
    private static long _seq, _op;
    private static readonly AsyncLocal<string?> ScopedOrigin = new();
    private static string? _path;
    public static void Install()
    {
        _path = System.Environment.GetEnvironmentVariable("STS2_RECORDING_PROBE_FILE");
        if (!GameControl.Enabled || string.IsNullOrEmpty(_path) || !Path.IsPathFullyQualified(_path))
            throw new InvalidOperationException("Private isolated recording probe path required.");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var harmony = new Harmony("ega.private.recording-probe");
        var descriptors = new Dictionary<string, string[]>
        {
            ["MegaCrit.Sts2.Core.GameActions.Multiplayer.ActionQueueSynchronizer"] = ["RequestEnqueue", "RequestEnqueueHookAction"],
            ["MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer"] = ["ChooseLocalOption"],
            ["MegaCrit.Sts2.Core.Multiplayer.Game.RestSiteSynchronizer"] = ["ChooseLocalOption"],
            ["MegaCrit.Sts2.Core.Multiplayer.Game.MapSelectionSynchronizer"] = ["PlayerVotedForMapCoord", "MoveToMapCoord"],
            ["MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer"] = ["PickRelicLocally", "OnPicked", "AwardRelics", "SkipRelicLocally"],
            ["MegaCrit.Sts2.Core.Multiplayer.Game.RewardSynchronizer"] = ["SyncLocalObtainedCard", "SyncLocalObtainedRelic", "SyncLocalObtainedPotion", "SyncLocalObtainedGold", "SyncLocalSkippedCard", "SyncLocalSkippedRelic", "SyncLocalSkippedPotion"],
            ["MegaCrit.Sts2.Core.Multiplayer.Game.RewardsSetSynchronizer"] = ["SelectLocalReward", "SkipLocalRewardsSet"],
            ["MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceSynchronizer"] = ["SyncLocalChoice"],
            ["MegaCrit.Sts2.Core.Events.EventOption"] = ["Chosen"],
            ["MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry"] = ["OnTryPurchaseWrapper", "InvokePurchaseCompleted", "InvokePurchaseFailed"],
            ["MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardRemovalEntry"] = ["OnTryPurchaseWrapper"],
            ["MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom"] = ["OpenInventory"],
            ["MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom"] = ["OpenChest"],
            ["MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardGridSelectionScreen"] = ["CardsSelected"],
            ["MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen"] = ["SelectCard", "OnAlternateRewardSelected"],
            ["MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand"] = ["SelectCards"],
            ["MegaCrit.Sts2.Core.Models.CardModel"] = ["TryManualPlay", "EnqueueManualPlay", "ToMutable", "UpgradeInternal"],
            ["MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckCardSelectScreen"] = ["CloseSelection", "CancelSelection", "ConfirmSelection"],
            ["MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen"] = ["CloseSelection", "CancelSelection", "ConfirmSelection"],
            ["MegaCrit.Sts2.Core.Nodes.Potions.NPotionPopup"] = ["OnUseButtonPressed", "UsePotion", "_Input"],
            ["MegaCrit.Sts2.Core.Nodes.Potions.NPotionHolder"] = ["UsePotion"],
            ["MegaCrit.Sts2.Core.Nodes.Combat.NCardPlay"] = ["TryPlayCard", "CancelPlayCard"],
            ["MegaCrit.Sts2.Core.Nodes.Combat.NMouseCardPlay"] = ["_Input"],
            ["MegaCrit.Sts2.Core.Runs.RunHistoryUtilities"] = ["CreateRunHistoryEntry"],
        };
        var assembly = typeof(GameAction).Assembly;
        foreach (var (typeName, names) in descriptors)
        {
            var type = assembly.GetType(typeName);
            foreach (var name in names)
            {
                var methods = type?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => m.Name == name).ToArray() ?? [];
                if (methods.Length == 0) { Write(new { kind = "missing", method = typeName + "." + name }); continue; }
                foreach (var method in methods)
                {
                    if (method.IsAbstract || method.ContainsGenericParameters) continue;
                    var postfix = method.ReturnType == typeof(void) ? nameof(AfterVoid) : nameof(AfterResult);
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(RecordingProbe), nameof(Before)),
                        postfix: new HarmonyMethod(typeof(RecordingProbe), postfix));
                    Write(new { kind = "installed", method = method.DeclaringType!.Name + "." + name });
                }
            }
        }
        harmony.Patch(AccessTools.Method(typeof(NPotionPopup), "RefreshButtons"),
            postfix: new HarmonyMethod(typeof(RecordingProbe), nameof(PotionButtonsRefreshed)));
        harmony.Patch(AccessTools.Constructor(typeof(ActionExecutor), [typeof(ActionQueueSet)]),
            postfix: new HarmonyMethod(typeof(RecordingProbe), nameof(ExecutorCreated)));
    }
    // Test adapter only, not a released API: exercise the same native Back-button path as UI.
    public static object CancelUnselectedGrid()
    {
        var current = ActiveScreenContext.Instance.GetCurrentScreen();
        if (!GameControl.Enabled || current is not (NDeckCardSelectScreen or NDeckUpgradeSelectScreen) ||
            !GameStateService.TryGetCardGridSelectionMetadata(current, out var metadata) || metadata.SelectedCount != 0)
            throw new ApiException(409, "probe_cancel_unavailable", "Known unselected native grid required.");
        var buttons = Descendants((Node)current).OfType<NBackButton>().Where(b => b.IsEnabled && b.IsVisibleInTree()).ToArray();
        if (buttons.Length != 1) throw new ApiException(409, "probe_cancel_unavailable", "One native Back button required.");
        var prior = ScopedOrigin.Value; ScopedOrigin.Value = "interface";
        try { buttons[0].EmitSignal(NClickableControl.SignalName.Released, buttons[0]); }
        finally { ScopedOrigin.Value = prior; }
        return new { dispatch = "accepted", experimental = true, complete = false };
    }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void PotionButtonsRefreshed(NPotionPopup __instance)
    {
        var button = __instance.GetNodeOrNull<NButton>("%UseButton");
        Write(new { kind = "ui_diagnostic", method = "NPotionPopup.RefreshButtons", use_enabled = button?.IsEnabled,
            use_rect = button?.GetGlobalRect().ToString(), marked_for_removal = __instance.IsMarkedForRemoval });
    }
    private static void ExecutorCreated(ActionExecutor __instance)
    {
        __instance.BeforeActionExecuted += action => Executed(action, "executing");
        __instance.AfterActionExecuted += action => Executed(action, "executed");
    }
    private static void Executed(GameAction action, string stage)
    {
        Actions.TryGetValue(action, out var parent);
        Write(new { kind = "execution", stage, action = action.GetType().Name, state = action.State.ToString(), parent_id = parent?.Id, origin = action is ReadyToBeginEnemyTurnAction or GenericHookGameAction ? "engine" : parent?.Origin ?? "unknown", causal_origin = parent?.Origin, args = Describe(action) });
    }
    private static string Describe(object? value)
    {
        if (value == null) return "null";
        if (value is InputEventMouseButton mouse) return $"mouse:{mouse.ButtonIndex}:pressed={mouse.Pressed}:position={mouse.Position}";
        if (value is InputEvent input) return input.AsText();
        var text = value.ToString() ?? value.GetType().Name;
        return text.Length > 240 ? text[..240] : text;
    }
    private static void Before(MethodBase __originalMethod, object[] __args, out Frame __state)
    {
        var stack = new StackTrace().GetFrames().Select(f => f.GetMethod()).Where(m => m?.DeclaringType != null)
            .Select(m => m!.DeclaringType!.FullName + "." + m.Name).Where(n => !n.Contains(nameof(RecordingProbe))).Take(35).ToArray();
        var origin = ScopedOrigin.Value ?? (stack.Any(s => s.Contains("GameActionService")) ? "interface" :
            stack.Any(s => s.Contains("NEventOptionButton") || s.Contains("NRestSiteButton") || s.Contains("CardPlay") || s.Contains("NEndTurnButton") || s.Contains("NMapPoint") || s.Contains("NMerchant") || s.Contains("NPotion") || s.Contains("NRewardButton") || s.Contains("NCardRewardSelectionScreen")) ? "ui_candidate" : "unknown");
        __state = new Frame(Interlocked.Increment(ref _op), __originalMethod.DeclaringType!.Name + "." + __originalMethod.Name, origin, stack, __args.Select(Describe).ToArray());
        if (__originalMethod.Name is "RequestEnqueue" or "RequestEnqueueHookAction" && __args.FirstOrDefault() is GameAction action)
        { Actions.Remove(action); Actions.Add(action, __state); }
        Write(new { kind = "hook", stage = "entered", frame = __state });
    }
    private static void AfterVoid(Frame __state) => Write(new { kind = "hook", stage = "returned", frame = __state });
    private static void AfterResult(object? __result, Frame __state)
    {
        if (__result is Task task)
            _ = task.ContinueWith(t => Write(new { kind = "hook", stage = t.IsFaulted ? "failed" : t.IsCanceled ? "cancelled" : "completed", frame = __state,
                result = t.IsCompletedSuccessfully ? Describe(t.GetType().GetProperty("Result")?.GetValue(t)) : t.Exception?.GetBaseException().GetType().Name }), TaskScheduler.Default);
        else Write(new { kind = "hook", stage = "returned", frame = __state, result = Describe(__result) });
    }
    private static void Write(object value)
    {
        lock (Gate)
        {
            using var file = new FileStream(_path!, FileMode.Append, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(file, new { seq = ++_seq, utc = DateTime.UtcNow, data = value });
            file.WriteByte(10); file.Flush(true);
        }
    }
}
#endif
