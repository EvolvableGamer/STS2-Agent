using STS2AIAgent.Server;
namespace STS2AIAgent.Tests;
internal static class ArenaLiveReplaySnapshotTests
{
 private static void Reject(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("Expected rejection");}
 public static void ValidSnapshotUsesExactBoundRunAndPreservesActions()
 {
  object run=new(),executor=new(); var binding=new ArenaLiveReplayBinding(run,executor,"stream","ABC123","IRONCLAD",0);
  var result=ArenaLiveReplaySnapshot.Build(binding,run,executor,"stream","ABC123","ironclad",0,true,()=>new[]{"PlayCard 3 1 # Strike","EndTurn"});
  Assert.Equal("# Character: IRONCLAD\n# Seed: ABC123\n# Ascension: 0\nPlayCard 3 1 # Strike\nEndTurn\n",result.content);
  Assert.Equal(2,result.action_count);Assert.False(result.complete);Assert.Equal("runreplays-live-buffer/1",result.source);
 }
 public static void GuardRejectsBeforeReadingBuffer()
 {
  object run=new(),executor=new();var binding=new ArenaLiveReplayBinding(run,executor,"stream","ABC123","IRONCLAD",0);int reads=0;
  IReadOnlyList<string> Read(){reads++;return new[]{"EndTurn"};}
  Reject(()=>ArenaLiveReplaySnapshot.Build(binding,new object(),executor,"stream","ABC123","IRONCLAD",0,true,Read));
  Reject(()=>ArenaLiveReplaySnapshot.Build(binding,run,new object(),"stream","ABC123","IRONCLAD",0,true,Read));
  Reject(()=>ArenaLiveReplaySnapshot.Build(binding,run,executor,"other","ABC123","IRONCLAD",0,true,Read));
  Reject(()=>ArenaLiveReplaySnapshot.Build(binding,run,executor,"stream","OTHER","IRONCLAD",0,true,Read));
  Reject(()=>ArenaLiveReplaySnapshot.Build(binding,run,executor,"stream","ABC123","DEFECT",0,true,Read));
  Reject(()=>ArenaLiveReplaySnapshot.Build(binding,run,executor,"stream","ABC123","IRONCLAD",1,true,Read));
  Reject(()=>ArenaLiveReplaySnapshot.Build(binding,run,executor,"stream","ABC123","IRONCLAD",0,false,Read));
  Assert.Equal(0,reads);
 }
 public static void RejectsEmptyOversizedOrMalformedBuffer()
 {
  object run=new(),executor=new();var b=new ArenaLiveReplayBinding(run,executor,"stream","ABC123","IRONCLAD",0);
  Reject(()=>ArenaLiveReplaySnapshot.Build(b,run,executor,"stream","ABC123","IRONCLAD",0,true,()=>Array.Empty<string>()));
  Reject(()=>ArenaLiveReplaySnapshot.Build(b,run,executor,"stream","ABC123","IRONCLAD",0,true,()=>new[]{"# comment"}));
  Reject(()=>ArenaLiveReplaySnapshot.Build(b,run,executor,"stream","ABC123","IRONCLAD",0,true,()=>new[]{new string('x',3*1024*1024)}));
  Reject(()=>ArenaLiveReplaySnapshot.Build(b,run,executor,"stream","ABC123","IRONCLAD",0,true,()=>new[]{"EndTurn\n# Seed: OTHER"}));
 }
 private static class Buffer {public static IReadOnlyList<string> SnapshotMinimal()=>new[]{"EndTurn"};}
 private static class BadBuffer {public static string SnapshotMinimal()=>"bad";}
 public static void ReflectionOnlyInvokesExpectedStaticShape()
 {
  Assert.Equal("EndTurn",ArenaLiveReplaySnapshot.Resolve(typeof(Buffer))()[0]);
  Reject(()=>ArenaLiveReplaySnapshot.Resolve(typeof(BadBuffer)));Reject(()=>ArenaLiveReplaySnapshot.Resolve(null));
 }
}
