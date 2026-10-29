using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
namespace STS2AIAgent.Server;
internal sealed record ArenaLiveReplayBinding(object Run,object Executor,string Stream,string Seed,string Character,int Ascension);
internal sealed record ArenaLiveReplayResult(string stream_id,string game_run_ref,string source,string content,int action_count,bool complete);
internal static class ArenaLiveReplaySnapshot
{
 internal static ArenaLiveReplayResult Build(ArenaLiveReplayBinding binding,object? run,object? executor,string stream,string? seed,string? character,int? ascension,bool naturalTerminal,Func<IReadOnlyList<string>> read)
 {
  if(run==null||executor==null||!ReferenceEquals(binding.Run,run)||!ReferenceEquals(binding.Executor,executor)||
   binding.Stream!=stream||binding.Seed!=seed||!string.Equals(binding.Character,character,StringComparison.OrdinalIgnoreCase)||
   binding.Ascension!=ascension||!naturalTerminal||!Regex.IsMatch(binding.Seed,"^[A-Za-z0-9]{1,100}$")||
   !Regex.IsMatch(binding.Character,"^[A-Za-z0-9_]{1,100}$"))throw new InvalidOperationException("Replay binding or terminal mismatch");
  var actions=read()??throw new InvalidOperationException("Replay buffer unavailable");
  var builder=new StringBuilder();builder.Append("# Character: ").Append(binding.Character).Append('\n').Append("# Seed: ").Append(binding.Seed).Append('\n').Append("# Ascension: ").Append(binding.Ascension).Append('\n');
  int count=0;
  foreach(var entry in actions){
   if(entry==null||entry.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidOperationException("Malformed replay entry");
   if(!string.IsNullOrWhiteSpace(entry)&&!entry.TrimStart().StartsWith('#'))count++;
   builder.Append(entry).Append('\n');
   if(builder.Length>3*1024*1024)throw new InvalidOperationException("Replay too large");
  }
  if(count==0||Encoding.UTF8.GetByteCount(builder.ToString())>3*1024*1024)throw new InvalidOperationException("Empty or oversized replay");
  return new ArenaLiveReplayResult(binding.Stream,binding.Seed,"runreplays-live-buffer/1",builder.ToString(),count,false);
 }
 internal static Func<IReadOnlyList<string>> Resolve(Type? type)
 {
  var method=type?.GetMethod("SnapshotMinimal",BindingFlags.Public|BindingFlags.Static,null,Type.EmptyTypes,null);
  if(method==null||!typeof(IReadOnlyList<string>).IsAssignableFrom(method.ReturnType))throw new InvalidOperationException("RunReplays snapshot unavailable");
  return ()=>method.Invoke(null,null) as IReadOnlyList<string> ?? throw new InvalidOperationException("RunReplays snapshot unavailable");
 }
}
