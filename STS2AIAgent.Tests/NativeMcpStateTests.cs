using System.Text.Json.Nodes;
using STS2AIAgent.Server;
namespace STS2AIAgent.Tests;
internal static class NativeMcpStateTests
{
    public static void SceneFidelityAndBudget()
    {
        var input = """{"screen":"COMBAT","in_combat":true,"session":{"mode":"singleplayer"},"available_actions":["play_card"],"map":{"nodes":[1]},"combat":{"action_readiness":{"can_use_combat_actions":true,"reason":"ready","debug":"large"},"player":{"energy":3},"players":[{"energy":3}],"hand":[{"i":3,"card_id":"ARMAMENTS","targets":[2],"line":"Upgrade"}]},"run":{"deck":[{"n":2,"card_ids":["STRIKE","STRIKE"]}],"players":[{"is_local":true,"deck":[1,2]}],"relic_ids":["BURNING_BLOOD"],"piles":{"draw":5}}}""";
        var state = JsonNode.Parse(NativeMcpState.Project(input))!;
        Assert.True(state["map"] == null);
        Assert.True(state["run"]!["deck"] == null);
        Assert.Equal(2, state["run"]!["deck_count"]!.GetValue<int>());
        Assert.Equal(3, state["combat"]!["hand"]![0]!["i"]!.GetValue<int>());
        Assert.Equal(2, state["combat"]!["hand"]![0]!["targets"]![0]!.GetValue<int>());
        Assert.True(state["combat"]!["action_readiness"]!["debug"] == null);
        foreach (var (screen, field) in new[] { ("MAP","map"),("CARD_SELECTION","selection"),("REWARD","reward"),("EVENT","event"),("SHOP","shop"),("REST","rest"),("MAIN_MENU","character_select"),("MODAL","modal"),("CHEST","chest"),("GAME_OVER","game_over"),("CRYSTAL_SPHERE","crystal_sphere"),("UNLOCK","unlock") })
        {
            var scene = new JsonObject { ["screen"] = screen, [field] = new JsonObject { ["sentinel"] = true, ["options"] = new JsonArray(1,2,3) } };
            var projected = JsonNode.Parse(NativeMcpState.Project(scene.ToJsonString()))!;
            Assert.True(projected[field]!["sentinel"]!.GetValue<bool>());
            Assert.True(projected.ToJsonString().Length < 8192);
        }
        var hidden = NativeMcpState.Project("""{"screen":"CRYSTAL_SPHERE","crystal_sphere":{"items":[{"kind":null,"is_good":null,"cells":[[0,1]]}]}}""");
        Assert.Contains("\"kind\":null", hidden);
        Assert.Contains("\"is_good\":null", hidden);
        var oversized = new JsonObject { ["screen"]="CARD_SELECTION", ["selection"] = new JsonObject { ["cards"] = new string('x', 20000) } };
        var limited=NativeMcpState.Project(oversized.ToJsonString());
        Assert.True(limited.Length < 8192);
        Assert.Contains("compact_state_too_large", limited);
        var waiting=JsonNode.Parse(input)!;waiting["combat"]!["action_readiness"]!["can_use_combat_actions"]=false;
        Assert.Contains("debug",NativeMcpState.Project(waiting.ToJsonString()));
    }
}
