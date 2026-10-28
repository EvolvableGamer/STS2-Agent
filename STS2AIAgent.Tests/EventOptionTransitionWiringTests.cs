namespace STS2AIAgent.Tests;
internal static class EventOptionTransitionWiringTests
{
 public static void CapturesOldPageBeforeDispatch()
 {
  var source=AgentSourceFixture.Read("STS2AIAgent/Game/GameActionService.Rooms.cs");
  var start=source.IndexOf("private static async Task<ActionResponsePayload> ExecuteChooseEventOptionAsync",StringComparison.Ordinal);
  var end=source.IndexOf("private static async Task<bool> WaitForEventScreenTransitionAsync",start,StringComparison.Ordinal);
  Assert.True(start>=0&&end>start,"Event handler is missing");
  var body=source[start..end].Replace("\r\n", "\n", StringComparison.Ordinal);var dispatch=body.IndexOf("ChooseLocalOption(request.option_index.Value)",StringComparison.Ordinal);
  var eventId=body.IndexOf("var previousEventId = eventModel.Id?.Entry",StringComparison.Ordinal);
  var signature=body.IndexOf("var previousOptionSignature = BuildEventOptionSignature(eventModel)",StringComparison.Ordinal);
  var count=body.IndexOf("var previousOptionCount = options.Count",StringComparison.Ordinal);
  Assert.True(dispatch>0&&eventId>=0&&signature>=0&&count>=0&&eventId<dispatch&&signature<dispatch&&count<dispatch,"Old event ID, signature and count must be captured before a synchronous option transition.");
  Assert.True(body.Contains("WaitForEventOptionTransitionAsync(\n            previousEventId,\n            previousOptionSignature,\n            previousOptionCount",StringComparison.Ordinal),"Transition wait must use the captured old page.");
 }
}
