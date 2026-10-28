using STS2AIAgent.Game;
namespace STS2AIAgent.Tests;
internal static class ArenaUnlockPolicyTests
{
 public static void OnlyExactIsolatedProfileAndOptInAreAccepted()
 {
  Assert.True(ArenaUnlockPolicy.Allows("2026102901",1,"user://default/2026102901/modded/profile1/saves/progress.save",true));
  Assert.False(ArenaUnlockPolicy.Allows(null,1,"user://default/2026102901/modded/profile1/saves/progress.save",true));
  Assert.False(ArenaUnlockPolicy.Allows("2026102901",1,"user://steam/123/profile1/saves/progress.save",true));
  Assert.False(ArenaUnlockPolicy.Allows("2026102901",2,"user://default/2026102901/modded/profile1/saves/progress.save",true));
  Assert.False(ArenaUnlockPolicy.Allows("2026102901",1,"user://default/2026102901/modded/profile1/saves/progress.save",false));
 }
}
