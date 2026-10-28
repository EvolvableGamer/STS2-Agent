using STS2AIAgent.Server;
namespace STS2AIAgent.Tests;
internal static class ArenaTerminalPolicyTests
{
 public static void RequiresHistoryAndNaturalSummary()
 {
  Assert.True(ArenaTerminalPolicy.Allows(true,false,true,true,12));
  Assert.False(ArenaTerminalPolicy.Allows(false,false,true,true,12));
  Assert.False(ArenaTerminalPolicy.Allows(true,true,true,true,12));
  Assert.False(ArenaTerminalPolicy.Allows(true,false,false,true,12));
  Assert.False(ArenaTerminalPolicy.Allows(true,false,true,false,12));
  Assert.False(ArenaTerminalPolicy.Allows(true,false,true,true,null));
 }
}
