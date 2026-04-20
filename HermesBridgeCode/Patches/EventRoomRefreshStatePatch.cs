using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NEventRoom), "RefreshEventState")]
internal static class EventRoomRefreshStatePatch
{
    public static void Postfix(EventModel eventModel)
    {
        BridgeTrace.Log("NEventRoom.RefreshEventState postfix fired");
        if (eventModel is not null)
        {
            var extracted = BridgeStateExtractor.ExtractEvent(eventModel);
            BridgeSnapshotWriter.SetScreen("Event", "EventRefreshState");
            BridgeSnapshotWriter.SetEvent(extracted, "EventRefreshStatePayload");
        }
    }
}
