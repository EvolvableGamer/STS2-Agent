using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STS2AIAgent.Server;

// Native MCP presentation only: never used by HTTP, GameStateService or the in-game AI.
internal static class NativeMcpState
{
    internal const int MaxCharacters = 7600;
    internal static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static string Project(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject state) return json;
        var screen = state["screen"]?.GetValue<string>();
        var combat = state["combat"] as JsonObject;
        var inCombat = state["in_combat"]?.GetValue<bool>() == true || screen == "COMBAT";
        var solo = state["session"]?["mode"]?.GetValue<string>() == "singleplayer";
        if (screen != "MAP") state.Remove("map");
        state.Remove("native_profile_id");
        state.Remove("run_id");
        if (state["run"] is JsonObject run)
        {
            var localDeck = (run["players"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(player => player["is_local"]?.GetValue<bool>() == true)?["deck"] as JsonArray;
            var deckCount = localDeck?.Count;
            if (solo) run.Remove("players"); // Same local deck/relic/potion data is already in run.
            if (inCombat)
            {
                run["deck_count"] = deckCount;
                run.Remove("piles"); // Combat already carries draw/discard/exhaust; avoid a second copy.
                run.Remove("deck");
                if (run["players"] is JsonArray players)
                    foreach (var player in players.OfType<JsonObject>())
                    {
                        if (player["deck"] is JsonArray cards) player["deck_count"] = cards.Count;
                        player.Remove("deck");
                    }
            }
        }
        if (combat != null)
        {
            combat.Remove("card_value_note"); // Rules/preview semantics remain in the opt-in play contract resource.
            if (solo) combat.Remove("players");
            if (combat["action_readiness"] is JsonObject readiness && readiness["can_use_combat_actions"]?.GetValue<bool>() == true)
                combat["action_readiness"] = new JsonObject { ["can_use_combat_actions"] = true };
        }
        RemoveNulls(state);
        var result = state.ToJsonString(Json);
        if (result.Length <= MaxCharacters) return result;
        // Never cut JSON, legal options, card text or a list halfway through. Explicit raw recovery.
        return new JsonObject
        {
            ["screen"] = screen,
            ["error"] = new JsonObject
            {
                ["code"] = "compact_state_too_large",
                ["characters"] = result.Length,
                ["message"] = "State exceeds the compact budget. Read get_raw_game_state or use raw_state=true with a client output limit large enough for the full result; do not guess actions."
            }
        }.ToJsonString(Json);
    }

    // Strip every previous snapshot and project exactly the current post-action state.
    internal static string Receipt(string json, bool raw)
    {
        if (JsonNode.Parse(json) is not JsonObject result) return json;
        result.Remove("previous");
        if (!raw && result["state"] != null)
            result["state"] = JsonNode.Parse(Project(result["state"]!.ToJsonString(Json)));
        return result.ToJsonString(Json);
    }

    private static void RemoveNulls(JsonNode node)
    {
        // Only inactive top-level sections. Nested nulls encode unknown values, e.g.
        // Crystal Sphere's unrevealed kind/is_good, and must retain their meaning.
        if (node is JsonObject obj)
            foreach (var key in obj.Where(x => x.Value == null).Select(x => x.Key).ToArray())
                obj.Remove(key);
    }
}
