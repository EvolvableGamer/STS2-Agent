using STS2AIAgent.Game;
namespace STS2AIAgent.Tests;
internal static class ArenaRewardExitPolicyTests
{
 public static void AllowsOnlyEmptyCompletedReward()
 {
  Assert.True(ArenaRewardExitPolicy.Allows(true,false,true,false));
  Assert.False(ArenaRewardExitPolicy.Allows(false,false,true,false));
  Assert.False(ArenaRewardExitPolicy.Allows(true,true,true,false));
  Assert.False(ArenaRewardExitPolicy.Allows(true,false,false,false));
  Assert.False(ArenaRewardExitPolicy.Allows(true,false,true,true));
 }
}
