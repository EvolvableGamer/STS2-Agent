using STS2AIAgent.Game;
namespace STS2AIAgent.Tests;
internal static class ArenaInputPolicyTests
{
 public static void OnlyRecordsPressesWithoutKeyRepeat()
 {
  Assert.True(ArenaInputPolicy.ShouldRecord(true,false));Assert.False(ArenaInputPolicy.ShouldRecord(false,false));Assert.False(ArenaInputPolicy.ShouldRecord(true,true));
 }
}
