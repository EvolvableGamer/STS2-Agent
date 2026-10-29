using STS2AIAgent.Server;
namespace STS2AIAgent.Tests;
internal static class ArenaSelectionContinuationTests
{
 private static async Task Reject(Func<Task> action){try{await action();}catch(InvalidOperationException){return;}throw new Exception("Expected refusal");}
 public static async Task ParentStaysPendingUntilNativeCompletionAndChildrenAreRecorded()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {using var actions=new ArenaControlledActions(dir,"stream","run");
   var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   var parent=await actions.ExecuteAsync("parent","play_card",new {action="play_card"},()=>Task.FromResult("pending"),()=>new ArenaSelectionContinuation(done.Task,()=>true));
   Assert.Equal("pending",parent);Assert.Equal("pending",actions.Query("parent"));
   await Reject(()=>actions.ExecuteContinuationAsync("invalid","other","select_deck_card",new {},_=>Task.FromResult("completed")));
   await Reject(()=>actions.ExecuteContinuationAsync("invalid","parent","end_turn",new {},_=>Task.FromResult("completed")));
   Assert.Equal("completed",await actions.ExecuteContinuationAsync("child1","parent","select_deck_card",new {action="select_deck_card"},proof=>Task.FromResult(proof()?"completed":"pending")));
   Assert.Equal("pending",actions.Query("parent"));
   Assert.Equal("completed",await actions.ExecuteContinuationAsync("child2","parent","confirm_selection",new {action="confirm_selection"},proof=>Task.FromResult(proof()?"completed":"pending")));
   Assert.Equal("pending",actions.Query("parent"));Assert.Equal(2,actions.ReadFacts(0).Count);
   done.SetResult(true);Assert.Equal("completed",actions.Query("parent"));
   var facts=actions.ReadFacts(0);Assert.Equal(3,facts.Count);Assert.Equal("parent",facts[2].payload.GetProperty("caused_by_action_id").GetString());
  } finally {Directory.Delete(dir,true);}
 }
 public static async Task UnknownChildOrRestartNeverResolvesOrAllowsContinuation()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {
   var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    await actions.ExecuteAsync("parent","play_card",new {},()=>Task.FromResult("pending"),()=>new ArenaSelectionContinuation(done.Task,()=>true));
    Assert.Equal("pending",await actions.ExecuteContinuationAsync("child","parent","select_deck_card",new {},_=>Task.FromResult("pending")));
    done.SetResult(true);Assert.Equal("unknown",actions.Query("child"));Assert.Equal("unknown",actions.Query("parent"));
    await Reject(()=>actions.ExecuteContinuationAsync("child2","parent","confirm_selection",new {},_=>Task.FromResult("completed")));
   }
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    Assert.Equal("unknown",actions.Query("parent"));Assert.Equal("unknown",actions.Query("child"));
    await Reject(()=>actions.ExecuteContinuationAsync("child3","parent","confirm_selection",new {},_=>Task.FromResult("completed")));
   }
  } finally {Directory.Delete(dir,true);}
 }
 public static async Task FaultedNativeCompletionStaysUnknown()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {using var actions=new ArenaControlledActions(dir,"stream","run");
   var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   await actions.ExecuteAsync("parent","play_card",new {},()=>Task.FromResult("pending"),()=>new ArenaSelectionContinuation(done.Task,()=>true));
   Assert.Equal("pending",actions.Query("parent"));
   done.SetException(new Exception("native action failed"));Assert.Equal("unknown",actions.Query("parent"));Assert.Equal(0,actions.ReadFacts(0).Count);
  } finally {Directory.Delete(dir,true);}
 }
 public static async Task RestartAfterCompletedChildCannotRecoverParentProof()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {
   var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    await actions.ExecuteAsync("parent","play_card",new {},()=>Task.FromResult("pending"),()=>new ArenaSelectionContinuation(done.Task,()=>true));
    await actions.ExecuteContinuationAsync("child","parent","select_deck_card",new {},_=>Task.FromResult("completed"));
   }
   using(var actions=new ArenaControlledActions(dir,"stream","run")) {
    Assert.Equal("unknown",actions.Query("parent"));Assert.Equal("completed",actions.Query("child"));
    await Reject(()=>actions.ExecuteContinuationAsync("child2","parent","confirm_selection",new {},_=>Task.FromResult("completed")));
   }
  } finally {Directory.Delete(dir,true);}
 }
 public static async Task StaleSelectionGuardLeavesUnknownAndBlocksFurtherChoices()
 {
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  try {using var actions=new ArenaControlledActions(dir,"stream","run");var done=new TaskCompletionSource<bool>();
   await actions.ExecuteAsync("parent","play_card",new {},()=>Task.FromResult("pending"),()=>new ArenaSelectionContinuation(done.Task,()=>false));
   await Reject(()=>actions.ExecuteContinuationAsync("child","parent","select_deck_card",new {},guard=>guard()?Task.FromResult("completed"):throw new InvalidOperationException("stale")));
   Assert.Equal("unknown",actions.Query("child"));
   await Reject(()=>actions.ExecuteContinuationAsync("child2","parent","confirm_selection",new {},_=>Task.FromResult("completed")));
  } finally {Directory.Delete(dir,true);}
 }
 public static void OpenSelectionCannotBeConsideredStable()
 {
  var source=AgentSourceFixture.ReadActionService();
  var stable=AgentSourceFixture.MethodBody(source,"IsPlayCardStable");
  Assert.Contains("IsPlayCardAwaitingPlayerInput()",stable);
  var play=AgentSourceFixture.MethodBody(source,"ExecutePlayCardAsync");
  Assert.Contains("!stable && !IsPlayCardAwaitingPlayerInput()",play);
 }
}
