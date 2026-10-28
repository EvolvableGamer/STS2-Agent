using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using STS2AIAgent.Server;
namespace STS2AIAgent.Game;
// WindowInput is a pre-Viewport observation; it does not prove which game operation resulted.
// Direct Viewport.PushInput and other input paths may bypass it. Never claim full coverage.
internal static class ArenaWindowInputObserver
{
 private static Window? _window;
 internal static void Attach()
 {
  var root=NGame.Instance?.GetTree()?.Root ?? throw new InvalidOperationException("Game window unavailable");
  if(ReferenceEquals(root,_window))return;
  if(_window!=null&&GodotObject.IsInstanceValid(_window))_window.WindowInput-=OnWindowInput;
  _window=root;_window.WindowInput+=OnWindowInput;
 }
 private static void OnWindowInput(InputEvent input)
 {
  string? kind=input switch {
   InputEventKey k when ArenaInputPolicy.ShouldRecord(k.Pressed,k.Echo)=>"keyboard",
   InputEventMouseButton m when m.Pressed=>"mouse_button",
   InputEventJoypadButton j when j.Pressed=>"joypad_button",
   InputEventJoypadMotion j when Math.Abs(j.AxisValue)>0.2=>"joypad_motion",
   InputEventScreenTouch t when t.Pressed=>"touch",
   InputEventAction a when a.Pressed=>"action",
   _=>null
  };
  if(kind==null)return;
  try {ArenaControlledSession.RecordExternalInput(kind);}
  catch(Exception ex) {
   ArenaControlledSession.NoteInputFailure();
   _window?.SetDisableInput(true);
   Log.Error("[STS2AIAgent] Arena input recording failed; input disabled: "+ex.GetType().Name);
  }
 }
}
