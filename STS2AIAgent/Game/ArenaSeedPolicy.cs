namespace STS2AIAgent.Game;
internal static class ArenaSeedPolicy
{
 public static bool IsValid(string? seed)=>!string.IsNullOrWhiteSpace(seed)&&seed.Length<=100&&seed.All(char.IsAsciiLetterOrDigit);
}
