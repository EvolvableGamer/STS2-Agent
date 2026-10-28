using STS2AIAgent.Server;
namespace STS2AIAgent.Tests;
internal static class ArenaFactJournalTests
{
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
