namespace STS2AIAgent.Game;
internal static class ArenaInputPolicy
{
 public static bool ShouldRecord(bool pressed,bool echo)=>pressed&&!echo;
}
