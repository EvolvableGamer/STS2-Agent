using STS2AIAgent.Game;
namespace STS2AIAgent.Tests;
internal static class ArenaSeedPolicyTests
{
 public static void RejectsBlankOrOversize()
 {
  Assert.False(ArenaSeedPolicy.IsValid(""));
  Assert.False(ArenaSeedPolicy.IsValid(new string('A',101)));
  Assert.True(ArenaSeedPolicy.IsValid("ABC123"));
 }
}
