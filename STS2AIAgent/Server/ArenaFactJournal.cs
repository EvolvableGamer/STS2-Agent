using System.Text;
using System.Text.Json;
namespace STS2AIAgent.Server;
internal sealed record ArenaFact(string stream_id,long source_seq,string game_run_ref,string kind,JsonElement payload);
// Private journal primitive. Its existence does not prove game coverage or completeness.
internal sealed class ArenaFactJournal : IDisposable
{
 private readonly FileStream _file;
 private readonly List<ArenaFact> _facts = new();
 private readonly string _stream, _run;
 private bool _poisoned;
 public ArenaFactJournal(string path,string stream,string run)
 {
  if(string.IsNullOrWhiteSpace(stream)||string.IsNullOrWhiteSpace(run))throw new ArgumentException("Missing fact binding");
  _stream=stream;_run=run;
  _file=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  try {
   using var reader=new StreamReader(_file,Encoding.UTF8,false,4096,true);
   var text=reader.ReadToEnd();
   if(text.Length>0&&!text.EndsWith('\n'))throw new InvalidDataException("Incomplete fact journal tail");
   foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries)) {
    var fact=JsonSerializer.Deserialize<ArenaFact>(line)??throw new InvalidDataException("Invalid fact");
    if(fact.stream_id!=stream||fact.game_run_ref!=run||fact.source_seq!=_facts.Count+1||string.IsNullOrWhiteSpace(fact.kind))throw new InvalidDataException("Fact binding or sequence mismatch");
    _facts.Add(fact);
   }
   _file.Seek(0,SeekOrigin.End);
  } catch {_file.Dispose();throw;}
 }
 public long LastSequence => _facts.Count;
 public ArenaFact Append(string kind,object payload)
 {
  if(_poisoned)throw new InvalidOperationException("Fact journal is poisoned");
  if(string.IsNullOrWhiteSpace(kind))throw new ArgumentException("Missing fact kind");
  var fact=new ArenaFact(_stream,_facts.Count+1,_run,kind,JsonSerializer.SerializeToElement(payload));
  var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fact)+"\n");
  try {_file.Write(bytes);_file.Flush(true);} catch {_poisoned=true;throw;}
  _facts.Add(fact);return fact;
 }
 public IReadOnlyList<ArenaFact> ReadAfter(long afterSeq)
 {
  if(afterSeq<0||afterSeq>_facts.Count)throw new ArgumentOutOfRangeException(nameof(afterSeq));
  return _facts.Skip((int)afterSeq).Take(256).ToArray();
 }
 public void Dispose()=>_file.Dispose();
}
