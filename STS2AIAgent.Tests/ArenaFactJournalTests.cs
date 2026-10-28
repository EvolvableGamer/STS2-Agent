using STS2AIAgent.Server;
namespace STS2AIAgent.Tests;
internal static class ArenaFactJournalTests
{
 public static async Task TerminalWatermarkIsAtomic()
 {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".jsonl");
  try {using var j=new ArenaFactJournal(path,"stream","run");j.Append("started",new {});
   await Task.WhenAll(Task.Run(()=>{for(var i=0;i<100;i++)j.AppendObservedInput("mouse_button");}),Task.Run(()=>j.AppendTerminal("death",2)));
   var facts=j.ReadAfter(0);var terminal=facts[^1];Assert.Equal("terminal",terminal.kind);Assert.Equal(terminal.source_seq,terminal.payload.GetProperty("source_high_watermark").GetInt64());
  } finally {File.Delete(path);}
 }
 public static void ObservedInputRequiresStartAndStopsAfterTerminal()
 {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".jsonl");
  try {using var j=new ArenaFactJournal(path,"stream","run");
   Assert.False(j.AppendObservedInput("mouse_button"));
   j.Append("started",new {});Assert.True(j.AppendObservedInput("mouse_button"));
   j.Append("terminal",new {});Assert.False(j.AppendObservedInput("keyboard"));
   Assert.Equal(3,j.ReadAfter(0).Count);
  } finally {File.Delete(path);}
 }
 public static async Task ConcurrentAppendsRemainContiguous()
 {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".jsonl");
  try {using(var j=new ArenaFactJournal(path,"stream","run")) {
   await Task.WhenAll(Enumerable.Range(0,32).Select(i=>Task.Run(()=>j.Append("incident",new {i}))));
   Assert.Equal(32,j.ReadAfter(0).Count);
  } using(var j=new ArenaFactJournal(path,"stream","run")) Assert.Equal(32,j.ReadAfter(0).Count);
  } finally {File.Delete(path);}
 }
 public static void RejectsWrongBinding()
 {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".jsonl");
  try {using(var j=new ArenaFactJournal(path,"stream","run")) j.Append("started",new {});
   try {using var j=new ArenaFactJournal(path,"other","run");throw new Exception("accepted wrong binding");} catch(InvalidDataException) {}}
  finally {File.Delete(path);}
 }
 public static void RejectsIncompleteTail()
 {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".jsonl");
  try {File.WriteAllText(path,"{broken");try {using var j=new ArenaFactJournal(path,"stream","run");throw new Exception("accepted incomplete tail");} catch(InvalidDataException) {}}
  finally {File.Delete(path);}
 }
 public static void ReopensAndReadsAfterCursor()
 {
  var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".jsonl");
  try {
   using(var j=new ArenaFactJournal(path,"stream","run")){j.Append("started",new {x=1});j.Append("operation",new {x=2});}
   using(var j=new ArenaFactJournal(path,"stream","run")){var facts=j.ReadAfter(1);Assert.Equal(1,facts.Count);Assert.Equal(2L,facts[0].source_seq);}
  } finally {File.Delete(path);}
 }
}
