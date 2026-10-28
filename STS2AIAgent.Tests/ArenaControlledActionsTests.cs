using STS2AIAgent.Server;
namespace STS2AIAgent.Tests;
internal static class ArenaControlledActionsTests
{
 public static void StartedMustBeFirst()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {using(var actions=new ArenaControlledActions(dir,"stream","run")) {
   var fact=actions.Started(new {seed="run"});Assert.Equal(1L,fact.source_seq);Assert.Equal("started",fact.kind);
   try {actions.Started(new {seed="run"});throw new Exception("duplicate start");}catch(InvalidOperationException){}
  }} finally {Directory.Delete(dir,true);}
 }
 public static void TerminalIsSequencedAndFinal()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    var fact=actions.Terminal("death",12);Assert.Equal(1L,fact.source_seq);Assert.Equal(12,fact.payload.GetProperty("floor").GetInt32());Assert.Equal(1L,fact.payload.GetProperty("source_high_watermark").GetInt64());
   }
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    try {actions.Terminal("death",12);throw new Exception("duplicate terminal");}catch(InvalidOperationException){}
   }
  } finally {Directory.Delete(dir,true);}
 }
 public static async Task UnresolvedSurvivesRestart()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    Assert.Equal("pending",await actions.ExecuteAsync("a1","end_turn",new {},()=>Task.FromResult("pending")));
    Assert.Equal(0,actions.ReadFacts(0).Count);Assert.Equal("unknown",actions.Query("a1"));
   }
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    Assert.Equal("unknown",actions.Query("a1"));
    try {await actions.ExecuteAsync("a2","end_turn",new {},()=>Task.FromResult("completed"));throw new Exception("unresolved accepted");}catch(InvalidOperationException){}
   }
  } finally {Directory.Delete(dir,true);}
 }
 public static async Task PersistsRequestBeforeDispatchAndRejectsReplay()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {
   using var actions=new ArenaControlledActions(dir,"stream","run");
   var count=0;
   var result=await actions.ExecuteAsync("a1","end_turn",new {action="end_turn"},async()=>{count++;Assert.True(new FileInfo(Path.Combine(dir,"requests.jsonl")).Length>0);await Task.Yield();return "completed";});
   Assert.Equal("completed",result);Assert.Equal("completed",actions.Query("a1"));Assert.Equal(1,count);Assert.Equal("operation",actions.ReadFacts(0)[0].kind);
   try {await actions.ExecuteAsync("a1","end_turn",new {action="end_turn"},()=>Task.FromResult("completed"));throw new Exception("replayed");}catch(InvalidOperationException){}
  } finally {Directory.Delete(dir,true);}
 }
}
