using STS2AIAgent.Game;
using System.Text.Json;

namespace STS2AIAgent.Tests;

internal static class GameStartCommandStoreTests
{
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    public static void IntentSurvivesReconstructionWithoutReplay()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var first = new GameStartCommandStore(directory);
            Assert.True(first.Begin("command_1", "instance_a"));
            var restored = new GameStartCommandStore(directory);
            Assert.True(restored.HasIntent("command_1"));
            Assert.False(restored.Begin("command_1", "instance_a"));
            Assert.Null(restored.Read("command_1", "instance_a"));
            restored.Complete("command_1", new { instance_id = "instance_a", outcome = "executed" });
            Assert.Equal("executed", ((JsonElement)restored.Read("command_1", "instance_a")!).GetProperty("outcome").GetString());
            Assert.Null(restored.Read("command_1", "instance_b"));
            Assert.False(restored.Begin("command_1", "instance_b"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    public static void TornResultAndUnsafeIdFailClosed()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var store = new GameStartCommandStore(directory);
            Throws<ArgumentException>(() => store.Begin("../escape", "instance"));
            Assert.True(store.Begin("command", "instance"));
            File.WriteAllText(Path.Combine(directory, "command.result"), "{");
            Throws<JsonException>(() => store.Read("command", "instance"));
            Assert.False(store.Begin("command", "instance"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
