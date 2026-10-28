namespace STS2AIAgent.Server;
internal static class ArenaTerminalPolicy
{
 public static bool Allows(bool hasHistory,bool abandoned,bool summaryReady,bool seedMatches,int? floor)=>hasHistory&&!abandoned&&summaryReady&&seedMatches&&floor is >=0;
}
