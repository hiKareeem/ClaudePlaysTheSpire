using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NEventRoom), "BeforeOptionChosen")]
internal static class EventRoomBeforeOptionChosenPatch
{
    public static void Prefix(EventOption option)
    {
        try
        {
            var title = option is not null
                ? BridgeStateExtractor.SafeLocString(option.Title)
                : "<null>";
            BridgeTrace.Log($"NEventRoom.BeforeOptionChosen option={title ?? "<null>"}");
        }
        catch
        {
            BridgeTrace.Log("NEventRoom.BeforeOptionChosen (unreadable option)");
        }
    }
}
