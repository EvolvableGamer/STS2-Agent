using System.Text.Json;
namespace STS2AIAgent.Server;
// Controlled-action evidence only. It does not observe manual input or automatic game transitions.
internal sealed class ArenaControlledActions : IDisposable
{
 private readonly ArenaFactJournal _requests, _facts;
 private readonly SemaphoreSlim _gate = new(1,1);
 private readonly object _observationGate=new();
 private bool _pendingInput;
 private int _inputIncident;
 private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
 private readonly Dictionary<string,string> _statuses = new(StringComparer.Ordinal);
 private bool _unresolved, _terminal;
 private ArenaFact? _terminalFact;
 public ArenaControlledActions(string directory,string stream,string run)
 {
  Directory.CreateDirectory(directory);
  _requests=new ArenaFactJournal(Path.Combine(directory,"requests.jsonl"),stream+"-requests",run);
  try {_facts=new ArenaFactJournal(Path.Combine(directory,"facts.jsonl"),stream,run);}
  catch {_requests.Dispose();throw;}
  long factCursor=0;
  while(true) {var page=_facts.ReadAfter(factCursor);if(page.Count==0)break;foreach(var fact in page) {if(_terminal)throw new InvalidDataException("Facts after terminal");if(fact.kind=="terminal"){_terminal=true;_terminalFact=fact;}if(fact.kind=="incident")_inputIncident=1;factCursor=fact.source_seq;}}
  long cursor=0;
  while(true) { var page=_requests.ReadAfter(cursor); if(page.Count==0)break; foreach(var record in page) {
   if(record.kind=="input_observed"){_inputIncident=1;cursor=record.source_seq;continue;}
   var id=record.payload.GetProperty("action_id").GetString() ?? throw new InvalidDataException("Missing action id");
   if(record.kind=="requested") {if(!_ids.Add(id)||_unresolved)throw new InvalidDataException("Invalid request history");_statuses[id]="unknown";_unresolved=true;}
   else if(record.kind=="resolved") {if(!_unresolved||!_ids.Contains(id))throw new InvalidDataException("Invalid resolution history");_statuses[id]="completed";_unresolved=false;}
   else throw new InvalidDataException("Unknown request history");
   cursor=record.source_seq;
  }}
 }
 public async Task<ArenaFact> StartAsync(string id,object request,Func<Task<object>> dispatch)
 {
  if(string.IsNullOrWhiteSpace(id)||id.Length>128)throw new ArgumentException("Invalid start ID");
  await _gate.WaitAsync();
  try {
   if(_facts.LastSequence!=0||_terminal||_unresolved||Volatile.Read(ref _inputIncident)!=0||_ids.Contains(id))throw new InvalidOperationException("Start already attempted");
   _requests.Append("requested",new {action_id=id,action="start_task",request});_ids.Add(id);_statuses[id]="unknown";_unresolved=true;
   var actual=await dispatch();
   ArenaFact fact;
   lock(_observationGate) {
    fact=_facts.Append("started",new {actual_conditions=actual,coverage="bridge_confirmed_only",complete=false});
    if(_pendingInput){_facts.Append("incident",new {code="external_input_during_start",coverage="partial",complete=false});_pendingInput=false;}
   }
   _requests.Append("resolved",new {action_id=id,status="completed"});_statuses[id]="completed";_unresolved=false;
   return fact;
  } finally {_gate.Release();}
 }
 public async Task<string> ExecuteAsync(string id,string action,object request,Func<Task<string>> dispatch)
 {
  if(string.IsNullOrWhiteSpace(id)||id.Length>128||string.IsNullOrWhiteSpace(action))throw new ArgumentException("Missing action id or action");
  await _gate.WaitAsync();
  try {
   if(_terminal||_unresolved||Volatile.Read(ref _inputIncident)!=0||_ids.Contains(id))throw new InvalidOperationException("Unresolved or duplicate action");
   _requests.Append("requested",new {action_id=id,action,request});_ids.Add(id);_statuses[id]="unknown";_unresolved=true;
   var status=await dispatch();
   if(status!="completed")return status;
   _facts.Append("operation",new {operation_id=id+"-operation",caused_by_action_id=id,origin="agent_interface",action,request,status="completed",coverage="bridge_confirmed_only"});
   _requests.Append("resolved",new {action_id=id,status});_statuses[id]="completed";_unresolved=false;
   return status;
  } finally {_gate.Release();}
 }
 public ArenaFact Started(object actualConditions)
 {
  _gate.Wait();
  try {
   if(_facts.LastSequence!=0||_unresolved||_terminal)throw new InvalidOperationException("Start is not first");
   return _facts.Append("started",new {actual_conditions=actualConditions,coverage="bridge_confirmed_only",complete=false});
  } finally {_gate.Release();}
 }
 public ArenaFact Terminal(string outcome,int floor)
 {
  _gate.Wait();
  try {
   if(_unresolved)throw new InvalidOperationException("Action outcome unresolved");
   if(_terminal) {
    if(_terminalFact?.payload.GetProperty("outcome").GetString()==outcome && _terminalFact.payload.GetProperty("floor").GetInt32()==floor)return _terminalFact;
    throw new InvalidOperationException("Conflicting terminal");
   }
   lock(_observationGate) {
    var fact=_facts.AppendTerminal(outcome,floor);
    _terminal=true;_terminalFact=fact;return fact;
   }
  } finally {_gate.Release();}
 }
 public void RecordEngineAction(string phase,uint? id,string type)
 {
  if(phase is not ("started" or "finished"))throw new ArgumentException("Invalid engine action phase");
  lock(_observationGate) {
   if(_terminal)return;
   if(_facts.LastSequence==0){_requests.Append("input_observed",new {input_kind="engine_action_before_started"});_pendingInput=true;Interlocked.Exchange(ref _inputIncident,1);return;}
   _facts.Append("progress",new {kind="engine_action_"+phase,game_action_id=id,action_type=type,origin="unattributed",coverage="partial",complete=false});
  }
 }
 public void RecordExternalInput(string kind)
 {
  lock(_observationGate) {
   if(_terminal)return;
   Interlocked.Exchange(ref _inputIncident,1);
   _requests.Append("input_observed",new {input_kind=kind});
   if(_facts.LastSequence==0){_pendingInput=true;return;}
   _facts.Append("incident",new {code="external_window_input",input_kind=kind,origin="unattributed",coverage="partial",complete=false});
  }
 }
 public string Query(string id) { _gate.Wait(); try {return _statuses.GetValueOrDefault(id,"not_found");} finally {_gate.Release();} }
 public IReadOnlyList<ArenaFact> ReadFacts(long afterSeq) { _gate.Wait(); try {return _facts.ReadAfter(afterSeq);} finally {_gate.Release();} }
 public void Dispose(){_requests.Dispose();_facts.Dispose();_gate.Dispose();}
}
