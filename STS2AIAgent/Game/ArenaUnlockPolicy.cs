namespace STS2AIAgent.Game;
internal static class ArenaUnlockPolicy
{
 public static bool Allows(string? optIn,int profile,string path,bool mainMenu)=>optIn=="2026102901"&&profile==1&&mainMenu&&path.Replace('\\','/')=="user://default/2026102901/modded/profile1/saves/progress.save";
}
