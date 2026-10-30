using System.Runtime.InteropServices;
using STS2AIAgent.Game;
namespace STS2AIAgent.Tests;
internal static class PublicBuildProfileTests
{
    public static void PlatformHashesFailClosed()
    {
        const string mac = "e7ceb80669bfaf5c8fccabaa126ae2bb283aba514be5b5b55612579cfd285f18";
        Assert.Equal(mac, PublicBuildProfile.ExpectedHash("darwin", Architecture.Arm64));
        Assert.Null(PublicBuildProfile.ExpectedHash("win32", Architecture.X64));
        PublicBuildProfile.Validate("darwin", Architecture.Arm64, mac.ToUpperInvariant());
        foreach (var value in new[] { ("win32", Architecture.X64, mac), ("darwin", Architecture.X64, mac), ("darwin", Architecture.Arm64, new string('0', 64)) })
        {
            try { PublicBuildProfile.Validate(value.Item1, value.Item2, value.Item3); }
            catch (InvalidOperationException error) { Assert.Contains("23811903", error.Message); continue; }
            throw new Exception("An unsupported or mismatched engine was accepted.");
        }
    }
    public static void ModValidatesBeforeInitializing()
    {
        var source = AgentSourceFixture.Read("STS2AIAgent/ModEntry.cs");
        Assert.True(source.IndexOf("PublicBuildProfile.Validate", StringComparison.Ordinal) < source.IndexOf("GameControl.Install", StringComparison.Ordinal));
        Assert.Contains("RuntimeInformation.ProcessArchitecture", source);
        Assert.Contains("STS2_PUBLIC_ENABLE_UPSTREAM_UI", source);
        var router = AgentSourceFixture.Read("STS2AIAgent/Server/Router.cs");
        Assert.False(router.Contains("/arena/", StringComparison.Ordinal));
        Assert.True(router.Contains("GameControl.Authorized", StringComparison.Ordinal));
        Assert.True(AgentSourceFixture.SourceFiles().All(path => !Path.GetFileName(path).StartsWith("Arena", StringComparison.Ordinal)));
    }
    public static void NestedChoiceNeverCompletesOrCancelsParent()
    {
        var source = AgentSourceFixture.ReadActionService();
        Assert.Contains("if (IsPlayCardAwaitingPlayerInput()) return false;", AgentSourceFixture.MethodBody(source, "IsPlayCardStable"));
        var play = AgentSourceFixture.MethodBody(source, "ExecutePlayCardAsync");
        Assert.Contains("!stable && !IsPlayCardAwaitingPlayerInput()", play);
        Assert.Contains("!IsPlayCardAwaitingPlayerInput() && CardPlayCounterPolicy.ShouldRollBack", play);
        Assert.False(source.Contains("ArenaSelection", StringComparison.Ordinal));
        Assert.False(source.Contains("arenaPermit", StringComparison.Ordinal));
    }
}
