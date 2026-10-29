using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;
using STS2AIAgent.Server;

namespace STS2AIAgent.Game;

// Tracks only the native play action for this exact CardModel in this executor.
internal sealed class ArenaSelectionTracker : IDisposable
{
 private readonly ActionExecutor _executor;
 private readonly CardModel _card;
 private readonly TaskCompletionSource<bool> _finished=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private GameAction? _action;
 private bool _disposed;
 internal ArenaSelectionTracker(ActionExecutor executor,CardModel card)
 {
  _executor=executor;_card=card;executor.BeforeActionExecuted+=Before;executor.AfterActionExecuted+=After;
 }
 private void Before(GameAction action)
 {
  try {if(_action==null&&action is PlayCardAction play&&ReferenceEquals(play.NetCombatCard.ToCardModelOrNull(),_card))_action=action;}
  catch { /* No proof means the action remains unknown. */ }
 }
 private void After(GameAction action)
 {
  if(!ReferenceEquals(_action,action)||action.State!=GameActionState.Finished)return;
  Dispose();_finished.TrySetResult(true);
 }
 private bool CanContinue()
 {
  var screen=ActiveScreenContext.Instance.GetCurrentScreen();
  return !_finished.Task.IsCompleted&&ReferenceEquals(RunManager.Instance.ActionExecutor,_executor)&&_action!=null&&
   _action.State is GameActionState.GatheringPlayerChoice or GameActionState.ReadyToResumeExecuting&&
   screen!=null&&GameStateService.ResolveScreen(screen)=="CARD_SELECTION";
 }
 internal ArenaSelectionContinuation? PendingSelection()=>CanContinue()?new ArenaSelectionContinuation(_finished.Task,CanContinue):null;
 public void Dispose()
 {
  if(_disposed)return;_disposed=true;_executor.BeforeActionExecuted-=Before;_executor.AfterActionExecuted-=After;
 }
}
