using STS2AIAgent.Game;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace STS2AIAgent.Server;
internal sealed record ArenaCustomSetup(string mode,string? seed,string? character_id,int ascension,string[] modifiers,int native_profile_id);
internal sealed class ArenaStartRequest { public string? command_id {get; init;} public JsonElement task {get; init;} }
internal sealed class ArenaControlledBindRequest { public string? stream_id { get; init; } public string? game_run_ref { get; init; } }
internal sealed class ArenaControlledActionRequest { public string? action_id { get; init; } public ActionRequest? request { get; init; } }
internal static class ArenaControlledSession
{
 private static readonly HashSet<string> AllowedActions = new(StringComparer.Ordinal) {
  "play_card","end_turn","choose_map_node","choose_event_option","proceed","choose_rest_option",
  "choose_reward_card","skip_reward_cards","claim_reward","collect_rewards_and_proceed","confirm_selection","select_deck_card",
  "choose_treasure_relic","open_chest","close_shop_inventory","open_shop_inventory","confirm_modal",
  "decline_tutorials","close_cards_view","confirm_unlock","continue_game_over"
 };
 internal static bool AllowsAction(string? action)=>action!=null&&AllowedActions.Contains(action);
 internal static void CheckRewardExit(string action,GameStatePayload state)
 {
  if(action!="collect_rewards_and_proceed")return;
  var reward=state.reward;
  if(!ArenaRewardExitPolicy.Allows(state.screen=="REWARD"&&reward!=null,reward?.pending_card_choice??true,reward?.can_proceed??false,reward?.rewards.Any(x=>x.claimable)??true))
   throw new ApiException(409,"reward_not_drained","Claim rewards and resolve card choices individually before leaving.");
 }
 private static readonly object ControlledPermit=new();
 private static int _bypassAttempt;
 internal static object Permit => ControlledPermit;
 internal static bool IsBound => _actions!=null;
 internal static bool HasPermit(object? permit)=>ReferenceEquals(permit,ControlledPermit);
 internal static void NoteBypass()=>Interlocked.Exchange(ref _bypassAttempt,1);
 internal static bool HasBypass=>Volatile.Read(ref _bypassAttempt)!=0;
 internal static void RecordExternalInput(string kind)=>_actions?.RecordExternalInput(kind);
 internal static void NoteInputFailure()=>Interlocked.Exchange(ref _bypassAttempt,1);
 private static readonly object Gate=new();
 private static ArenaControlledActions? _actions;
 private static string? _stream,_run;
 internal static string StartStream(string commandId)
 {
  if(string.IsNullOrWhiteSpace(commandId)||commandId.Length>128)throw new ApiException(400,"invalid_request","Invalid command ID.");
  return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commandId)))[..32].ToLowerInvariant();
 }
 internal static object PrepareStart(string commandId,string run,JsonElement task)
 {
  var stream=StartStream(commandId);
  if(!ArenaSeedPolicy.IsValid(run))throw new ApiException(400,"invalid_seed","Invalid seed.");
  var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STS2AIAgent","arena-controlled",stream);
  Directory.CreateDirectory(dir);
  var path=Path.Combine(dir,"start-binding.json");
  var taskHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(task.GetRawText())));
  var binding=JsonSerializer.Serialize(new {command_id=commandId,stream_id=stream,game_run_ref=run,task_hash=taskHash});
  try {using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);var bytes=Encoding.UTF8.GetBytes(binding);file.Write(bytes);file.Flush(true);}
  catch(IOException) {if(!File.Exists(path)||File.ReadAllText(path)!=binding)throw new ApiException(409,"start_conflict","Start command binding already exists or differs.");}
  return Bind(stream,run,run);
 }
 internal static object RestoreStart(string commandId)
 {
  var stream=StartStream(commandId);
  var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STS2AIAgent","arena-controlled",stream,"start-binding.json");
  if(!File.Exists(path))throw new ApiException(404,"start_not_found","Start command not found.");
  using var doc=JsonDocument.Parse(File.ReadAllText(path));var root=doc.RootElement;
  if(root.GetProperty("command_id").GetString()!=commandId||root.GetProperty("stream_id").GetString()!=stream)throw new InvalidDataException("Invalid start binding.");
  var run=root.GetProperty("game_run_ref").GetString()??throw new InvalidDataException("Missing run binding.");
  return Bind(stream,run,run);
 }
 internal static object Bind(string? stream,string? run,string actual)
 {
  if(!Guid.TryParseExact(stream,"N",out _)||string.IsNullOrWhiteSpace(run)||run!=actual||actual=="run_unknown")
   throw new ApiException(409,"binding_mismatch","Active game seed and stream must match the requested binding.");
  lock(Gate) {
   if(_actions!=null) {
    if(_stream!=stream||_run!=run)throw new ApiException(409,"already_bound","A different diagnostic stream is active.");
   } else {
    var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STS2AIAgent","arena-controlled",stream!);
    _actions=new ArenaControlledActions(directory,stream!,run!);_stream=stream;_run=run;
   }
   return new {stream_id=stream,game_run_ref=run,coverage="bridge_confirmed_only",complete=false};
  }
 }
 internal static IReadOnlyList<ArenaFact> Read(long after) {lock(Gate) return (_actions??throw new ApiException(409,"not_bound","No diagnostic stream is active.")).ReadFacts(after);}
 internal static ArenaControlledActions Actions=>_actions??throw new ApiException(409,"not_bound","No diagnostic stream is active.");
 internal static void CheckRun(string actual) {if(actual!=_run)throw new ApiException(409,"binding_mismatch","Active game seed changed.");}
}
